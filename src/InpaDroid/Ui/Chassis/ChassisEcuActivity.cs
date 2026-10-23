using InpaDroid.Ui.Live;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui.Chassis;

[Activity(Label = "@string/ch_ecu_label", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
                           | ConfigChanges.UiMode | ConfigChanges.Locale)]
public class ChassisEcuActivity : FaultScreenActivity
{
    public const string ExtraChassisId = "chassis_id";
    public const string ExtraEcuIndex = "chassis_ecu_index";

    const int StatusPollMs = 1000;
    internal const int MaxLabelPad = 30;
    internal const int MaxValuePad = 12;

    ChassisInfo _chassis = null!;
    ChassisEcu _ecu = null!;
    TextView _status = null!;
    bool _pollInFlight;

    protected override bool InFlight => _busyJob || _pollInFlight;

    protected override string EcuSgbd => _ecu.Sgbd;
    protected override string VariantBusyText => UiUtil.Str(this, Resource.String.ch_identifying_variant, _ecu.Sgbd);

    protected override string ChassisName => _chassis.Name;
    protected override string EcuTitle => _ecu.Title;
    protected override string FaultReadJob => _ecu.FsReadJob;
    protected override string FaultClearJob => _ecu.FsClearJob;
    protected override string FaultsTitle => GetString(Resource.String.ch_faults_title)!;
    protected override string ClearTitle => GetString(Resource.String.ch_clear_title)!;
    protected override string ClearMessage(string name) =>
        UiUtil.Str(this, Resource.String.ch_clear_confirm, _ecu.Title, name, _ecu.FsClearJob);
    protected override string ClearYes => GetString(Resource.String.ch_clear_button)!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        int index = Intent?.GetIntExtra(ExtraEcuIndex, -1) ?? -1;
        // Recreada tras matar el proceso: el índice se refiere al catálogo con el override de la carpeta ECU.
        if (savedInstanceState != null)
            DiagHolder.LoadCatalogs(this, DiagHolder.LoadSettings(this).EcuPath);
        _chassis = ChassisCatalog.Get(Intent?.GetStringExtra(ExtraChassisId) ?? "");
        var ecus = _chassis.Ecus;
        if (index < 0 || index >= ecus.Count)
        {
            UiUtil.Toast(this, GetString(Resource.String.ch_ecu_invalid));
            Finish();
            return;
        }
        _ecu = ecus[index];

        InitInpa(Resource.Layout.chassis_ecu, _ecu.Title);
        _heading = FindViewById<TextView>(Resource.Id.chassis_heading)!;
        _status = FindViewById<TextView>(Resource.Id.chassis_status)!;
        _results = new ResultPanel(FindViewById<LinearLayout>(Resource.Id.chassis_results)!);
        UpdateSubtitle();

        SetKey(1, GetString(Resource.String.ch_key_info), ShowInfo, Resource.Drawable.ic_info);
        SetKey(2, GetString(Resource.String.ch_key_ident), () => RunOnce(_ecu.IdentJob, GetString(Resource.String.ch_key_ident)), Resource.Drawable.ic_ident);
        SetKey(3, GetString(Resource.String.fb_key_report), ShareReport, Resource.Drawable.ic_copy);
        SetKey(4, GetString(Resource.String.ch_key_faults), () => ReadFaults(), Resource.Drawable.ic_fault);
        SetShiftKey(4, GetString(Resource.String.ch_key_clear_faults), ConfirmClearFaults, Resource.Drawable.ic_delete);
        SetKey(5, GetString(Resource.String.ch_key_live), StatusMenu, Resource.Drawable.ic_live);
        SetKey(6, GetString(Resource.String.ch_key_actions), SteuernMenu, Resource.Drawable.ic_bolt);
        SetKey(7, GetString(Resource.String.ch_key_all_jobs), OpenAllJobs, Resource.Drawable.ic_list);
        SetKey(8, GetString(Resource.String.ch_key_chart), ChartMenu, Resource.Drawable.ic_chart);
        SetKey(9, GetString(Resource.String.cd_key_coding), () => CodingActivity.Start(this, _chassis.Name, _ecu.Title, _sgbd ?? _ecu.Sgbd), Resource.Drawable.ic_tune);
        SetKey(10, GetString(Resource.String.ch_key_back), Finish);
        RefreshKeys();

