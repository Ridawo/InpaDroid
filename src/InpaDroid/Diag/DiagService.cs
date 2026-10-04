using System.Collections.Concurrent;
using System.Globalization;
using Android.Content;
using Android.Hardware.Usb;
using Android.Net;
using EdiabasLib;

namespace InpaDroid.Diag;

// Envoltorio de EdiabasNet. EdiabasNet no es thread-safe: todas las llamadas a _ediabas se hacen en un único
// hilo de trabajo (_worker) que consume _queue en orden. Las tareas devueltas nunca fallan: los errores se
// devuelven como JobResult.Ok = false o como valores vacíos/null.
public sealed class DiagService : IDisposable
{
    private const string ResultSetStatus = "JOB_STATUS";

    private readonly Context _context;
    private readonly AdapterSettings _settings;
    private readonly TcpClientWithTimeout.NetworkData _networkData;
    private readonly EdiabasNet _ediabas;
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _worker;
    private volatile bool _abort;
    private volatile bool _disposed;
    // Se incrementa en cada Abort: cancela el job en curso y también los que ya estaban en cola.
    private int _abortGeneration;

    // EdiabasNet usa la codificación 1252, que en Android no existe sin este proveedor (como BmwDeepObd MyApplication).
    static DiagService() => System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

    public DiagService(Context context, AdapterSettings settings)
    {
        _context = context.ApplicationContext ?? context;
        _settings = settings;
        _networkData = new TcpClientWithTimeout.NetworkData(
            _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager);

        // Igual que BmwDeepObd EdiabasThread: interfaz + AbortJobFunc, luego EcuPath.
        _ediabas = new EdiabasNet
        {
            EdInterfaceClass = _settings.Type == AdapterType.Enet ? new EdInterfaceEnet() : new EdInterfaceObd(),
            AbortJobFunc = () => _abort
        };
        _ediabas.SetConfigProperty("EcuPath", _settings.EcuPath);
        _ediabas.SetConfigProperty("IfhTrace", "0");
        // Se configura después de asignar EdInterfaceClass: al asignarla, la interfaz lee sus valores del config.
        ConfigureInterface();

        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "InpaDroid.Diag" };
        _worker.Start();
    }

    public IReadOnlyList<EcuFile> ScanEcuFolder()
    {
        try
        {
            string path = _settings.EcuPath;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return [];
            }

            return Directory.EnumerateFiles(path)
                .Where(f => HasExtension(f, EdiabasNet.GroupFileExt) || HasExtension(f, EdiabasNet.PrgFileExt))
                .Select(f => new EcuFile
                {
                    Name = Path.GetFileNameWithoutExtension(f),
                    FullPath = f,
                    IsGroup = HasExtension(f, EdiabasNet.GroupFileExt)
                })
                .OrderByDescending(e => e.IsGroup)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public Task<JobResult> RunJobAsync(string sgbd, string job, string args = "", string results = "") =>
        Enqueue(() =>
        {
            ResolveSgbd(sgbd);
            ExecuteJob(job, args, results);
            return ToJobResult(_ediabas.ResultSets);
        }, error => new JobResult { Ok = false, Error = error });

    // Como EdiabasToolActivity.ReadSgbd: jobs internos _JOBS/_JOBCOMMENTS/_ARGUMENTS/_RESULTS, sin comunicación.
    public Task<IReadOnlyList<JobInfo>> GetJobsAsync(string sgbd) =>
        Enqueue(() => ReadJobs(sgbd), _ => (IReadOnlyList<JobInfo>)[]);

    public Task<string> ResolveVariantAsync(string sgbd) =>
        Enqueue(() =>
        {
            ResolveSgbd(sgbd);
            // Tras ResolveSgbdFile de un .grp (job IDENT), SgbdFileName es la variante, p.ej. "d_motor.prg".
            return Path.GetFileNameWithoutExtension(_ediabas.SgbdFileName ?? "").ToUpperInvariant();
        }, _ => "");

    public Task<(double? Ubatt, bool? Ignition)> ReadBatteryIgnitionAsync() =>
        Enqueue<(double?, bool?)>(() =>
        {
            double? ubatt = null;
            double? mv = FindNumber(TryRunUtilityJob("STATUS_UBATT"), "STAT_UBATT_WERT", "STAT_UBATT");
            if (mv != null)
            {
                // UTILITY devuelve mV; si el valor ya parece estar en voltios se deja tal cual.
                ubatt = mv > 100 ? mv / 1000.0 : mv;
            }

            double? ignition = FindNumber(TryRunUtilityJob("STATUS_ZUENDUNG"), "STAT_ZUENDUNG");
            return (ubatt, ignition == null ? (bool?)null : ignition != 0);
        }, _ => (null, null));

    public void Abort()
    {
        Interlocked.Increment(ref _abortGeneration);
        _abort = true;
        if (_disposed)
        {
            return;
        }
        try
        {
            // Como el botón Abort de EdiabasToolActivity: corta también la transmisión en curso.
            _ediabas.EdInterfaceClass?.TransmitCancel(true);
        }
        catch (Exception)
        {
            // ignorado
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Abort();
        _disposed = true;
        _queue.CompleteAdding();
        // El hilo de trabajo cierra EdiabasNet al vaciar la cola; se espera un poco para liberar el adaptador
        // antes de que se cree otro DiagService, sin bloquear demasiado el hilo de UI.
        _worker.Join(TimeSpan.FromSeconds(3));
    }

    private void WorkerLoop()
    {
        foreach (Action work in _queue.GetConsumingEnumerable())
        {
            work();
        }

        try
        {
            _ediabas.CloseSgbd();
            _ediabas.Dispose();
        }
        catch (Exception)
        {
            // ignorado
        }
    }

    private Task<T> Enqueue<T>(Func<T> work, Func<string, T> onError)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        int generation = Volatile.Read(ref _abortGeneration);

        void Run()
        {
            if (_disposed)
            {
                tcs.SetResult(onError("Servicio de diagnóstico cerrado"));
                return;
            }
            if (generation != Volatile.Read(ref _abortGeneration))
            {
                tcs.SetResult(onError("Cancelado"));
                return;
            }
            _abort = false;
            try
            {
                // Abort deja la transmisión cancelada; si no se rearma, todos los jobs siguientes fallan al instante
                // (BmwDeepObd hace lo mismo antes de cada job).
                _ediabas.EdInterfaceClass?.TransmitCancel(false);
                RefreshNetworks();
                tcs.SetResult(work());
            }
            catch (Exception ex)
            {
                tcs.SetResult(onError(_abort ? "Cancelado" : EdiabasNet.GetExceptionText(ex, false, false)));
            }
        }

        try
        {
            _queue.Add(Run);
        }
        catch (InvalidOperationException)
        {
            tcs.SetResult(onError("Servicio de diagnóstico cerrado"));
        }
        return tcs.Task;
    }

    // Puertos y parámetros de conexión como en BmwDeepObd ActivityCommon.SetEdiabasInterface.
    private void ConfigureInterface()
    {
        if (_ediabas.EdInterfaceClass is EdInterfaceEnet enet)
        {
            string host = _settings.EnetHost.Trim();
            enet.RemoteHost = host.Length == 0 || host.Equals(EdInterfaceEnet.AutoIp, StringComparison.OrdinalIgnoreCase)
                ? EdInterfaceEnet.AutoIpAllCombined
                : host;
            // Sin certificados: ENET/DoIP clásico por TCP (SetDoIpSslProperties con appDataDir vacío).
            enet.NetworkProtocol = EdInterfaceEnet.NetworkProtocolTcp;
            enet.Authentication = EdInterfaceEnet.AuthenticationNone;
            enet.ConnectParameter = new EdInterfaceEnet.ConnectParameterType(_networkData, null);
            return;
        }

        var obd = (EdInterfaceObd)_ediabas.EdInterfaceClass;
        switch (_settings.Type)
        {
            case AdapterType.Elm327Bluetooth:
                obd.ComPort = EdBluetoothInterface.PortId + ":" + _settings.BluetoothAddress.Trim() + ";" + EdBluetoothInterface.Elm327Tag;
                obd.ConnectParameter = new EdBluetoothInterface.ConnectParameterType(_networkData, false, false, () => _context);
                break;

            case AdapterType.Elm327Wifi:
            {
                string host = _settings.ElmWifiHost.Trim();
                obd.ComPort = host.Length == 0 ? EdElmWifiInterface.PortId : EdElmWifiInterface.PortId + ":" + host;
                obd.ConnectParameter = new EdElmWifiInterface.ConnectParameterType(_networkData);
                break;
            }

            default: // UsbKDcan (FTDI)
                obd.ComPort = EdFtdiInterface.PortId + "0";
                obd.ConnectParameter = new EdFtdiInterface.ConnectParameterType(
                    _context.GetSystemService(Context.UsbService) as UsbManager);
                break;
        }
    }

    // BmwDeepObd rellena NetworkData con callbacks de red; aquí basta con leer las redes WiFi/Ethernet activas
    // antes de cada operación, para que TcpClientWithTimeout se ligue a la red del adaptador y no a la de datos.
    private void RefreshNetworks()
    {
        if (_settings.Type is not (AdapterType.Enet or AdapterType.Elm327Wifi))
        {
            return;
        }
        ConnectivityManager? connectivity = _networkData.ConnectivityManager;
        if (connectivity == null)
        {
            return;
        }

        lock (_networkData.LockObject)
        {
            _networkData.ActiveWifiNetworks.Clear();
            _networkData.ActiveEthernetNetworks.Clear();
#pragma warning disable CS0618, CA1422 // GetAllNetworks está obsoleto en API 31 pero sigue funcionando
            Network[] networks = connectivity.GetAllNetworks();
#pragma warning restore CS0618, CA1422
            foreach (Network network in networks)
            {
                NetworkCapabilities? caps = connectivity.GetNetworkCapabilities(network);
                if (caps == null)
                {
                    continue;
                }
                if (caps.HasTransport(TransportType.Wifi))
                {
                    _networkData.ActiveWifiNetworks.Add(network);
                }
                else if (caps.HasTransport(TransportType.Ethernet))
                {
                    _networkData.ActiveEthernetNetworks.Add(network);
                }
            }
        }
    }

    private void ResolveSgbd(string sgbd)
    {
        _ediabas.ResolveSgbdFile(Path.GetFileName(sgbd.Trim()));
    }

    private void ExecuteJob(string job, string args, string results)
    {
        _ediabas.ArgString = args;
        _ediabas.ArgStringStd = "";
        results ??= "";   // el setter de EdiabasNet no admite null
        // Si se piden resultados concretos, EDIABAS filtra también JOB_STATUS y un job fallido parecería correcto.
        // Se añade solo si falta: un nombre repetido hace fallar el job.
        bool hasStatus = results.Split(';').Any(r => r.Trim().Equals(ResultSetStatus, StringComparison.OrdinalIgnoreCase));
        if (results.Trim().Length > 0 && !hasStatus)
        {
            results += ";" + ResultSetStatus;
        }
        _ediabas.ResultsRequests = results;
        _ediabas.ExecuteJob(job);
    }

    private IReadOnlyList<JobInfo> ReadJobs(string sgbd)
    {
        try
        {
            _ediabas.NoInitForVJobs = true;
            try
            {
                ResolveSgbd(sgbd);
            }
            catch (Exception) when (IsGroupFile(sgbd))
            {
                // Un .grp sin adaptador no se puede resolver (necesita IDENT): se listan los jobs del propio grupo.
                _ediabas.SgbdFileName = Path.GetFileNameWithoutExtension(sgbd.Trim()) + EdiabasNet.GroupFileExt;
            }

            var jobs = new List<JobInfo>();
            foreach (string name in StringResults(RunVJob("_JOBS", "ALL"), "JOBNAME"))
            {
                List<Dictionary<string, EdiabasNet.ResultData>> commentSets = RunVJob("_JOBCOMMENTS", name);
                jobs.Add(new JobInfo
                {
                    Name = name,
                    Comment = commentSets.Count >= 2 ? string.Join("\n", NumberedStrings(commentSets[1], "JOBCOMMENT")) : "",
                    Arguments = StringResults(RunVJob("_ARGUMENTS", name), "ARG"),
                    Results = StringResults(RunVJob("_RESULTS", name), "RESULT")
                });
            }
            return jobs;
        }
        finally
        {
            _ediabas.NoInitForVJobs = false;
        }
    }

    private List<Dictionary<string, EdiabasNet.ResultData>> RunVJob(string job, string arg)
    {
        ExecuteJob(job, arg, "");
        return _ediabas.ResultSets ?? [];
    }

    private bool IsGroupFile(string sgbd)
    {
        string name = Path.GetFileNameWithoutExtension(sgbd.Trim());
        return ScanEcuFolder().Any(f => f.IsGroup && f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    // Ejecuta un job de UTILITY; null si falla o JOB_STATUS no es OKAY.
    private List<Dictionary<string, EdiabasNet.ResultData>>? TryRunUtilityJob(string job)
    {
        try
        {
            ResolveSgbd("UTILITY");
            ExecuteJob(job, "", "");
            List<Dictionary<string, EdiabasNet.ResultData>>? sets = _ediabas.ResultSets;
            if (sets == null || sets.Count < 2)
            {
                return null;
            }
            if (sets[^1].TryGetValue(ResultSetStatus, out EdiabasNet.ResultData? status) && status.OpData is string text &&
                !text.Equals("OKAY", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return sets;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static JobResult ToJobResult(List<Dictionary<string, EdiabasNet.ResultData>>? resultSets)
    {
        var sets = new List<IReadOnlyDictionary<string, string>>();
        if (resultSets != null)
        {
            // El set 0 contiene los resultados de sistema (VARIANTE, OBJECT, ...).
            foreach (Dictionary<string, EdiabasNet.ResultData> resultDict in resultSets.Skip(1))
            {
                sets.Add(resultDict.ToDictionary(kv => kv.Key, kv => FormatValue(kv.Value.OpData)));
            }
        }

        string jobStatus = "";
        if (sets.Count > 0 && sets[^1].TryGetValue(ResultSetStatus, out string? status))
        {
            jobStatus = status;
        }
        return new JobResult { Ok = true, Sets = sets, JobStatus = jobStatus };
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "",
        string s => s,
        long l => l.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("0.######", CultureInfo.InvariantCulture),
        byte[] bytes => BitConverter.ToString(bytes).Replace('-', ' '),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };

    // Valores de tipo string de "key" en todos los sets salvo el 0 (p.ej. JOBNAME de _JOBS).
    private static List<string> StringResults(List<Dictionary<string, EdiabasNet.ResultData>> resultSets, string key) =>
        resultSets.Skip(1)
            .Select(dict => dict.TryGetValue(key, out EdiabasNet.ResultData? data) ? data.OpData as string : null)
            .OfType<string>()
            .ToList();

    // key0, key1, ... hasta el primero que falte (JOBCOMMENT0, JOBCOMMENT1, ...).
    private static IEnumerable<string> NumberedStrings(Dictionary<string, EdiabasNet.ResultData> dict, string prefix)
    {
        for (int i = 0; dict.TryGetValue(prefix + i.ToString(CultureInfo.InvariantCulture), out EdiabasNet.ResultData? data); i++)
        {
            if (data.OpData is string text)
            {
                yield return text;
            }
        }
    }

    private static double? FindNumber(List<Dictionary<string, EdiabasNet.ResultData>>? resultSets, params string[] keys)
    {
        if (resultSets == null)
        {
            return null;
        }
        foreach (string key in keys)
        {
            foreach (Dictionary<string, EdiabasNet.ResultData> dict in resultSets.Skip(1))
            {
                if (dict.TryGetValue(key, out EdiabasNet.ResultData? data))
                {
                    switch (data.OpData)
                    {
                        case long l:
                            return l;
                        case double d:
                            return d;
                    }
                }
            }
        }
        return null;
    }

    private static bool HasExtension(string file, string ext) =>
        string.Equals(Path.GetExtension(file), ext, StringComparison.OrdinalIgnoreCase);
}
