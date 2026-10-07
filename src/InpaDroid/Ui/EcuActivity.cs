using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

[Activity(Label = "Centralita", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class EcuActivity : InpaActivity
{
    const int StatusPollMs = 1000;

    string _ecuName = "";
    bool _isGroup;
    string? _sgbd;                          // variante real (.prg) una vez resuelta
    IReadOnlyList<JobInfo>? _jobs;
    ResultPanel _results = null!;
    TextView _heading = null!;
    bool _busyJob;                          // job puntual en curso (bloquea otras teclas salvo F10)
    int _generation;                        // descarta resultados de operaciones ya sustituidas
    readonly PollRunner _poll = new();

    protected override bool PauseHeader => _busyJob || _poll.Running;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _ecuName = Intent?.GetStringExtra(EcuSelectActivity.ExtraEcuName) ?? "";
        _isGroup = Intent?.GetBooleanExtra(EcuSelectActivity.ExtraIsGroup, false) ?? false;
        if (!_isGroup)
            _sgbd = _ecuName;

        InitInpa(Resource.Layout.activity_ecu, _ecuName);
        _heading = FindViewById<TextView>(Resource.Id.ecu_heading)!;
        _results = new ResultPanel(FindViewById<LinearLayout>(Resource.Id.ecu_results)!);
        UpdateSubtitle();

        SetKey(1, "Info", () => RunOnce("INFO", "Información de la SGBD"));
        SetKey(2, "Ident", () => RunOnce("IDENT", "Identificación"));
        SetKey(4, "Errores", () => RunOnce("FS_LESEN", "Memoria de errores"));
        SetShiftKey(4, "Borrar err.", ConfirmClearFaults);
        SetKey(5, "Status", StatusMenu);
        SetKey(6, "Steuern", SteuernMenu);
        SetKey(7, "Jobs", JobsMenu);
        SetKey(9, "Copiar", CopyResults);
        SetKey(10, "Volver", Back);
        RefreshKeys();

        _heading.Text = "Seleccione una función";
        var menu = string.Join("\n", DefinedKeys().Select(k => KeyCaption(k.F, k.Shift, KeyText(k.F, k.Shift, k.Label))));
        _results.ShowInfo(_isGroup ? $"Grupo {_ecuName}" : $"SGBD {_ecuName}", menu);
        if (_isGroup)
            Exclusive(async () => { await EnsureSgbdAsync(); });
    }

    static string KeyText(int f, bool shift, string label) => (f, shift) switch
    {
        (4, false) => "Leer memoria de errores",
        (4, true) => "Borrar memoria de errores",
        (5, false) => "Status (lectura continua)",
        (6, false) => "Steuern (activar componentes)",
        (7, false) => "Todos los jobs (Tool32)",
        (9, false) => "Copiar resultados",
        _ => label,
    };

    void UpdateSubtitle() =>
        SetSubtitle(_isGroup ? $"{_ecuName}.grp → {(_sgbd ?? "?")}" : $"{_ecuName}.prg");

    protected override bool BeforeKey(int f, bool shift)
    {
        bool isBack = f == 10 && !shift;
        if (_busyJob && !isBack)
        {
            UiUtil.Toast(this, "Hay un job en curso: espera o pulsa Abortar");
            return false;
        }
        // Cualquier tecla detiene la lectura continua de Status (como en INPA).
        StopPolling();
        return true;
    }

    void Back()
    {
        if (_busyJob)
            AbortJob();
        Finish();
    }

    protected override void OnPause()
    {
        StopPolling();
        base.OnPause();
    }

    async void Exclusive(Func<Task> work)
    {
        if (_busyJob)
            return;
        _busyJob = true;
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            _results.ShowError(_sgbd ?? _ecuName, "ERROR: " + UiUtil.Describe(ex));
        }
        finally
        {
            _busyJob = false;
            HideBusy();
        }
    }

    async Task<string?> EnsureSgbdAsync()
    {
        if (_sgbd != null)
            return _sgbd;
        ShowBusy($"Identificando variante de {_ecuName}…");
        var variant = await Diag.ResolveVariantAsync(_ecuName);
        HideBusy();
        if (string.IsNullOrWhiteSpace(variant))
        {
            _results.ShowError($"Grupo {_ecuName}",
                "No se pudo identificar la variante de la centralita. Comprueba el cable, el encendido y la " +
                "configuración de la interfaz. Pulsa cualquier tecla F para reintentar.");
            return null;
        }
        _sgbd = variant;
        UpdateSubtitle();
        _results.ShowInfo($"Grupo {_ecuName}", $"Variante identificada: {_sgbd}", append: true);
        return _sgbd;
    }

    async Task<IReadOnlyList<JobInfo>?> EnsureJobsAsync(string sgbd)
    {
        if (_jobs is { Count: > 0 })
            return _jobs;
        ShowBusy($"Leyendo jobs de {sgbd}…");
        var jobs = await Diag.GetJobsAsync(sgbd);
        HideBusy();
        if (jobs.Count == 0)
        {
            _results.ShowError(sgbd, $"No se pudo leer la lista de jobs de {sgbd} (¿existe {sgbd}.prg en la carpeta ECU?).");
            return null;
        }
        _jobs = jobs;
        return jobs;
    }

    void RunOnce(string job, string heading, string args = "", string results = "") =>
        Exclusive(async () =>
        {
            var sgbd = await EnsureSgbdAsync();
            if (sgbd != null)
                await RunJobCore(sgbd, job, heading, args, results);
        });

    async Task RunJobCore(string sgbd, string job, string heading, string args, string results)
    {
        int gen = ++_generation;
        _heading.Text = heading;
        ShowBusy($"{sgbd} : {job}…");
        try
        {
            var r = await Diag.RunJobAsync(sgbd, job, args, results);
            if (gen == _generation)
                _results.ShowResult(sgbd, job, r);
        }
        catch (Exception ex)
        {
            if (gen == _generation)
                _results.ShowException(sgbd, job, ex);
        }
    }

    void ConfirmClearFaults()
    {
        var name = _sgbd ?? _ecuName;
        JobDialogs.Confirm(this, "Borrar memoria de errores",
            $"¿Borrar la memoria de errores de {name} (job FS_LOESCHEN)?\n\n" +
            "Los errores almacenados se perderán. Hazlo con el encendido conectado y el motor parado.",
            "Borrar",
            () => Exclusive(async () =>
            {
                var sgbd = await EnsureSgbdAsync();
                if (sgbd == null)
                    return;
                await RunJobCore(sgbd, "FS_LOESCHEN", "Borrar memoria de errores", "", "");
                _results.ShowInfo("Memoria de errores", "Pulsa F4 para volver a leerla.", append: true);
            }));
    }

    void PickJob(string? prefix, string title, Action<string, JobInfo> onPick) =>
        Exclusive(async () =>
        {
            var sgbd = await EnsureSgbdAsync();
            if (sgbd == null)
                return;
            var jobs = await EnsureJobsAsync(sgbd);
            if (jobs == null)
                return;
            var list = prefix == null
                ? jobs
                : jobs.Where(j => j.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            if (list.Count == 0)
            {
                _results.ShowInfo(title, $"{sgbd} no tiene jobs {prefix}*.");
                return;
            }
            JobDialogs.PickJob(this, $"{title} - {sgbd} ({list.Count})", list, job => onPick(sgbd, job));
        });

    void StatusMenu() =>
        PickJob("STATUS_", "Status", (sgbd, job) =>
        {
            if (job.Arguments.Count == 0)
                StartPolling(sgbd, job.Name, "", "");
            else
                JobDialogs.AskArguments(this, sgbd, job, "Leer", (args, res) => StartPolling(sgbd, job.Name, args, res));
        });

    void SteuernMenu() =>
        PickJob("STEUERN", "Steuern", (sgbd, job) =>
            JobDialogs.AskArguments(this, sgbd, job, "Continuar", (args, res) =>
                JobDialogs.Confirm(this, "⚠ ATENCIÓN: activar componentes",
                    $"Vas a ejecutar {job.Name} en {sgbd}" + (args.Length > 0 ? $" con argumentos \"{args}\"" : "") + ".\n\n" +
                    "Los jobs STEUERN activan actuadores reales (relés, motores, válvulas, bombas, inyectores…). " +
                    "Un uso incorrecto puede dañar la centralita o el vehículo, mover piezas o provocar lesiones.\n\n" +
                    "• Vehículo parado, freno de mano puesto y en P / punto muerto.\n" +
                    "• Nadie cerca de piezas móviles.\n" +
                    "• Batería con carga suficiente.\n\n" +
                    "Continúa solo si sabes exactamente lo que hace este job.",
                    "Ejecutar",
                    () => Exclusive(() => RunJobCore(sgbd, job.Name, "Steuern: " + job.Name, args, res)))));

    void JobsMenu() =>
        PickJob(null, "Jobs", (sgbd, job) =>
            JobDialogs.AskArguments(this, sgbd, job, "Ejecutar", (args, res) =>
                Exclusive(() => RunJobCore(sgbd, job.Name, "Job: " + job.Name, args, res))));

    void StartPolling(string sgbd, string job, string args, string results)
    {
        StopPolling();
        int gen = ++_generation;
        int count = 0;
        _heading.Text = "Status: " + job;
        _results.Clear();
        ShowBusy($"{job} cada 1 s. Pulsa cualquier tecla para parar.", "Parar", StopPolling);
        _poll.Start(async ct =>
        {
            var r = await Diag.RunJobAsync(sgbd, job, args, results);
            if (ct.IsCancellationRequested || gen != _generation)
                return false;
            _results.ShowResult(sgbd, job, r, note: $"#{++count}");
            return true;
        }, ex => _results.ShowException(sgbd, job, ex), StatusPollMs, 100);
    }

    void StopPolling()
    {
        if (!_poll.Stop())
            return;
        HideBusy();
        _heading.Text += " (detenido)";
    }

    void CopyResults()
    {
        var text = _results.PlainText();
        if (text.Length == 0)
            return;
        if (GetSystemService(ClipboardService) is ClipboardManager clipboard)
        {
            clipboard.PrimaryClip = ClipData.NewPlainText("InpaDroid", text);
            UiUtil.Toast(this, "Resultados copiados al portapapeles");
        }
    }
}