        _heading.Text = GetString(Resource.String.ecu_choose_function);
        _results.ShowInfo($"{_ecu.Title} ({_ecu.Sgbd})", GetString(Resource.String.ecu_choose_hint)!);
        Exclusive(async () => { await EnsureSgbdAsync(notify: false); });
    }

    void UpdateSubtitle() => SetSubtitle($"{_ecu.Sgbd} → {_sgbd ?? "?"}");

    string? StatusError(JobResult r) => DiagLogic.StatusError(r, GetString(Resource.String.g_rp_job_failed)!);

    // Botón Reintentar del estado vacío: limpia el aviso y vuelve a identificar; si falla, EnsureSgbdAsync lo muestra de nuevo.
    void RetryVariant() =>
        Exclusive(async () =>
        {
            ShowResults();
            _results.ShowInfo($"{_ecu.Title} ({_ecu.Sgbd})", GetString(Resource.String.ecu_choose_hint)!);
            await EnsureSgbdAsync();
        });

    protected override void OnVariantFailed(bool notify)
    {
        ShowResults();
        bool enet = DiagHolder.LoadSettings(this).Type == AdapterType.Enet;
        var message = GetString(enet ? Resource.String.fx_variant_failed_enet : Resource.String.ch_variant_failed)!;
        _results.ShowError($"{_ecu.Title} ({_ecu.Sgbd})", message);
        if (notify)
            UiUtil.Toast(this, message);
        _results.AddActions(
            (GetString(Resource.String.ecu_retry)!, RetryVariant),
            (GetString(Resource.String.ecu_open_settings)!, () => StartActivity(new Intent(this, typeof(SettingsActivity)))));
    }

    protected override void OnVariantFound()
    {
        UpdateSubtitle();
        _results.ShowInfo(_ecu.Sgbd, UiUtil.Str(this, Resource.String.ch_variant_ok, _sgbd), append: true);
    }

    // ---------------------------------------------------------------- ejecución de jobs

    void RunOnce(string job, string heading, string args = "") =>
        Exclusive(async () =>
        {
            var sgbd = await EnsureSgbdAsync();
            if (sgbd != null)
                await RunJobCore(sgbd, job, heading, args);
        });

    protected override void BeforeJob() => ShowResults();

    protected override void OnJobResult(JobResult r)
    {
        if (r.Ok && StatusError(r) is string err)
            _results.ShowError(GetString(Resource.String.ch_ecu_no_okay)!, err, append: true);
    }

    void ShowInfo()
    {
        var text = UiUtil.Str(this, Resource.String.ch_info_ecu, _ecu.Title) + "\n" +
                   $"SGBD       : {_ecu.Sgbd}\n" +
                   UiUtil.Str(this, Resource.String.ch_info_variant, _sgbd ?? GetString(Resource.String.ch_info_unidentified)!) + "\n\n" +
                   $"Ident      : {_ecu.IdentJob}\n" +
                   UiUtil.Str(this, Resource.String.ch_info_faults, _ecu.FsReadJob, _ecu.FsClearJob) + "\n\n" +
                   $"Status ({_ecu.StatusPages.Count}):\n" +
                   string.Concat(_ecu.StatusPages.Select(p => UiUtil.Str(this, Resource.String.ch_info_page, p.Title, p.Values.Count.ToString()) + "\n")) +
                   $"\nSteuern ({_ecu.Actions.Count}):\n" +
                   string.Concat(_ecu.Actions.Select(a => $"  • {a.Title}  [{a.Job}]\n"));
        JobDialogs.Message(this, GetString(Resource.String.ch_key_info)!, text);
    }

    // Memoria de errores (ResultPanel destaca F_ORT_NR / F_ORT_TEXT): el encabezado muestra cuántos hay.
    protected override void OnFaultsRead(JobResult r)
    {
        int faults = r.Sets.Count(FaultInfo.IsFault);
        _heading.Text = faults == 0 ? GetString(Resource.String.ch_faults_none) : UiUtil.Str(this, Resource.String.ch_faults_count, faults.ToString());
    }

    void PickItem(string title, IReadOnlyList<string> items, Action<int> onPick)
    {
        if (items.Count == 1)
        {
            onPick(0);
            return;
        }
        // Filas altas (>= 48dp) para tocar con el dedo.
        var adapter = new TwoLineAdapter(this, items.Select((t, i) => new TwoLineAdapter.Row(t, "", null, default, i)));
        new AlertDialog.Builder(this)
            .SetTitle(title)!
            .SetAdapter(adapter, (_, e) => onPick(e.Which))!
            .SetNegativeButton(Resource.String.ecu_pick_cancel, (_, _) => { })!
            .Show();
    }

    void StatusMenu()
    {
        if (_ecu.StatusPages.Count == 0)
        {
            ShowResults();
            _results.ShowInfo("Status", UiUtil.Str(this, Resource.String.ch_status_none, _chassis.Name));
            return;
        }
        PickItem(UiUtil.Str(this, Resource.String.ch_status_pick, _ecu.Title), _ecu.StatusPages.Select(p => p.Title).ToList(), i =>
            Exclusive(async () =>
            {
                var sgbd = await EnsureSgbdAsync();
                if (sgbd != null)
                    StartPolling(sgbd, _ecu.StatusPages[i]);
            }));
    }

    void ChartMenu()
    {
        if (_ecu.StatusPages.Count == 0)
        {
            ShowResults();
            _results.ShowInfo(GetString(Resource.String.ch_key_chart)!, UiUtil.Str(this, Resource.String.ch_chart_none, _chassis.Name));
            return;
        }
        PickItem(UiUtil.Str(this, Resource.String.ch_chart_pick, _ecu.Title), _ecu.StatusPages.Select(p => p.Title).ToList(), i =>
            Exclusive(async () =>
            {
                if (_ecu.StatusPages[i].Values.Count == 0)
                {
                    ShowResults();
                    _results.ShowInfo(GetString(Resource.String.ch_key_chart)!, GetString(Resource.String.ch_chart_no_values)!);
                    return;
                }
                var sgbd = await EnsureSgbdAsync();
                if (sgbd == null)
                    return;
                // El monitor repite un único job: se grafican los valores de la página que lo comparten.
                // Misma agrupación que PollLoop (job sin espacios ni mayúsculas/minúsculas, mismos argumentos).
                var first = _ecu.StatusPages[i].Values[0];
                string job = first.Job.Trim();
                var names = _ecu.StatusPages[i].Values
                    .Where(v => v.Job.Trim().Equals(job, StringComparison.OrdinalIgnoreCase) && v.Args == first.Args)
                    .Select(v => v.Result.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                StopPolling();
                StartActivity(LiveMonitorActivity.CreateIntent(this, _ecu.Title, sgbd, job, first.Args, names, _chassis.Id, Intent?.GetIntExtra(ExtraEcuIndex, -1) ?? -1));
            }));
    }

    void SteuernMenu()
    {
        if (_ecu.Actions.Count == 0)
        {
            ShowResults();
            _results.ShowInfo("Steuern", UiUtil.Str(this, Resource.String.ch_steuern_none, _chassis.Name));
            return;
        }
        PickItem(UiUtil.Str(this, Resource.String.ch_steuern_pick, _ecu.Title), _ecu.Actions.Select(a => a.Title).ToList(), i =>
        {
            var a = _ecu.Actions[i];
            var warning = a.Warning.Length > 0
                ? a.Warning
                : GetString(Resource.String.ch_steuern_warning)!;
            JobDialogs.ConfirmJob(this, "⚠ " + a.Title,
                warning + "\n\n" + GetString(Resource.String.ch_steuern_continue),
                _sgbd ?? _ecu.Sgbd, a.Job, a.Args,
                GetString(Resource.String.ch_run)!,
                onYes: () => Exclusive(async () =>
                {
                    var sgbd = await EnsureSgbdAsync();
                    if (sgbd != null)
                        await RunJobCore(sgbd, a.Job, "Steuern: " + a.Title, a.Args);
                }));
        });
    }

    void OpenAllJobs() =>
        Exclusive(async () =>
        {
            var sgbd = await EnsureSgbdAsync();
            if (sgbd == null)
                return;
            var intent = new Intent(this, typeof(EcuActivity));
            intent.PutExtra(EcuSelectActivity.ExtraEcuName, sgbd);
            intent.PutExtra(EcuSelectActivity.ExtraIsGroup, false);
            intent.PutExtra(EcuActivity.ExtraChassisName, _chassis.Name);
            StartActivity(intent);
        });

    // ---------------------------------------------------------------- Status continuo

    void ShowResults(bool results = true)
    {
        _results.Clear();
        _status.Visibility = results ? ViewStates.Gone : ViewStates.Visible;
    }

    void StartPolling(string sgbd, ChassisPage page)
    {
        StopPolling();
        int gen = ++_generation;
        _heading.Text = UiUtil.Str(this, Resource.String.ch_status_heading, page.Title);
        ShowResults(false);
        _status.Text = GetString(Resource.String.ch_reading);
        ShowBusy(GetString(Resource.String.ecu_polling_hint)!, GetString(Resource.String.ch_stop)!, StopPolling);
        // Un job por llamada (job + argumentos distintos), pidiendo solo los resultados necesarios.
        var groups = page.Values
            .GroupBy(v => (Job: v.Job.Trim().ToUpperInvariant(), v.Args))
            .Select(g => (g.Key.Job, g.Key.Args, Values: g.ToList(),
                Results: string.Join(";", g.Select(v => v.Result.Trim()).Append("JOB_STATUS")
                    .Distinct(StringComparer.OrdinalIgnoreCase))))
            .ToList();
        int count = 0;
        _poll.Start(async ct =>
        {
            var cells = new Dictionary<ChassisValue, (string Text, bool Error)>();
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
                    // Texto técnico tal cual: Friendly lo traduce abajo (Describe ya lo traduciría y saldría duplicado).
                    r = new JobResult { Ok = false, Error = ex.GetBaseException().Message };
                }
                finally
                {
                    _pollInFlight = false;
                }
                if (ct.IsCancellationRequested || gen != _generation)
                    return false;
                var error = StatusError(r) is string raw ? UiUtil.Friendly(this, raw) : null;
                foreach (var v in g.Values)
                {
                    cells[v] = error != null ? ("ERROR " + error, true)
                        : DiagLogic.FindResult(r, v.Result) is string text ? (text, false)
                        : (UiUtil.Str(this, Resource.String.ch_no_result, v.Result), true);
                }
            }
            _status.TextFormatted = StatusTable.BuildTable(page, cells, ++count);
            return true;
        }, ex => _status.Text = UiUtil.Describe(ex), StatusPollMs, 100);
    }
}
