using System.Diagnostics;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui.E39;

/// <summary>
/// Centralita del catálogo E39, con las teclas de un script INPA: F1 Info, F2 Ident, F4 Fehlerspeicher
/// (Shift+F4 borrar), F5 Status (páginas del catálogo en bucle), F6 Steuern, F7 todos los jobs, F10 Volver.
/// </summary>
[Activity(Label = "Centralita E39", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class E39EcuActivity : InpaActivity
{
    public const string ExtraEcuIndex = "e39_ecu_index";

    const int StatusPollMs = 1000;
    const int MaxLabelPad = 30;
    const int MaxValuePad = 12;

    E39Ecu _ecu = null!;
    string? _sgbd;                          // variante real (.prg) una vez resuelta
    ResultPanel _results = null!;
    TextView _heading = null!;
    TextView _status = null!;
    bool _busyJob;                          // job puntual en curso (bloquea otras teclas salvo F10)
    bool _pollInFlight;                     // job de Status en curso
    int _generation;                        // descarta resultados de operaciones ya sustituidas
    CancellationTokenSource? _pollCts;

    protected override bool PauseHeader => _busyJob || _pollCts != null;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        int index = Intent?.GetIntExtra(ExtraEcuIndex, -1) ?? -1;
        var ecus = E39Catalog.Ecus;
        if (index < 0 || index >= ecus.Count)
        {
            UiUtil.Toast(this, "Centralita E39 no válida");
            Finish();
            return;
        }
        _ecu = ecus[index];

        InitInpa(Resource.Layout.e39_ecu, _ecu.Title);
        _heading = FindViewById<TextView>(Resource.Id.e39_heading)!;
        _status = FindViewById<TextView>(Resource.Id.e39_status)!;
        _results = new ResultPanel(FindViewById<LinearLayout>(Resource.Id.e39_results)!);
        UpdateSubtitle();

        SetKey(1, "Info", ShowInfo);
        SetKey(2, "Ident", () => RunOnce(_ecu.IdentJob, "Identificación"));
        SetKey(4, "Errores", ReadFaults);
        SetShiftKey(4, "Borrar err.", ConfirmClearFaults);
        SetKey(5, "Status", StatusMenu);
        SetKey(6, "Steuern", SteuernMenu);
        SetKey(7, "Jobs", OpenAllJobs);
        SetKey(10, "Volver", Back);
        RefreshKeys();

        _heading.Text = "Seleccione una función";
        var menu = string.Join("\n", DefinedKeys().Select(k => KeyCaption(k.F, k.Shift, KeyText(k.F, k.Shift, k.Label))));
        _results.ShowInfo($"{_ecu.Title} ({_ecu.Sgbd})", menu);
        Exclusive(async () => { await EnsureSgbdAsync(); });
    }

    static string KeyText(int f, bool shift, string label) => (f, shift) switch
    {
        (2, false) => "Identificación",
        (4, false) => "Leer memoria de errores",
        (4, true) => "Borrar memoria de errores",
        (5, false) => "Status (lectura continua)",
        (6, false) => "Steuern (activar componentes)",
        (7, false) => "Todos los jobs (Tool32)",
        _ => label,
    };

    void UpdateSubtitle() => SetSubtitle($"{_ecu.Sgbd} → {_sgbd ?? "?"}");

    // ---------------------------------------------------------------- control de teclas

    protected override bool BeforeKey(int f, bool shift)
    {
        bool isBack = f == 10 && !shift;
        if (_busyJob && !isBack)
        {
            UiUtil.Toast(this, "Hay un job en curso: espera o pulsa Abortar");
            return false;
        }
        if (isBack && (_busyJob || _pollInFlight))
            AbortJob();
        // Cualquier tecla detiene la lectura continua de Status (como en INPA).
        StopPolling();
        return true;
    }

    void Back() => Finish();

    protected override void OnPause()
    {
        StopPolling();
        base.OnPause();
    }

    /// <summary>Ejecuta una operación exclusiva (sin otras teclas salvo F10) con gestión de errores.</summary>
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
            _results.ShowError(_sgbd ?? _ecu.Sgbd, "ERROR: " + UiUtil.Describe(ex));
        }
        finally
        {
            _busyJob = false;
            if (_pollCts == null)   // si la operación ha arrancado el Status, su barra "Parar" se queda
                HideBusy();
        }
    }

    /// <summary>Resuelve la variante (.grp → .prg) una sola vez; null si falla (se reintenta con la siguiente tecla).</summary>
    async Task<string?> EnsureSgbdAsync()
    {
        if (_sgbd != null)
            return _sgbd;
        ShowBusy($"Identificando variante de {_ecu.Sgbd}…");
        var variant = await Diag.ResolveVariantAsync(_ecu.Sgbd);
        HideBusy();
        if (string.IsNullOrWhiteSpace(variant))
        {
            ShowResults();
            _results.ShowError($"{_ecu.Title} ({_ecu.Sgbd})",
                "No se pudo identificar la variante de la centralita. Comprueba el cable K+DCAN (pines 7/8), " +
                "el encendido y que el archivo está en la carpeta ECU. Pulsa cualquier tecla F para reintentar.");
            return null;
        }
        _sgbd = variant;
        UpdateSubtitle();
        _results.ShowInfo(_ecu.Sgbd, $"Variante identificada: {_sgbd}", append: true);
        return _sgbd;
    }

    // ---------------------------------------------------------------- ejecución de jobs

    void RunOnce(string job, string heading, string args = "") =>
        Exclusive(async () =>
        {
            var sgbd = await EnsureSgbdAsync();
            if (sgbd != null)
                await RunJobCore(sgbd, job, heading, args);
        });

    /// <summary>Ejecuta un job y lo muestra; JOB_STATUS distinto de OKAY se añade como error en rojo.</summary>
    async Task<JobResult?> RunJobCore(string sgbd, string job, string heading, string args)
    {
        int gen = ++_generation;
        ShowResults();
        _heading.Text = heading;
        ShowBusy($"{sgbd} : {job}…");
        try
        {
            var r = await Diag.RunJobAsync(sgbd, job, args);
            if (gen != _generation)
                return null;
            _results.ShowResult(sgbd, job, r);
            if (r.Ok && StatusError(r) is string err)
                _results.ShowError("La centralita no ha respondido OKAY", err, append: true);
            return r;
        }
        catch (Exception ex)
        {
            if (gen == _generation)
                _results.ShowException(sgbd, job, ex);
            return null;
        }
    }

    /// <summary>Texto de error de un resultado, o null si es correcto (Ok y JOB_STATUS vacío u OKAY).</summary>
    static string? StatusError(JobResult r)
    {
        if (!r.Ok)
            return r.Error.Length > 0 ? r.Error : "El job ha fallado";
        if (r.JobStatus.Length > 0 && !r.JobStatus.Equals("OKAY", StringComparison.OrdinalIgnoreCase))
            return "JOB_STATUS: " + r.JobStatus;
        return null;
    }

    void ShowInfo()
    {
        var text = $"Centralita : {_ecu.Title}\n" +
                   $"SGBD       : {_ecu.Sgbd}\n" +
                   $"Variante   : {_sgbd ?? "(sin identificar)"}\n\n" +
                   $"Ident      : {_ecu.IdentJob}\n" +
                   $"Errores    : {_ecu.FsReadJob} / {_ecu.FsClearJob}\n\n" +
                   $"Status ({_ecu.StatusPages.Count}):\n" +
                   string.Concat(_ecu.StatusPages.Select(p => $"  • {p.Title} ({p.Values.Count} valores)\n")) +
                   $"\nSteuern ({_ecu.Actions.Count}):\n" +
                   string.Concat(_ecu.Actions.Select(a => $"  • {a.Title}  [{a.Job}]\n"));
        JobDialogs.Message(this, "Información", text);
    }

    // F4: memoria de errores (ResultPanel destaca F_ORT_NR / F_ORT_TEXT)
    void ReadFaults() =>
        Exclusive(async () =>
        {
            var sgbd = await EnsureSgbdAsync();
            if (sgbd == null)
                return;
            var r = await RunJobCore(sgbd, _ecu.FsReadJob, "Memoria de errores", "");
            if (r != null && StatusError(r) == null)
            {
                int faults = r.Sets.Count(s => s.ContainsKey("F_ORT_NR"));
                _heading.Text = faults == 0 ? "Memoria de errores: sin errores" : $"Memoria de errores: {faults} error(es)";
            }
        });

    void ConfirmClearFaults()
    {
        JobDialogs.Confirm(this, "Borrar memoria de errores",
            $"¿Borrar la memoria de errores de {_ecu.Title} ({_sgbd ?? _ecu.Sgbd}, job {_ecu.FsClearJob})?\n\n" +
            "Los errores almacenados se perderán. Hazlo con el encendido conectado y el motor parado.",
            "Borrar",
            () => Exclusive(async () =>
            {
                var sgbd = await EnsureSgbdAsync();
                if (sgbd == null)
                    return;
                await RunJobCore(sgbd, _ecu.FsClearJob, "Borrar memoria de errores", "");
                _results.ShowInfo("Memoria de errores", "Pulsa F4 para volver a leerla.", append: true);
            }));
    }

    /// <summary>Lista simple de opciones; con una sola opción se elige directamente.</summary>
    void PickItem(string title, IReadOnlyList<string> items, Action<int> onPick)
    {
        if (items.Count == 1)
        {
            onPick(0);
            return;
        }
        new AlertDialog.Builder(this)
            .SetTitle(title)!
            .SetItems(items.ToArray(), (_, e) => onPick(e.Which))!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .Show();
    }

    // F5: elegir una página de Status y refrescarla cada ~1 s
    void StatusMenu()
    {
        if (_ecu.StatusPages.Count == 0)
        {
            ShowResults();
            _results.ShowInfo("Status", "Esta centralita no tiene páginas de status en el catálogo E39. Usa F7 (todos los jobs).");
            return;
        }
        PickItem("Status - " + _ecu.Title, _ecu.StatusPages.Select(p => p.Title).ToList(), i =>
            Exclusive(async () =>
            {
                var sgbd = await EnsureSgbdAsync();
                if (sgbd != null)
                    StartPolling(sgbd, _ecu.StatusPages[i]);
            }));
    }

    // F6: elegir activación, mostrar aviso, confirmar y ejecutar una vez
    void SteuernMenu()
    {
        if (_ecu.Actions.Count == 0)
        {
            ShowResults();
            _results.ShowInfo("Steuern", "Esta centralita no tiene activaciones en el catálogo E39. Usa F7 (todos los jobs).");
            return;
        }
        PickItem("Steuern - " + _ecu.Title, _ecu.Actions.Select(a => a.Title).ToList(), i =>
        {
            var a = _ecu.Actions[i];
            var warning = a.Warning.Length > 0
                ? a.Warning
                : "Este job activa componentes reales del vehículo. Vehículo parado y nadie cerca de piezas móviles.";
            JobDialogs.Confirm(this, "⚠ " + a.Title,
                warning + "\n\n" +
                $"Job: {a.Job}" + (a.Args.Length > 0 ? $"  argumentos \"{a.Args}\"" : "") + "\n\n" +
                "Continúa solo si sabes exactamente lo que hace.",
                "Ejecutar",
                () => Exclusive(async () =>
                {
                    var sgbd = await EnsureSgbdAsync();
                    if (sgbd != null)
                        await RunJobCore(sgbd, a.Job, "Steuern: " + a.Title, a.Args);
                }));
        });
    }

    // F7: pantalla genérica de la variante (todos los jobs, estilo Tool32)
    void OpenAllJobs() =>
        Exclusive(async () =>
        {
            var sgbd = await EnsureSgbdAsync();
            if (sgbd == null)
                return;
            var intent = new Intent(this, typeof(EcuActivity));
            intent.PutExtra(EcuSelectActivity.ExtraEcuName, sgbd);
            intent.PutExtra(EcuSelectActivity.ExtraIsGroup, false);
            StartActivity(intent);
        });

    // ---------------------------------------------------------------- Status continuo

    /// <summary>Muestra el panel de resultados (true) o la tabla de Status (false).</summary>
    void ShowResults(bool results = true)
    {
        _results.Clear();
        _status.Visibility = results ? ViewStates.Gone : ViewStates.Visible;
    }

    void StartPolling(string sgbd, E39Page page)
    {
        StopPolling();
        var cts = new CancellationTokenSource();
        _pollCts = cts;
        int gen = ++_generation;
        _heading.Text = "Status: " + page.Title;
        ShowResults(false);
        _status.Text = "Leyendo…";
        ShowBusy("Status cada 1 s. Pulsa cualquier tecla para parar.", "Parar", StopPolling);
        PollLoop(sgbd, page, cts, gen);
    }

    async void PollLoop(string sgbd, E39Page page, CancellationTokenSource cts, int gen)
    {
        // Un job por llamada (job + argumentos distintos), pidiendo solo los resultados necesarios.
        var groups = page.Values
            .GroupBy(v => (Job: v.Job.Trim().ToUpperInvariant(), v.Args))
            .Select(g => (g.Key.Job, g.Key.Args, Values: g.ToList(),
                Results: string.Join(";", g.Select(v => v.Result.Trim()).Append("JOB_STATUS")
                    .Distinct(StringComparer.OrdinalIgnoreCase))))
            .ToList();
        int count = 0;
        while (!cts.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            var cells = new Dictionary<E39Value, (string Text, bool Error)>();
            foreach (var g in groups)
            {
                JobResult r;
                _pollInFlight = true;
                try
                {
                    r = await Diag.RunJobAsync(sgbd, g.Job, g.Args, g.Results);
                }
                catch (Exception ex)
                {
                    r = new JobResult { Ok = false, Error = UiUtil.Describe(ex) };
                }
                finally
                {
                    _pollInFlight = false;
                }
                if (cts.IsCancellationRequested || gen != _generation)
                    return;
                var error = StatusError(r);
                foreach (var v in g.Values)
                {
                    cells[v] = error != null ? ("ERROR " + error, true)
                        : FindResult(r, v.Result) is string text ? (text, false)
                        : ($"ERROR sin resultado {v.Result}", true);
                }
            }
            _status.TextFormatted = BuildTable(page, cells, ++count);
            try
            {
                await Task.Delay((int)Math.Max(100, StatusPollMs - sw.ElapsedMilliseconds), cts.Token);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
        }
    }

    static string? FindResult(JobResult r, string name)
    {
        foreach (var set in r.Sets)
        {
            foreach (var (key, value) in set)
            {
                if (key.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                    return value;
            }
        }
        return null;
    }

    /// <summary>Tabla estilo INPA: "Etiqueta ........  valor unidad", errores en rojo.</summary>
    static SpannableStringBuilder BuildTable(E39Page page, Dictionary<E39Value, (string Text, bool Error)> cells, int count)
    {
        int labelPad = Math.Min(MaxLabelPad, page.Values.Count == 0 ? 0 : page.Values.Max(v => v.Label.Length)) + 3;
        int valuePad = Math.Min(MaxValuePad, cells.Values.Where(c => !c.Error).Select(c => c.Text.Length).DefaultIfEmpty(0).Max());

        var sb = new SpannableStringBuilder();
        Append(sb, page.Title, new StyleSpan(TypefaceStyle.Bold), new ForegroundColorSpan(UiUtil.BlueDark));
        sb.Append($"   {DateTime.Now:HH:mm:ss}   #{count}\n");
        foreach (var v in page.Values)
        {
            sb.Append("\n");
            sb.Append(v.Label.Length + 1 >= labelPad ? v.Label + " " : (v.Label + " ").PadRight(labelPad - 1, '.') + " ");
            var (text, error) = cells.GetValueOrDefault(v, ("--", true));
            if (error)
                Append(sb, text, new ForegroundColorSpan(UiUtil.ErrorText));
            else
            {
                Append(sb, text.PadLeft(valuePad), new StyleSpan(TypefaceStyle.Bold));
                if (v.Unit.Length > 0)
                    sb.Append(" " + v.Unit);
            }
        }
        return sb;
    }

    static void Append(SpannableStringBuilder sb, string text, params Java.Lang.Object[] spans)
    {
        int start = sb.Length();
        sb.Append(text);
        foreach (var span in spans)
            sb.SetSpan(span, start, sb.Length(), SpanTypes.ExclusiveExclusive);
    }

    void StopPolling()
    {
        if (_pollCts == null)
            return;
        _pollCts.Cancel();
        _pollCts = null;
        HideBusy();
        _heading.Text += " (detenido)";
    }
}
