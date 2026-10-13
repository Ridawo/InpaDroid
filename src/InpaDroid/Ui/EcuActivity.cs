using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

[Activity(Label = "@string/g_ecu_label", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
                           | ConfigChanges.UiMode | ConfigChanges.Locale)]
public class EcuActivity : InpaActivity
{
    public const string ExtraChassisName = "chassis_name";
    const int StatusPollMs = 1000;

    string _ecuName = "";
    string _chassis = "";                   // chasis de origen (para el historial y el informe); vacío desde la lista de SGBD
    bool _isGroup;
    string? _sgbd;                          // variante real (.prg) una vez resuelta
    IReadOnlyList<JobInfo>? _jobs;
    ResultPanel _results = null!;
    TextView _heading = null!;
    bool _busyJob;                          // job puntual en curso (bloquea otras teclas salvo F10)
    bool _abortRequested;                   // el usuario ha abortado la operación en curso (Exclusive lo limpia)
    int _generation;                        // descarta resultados de operaciones ya sustituidas
    readonly PollRunner _poll = new();

    protected override bool PauseHeader => _busyJob || _poll.Running;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _ecuName = Intent?.GetStringExtra(EcuSelectActivity.ExtraEcuName) ?? "";
        _isGroup = Intent?.GetBooleanExtra(EcuSelectActivity.ExtraIsGroup, false) ?? false;
        _chassis = Intent?.GetStringExtra(ExtraChassisName) ?? "";
        if (!_isGroup)
            _sgbd = _ecuName;

        InitInpa(Resource.Layout.activity_ecu, _ecuName);
        _heading = FindViewById<TextView>(Resource.Id.ecu_heading)!;
        _results = new ResultPanel(FindViewById<LinearLayout>(Resource.Id.ecu_results)!);
        UpdateSubtitle();

        SetKey(1, GetString(Resource.String.g_key_info)!, () => RunOnce("INFO", GetString(Resource.String.g_head_sgbd_info)!), Resource.Drawable.ic_info);
        SetKey(2, GetString(Resource.String.g_key_ident)!, () => RunOnce("IDENT", GetString(Resource.String.g_key_ident)!), Resource.Drawable.ic_ident);
        SetKey(3, GetString(Resource.String.fb_key_report)!, ShareReport, Resource.Drawable.ic_copy);
        SetKey(4, GetString(Resource.String.g_key_faults)!, () => ReadFaults(), Resource.Drawable.ic_fault);
        SetShiftKey(4, GetString(Resource.String.g_key_clear_faults)!, ConfirmClearFaults, Resource.Drawable.ic_delete);
        SetKey(5, GetString(Resource.String.g_key_live)!, StatusMenu, Resource.Drawable.ic_live);
        SetKey(6, GetString(Resource.String.g_key_actuate)!, SteuernMenu, Resource.Drawable.ic_bolt);
        SetKey(7, GetString(Resource.String.g_key_all_jobs)!, JobsMenu, Resource.Drawable.ic_list);
        SetKey(8, GetString(Resource.String.cd_key_coding)!, () => CodingActivity.Start(this, _chassis, _ecuName, _sgbd ?? _ecuName), Resource.Drawable.ic_tune);
        SetKey(9, GetString(Resource.String.g_key_copy)!, CopyResults, Resource.Drawable.ic_copy);
        SetKey(10, GetString(Resource.String.g_key_back)!, Back);
        RefreshKeys();

        _heading.Text = GetString(Resource.String.ecu_choose_function);
        _results.ShowInfo(_isGroup ? UiUtil.Str(this, Resource.String.g_group_title, _ecuName) : $"SGBD {_ecuName}", GetString(Resource.String.ecu_choose_hint)!);
        if (_isGroup)
            Exclusive(async () => { await EnsureSgbdAsync(); });
    }

    void UpdateSubtitle() =>
        SetSubtitle(_isGroup ? $"{_ecuName}.grp → {(_sgbd ?? "?")}" : $"{_ecuName}.prg");

    protected override bool BeforeKey(int f, bool shift)
    {
        bool isBack = f == 10 && !shift;
        if (_busyJob && !isBack)
        {
            UiUtil.Toast(this, GetString(Resource.String.ecu_busy_job)!);
            return false;
        }
        // Cualquier acción detiene la lectura continua de Status.
        StopPolling();
        return true;
    }

    void Back()
    {
        if (_busyJob)
            AbortFlow();
        Finish();
    }

    // Abortar de la barra de trabajo: además de cancelar el job, corta la operación (p. ej. el borrado tras la copia).
    void AbortFlow()
    {
        _abortRequested = true;
        AbortJob();
    }

    protected override void OnPause()
    {
        StopPolling();
        base.OnPause();
    }

    // Atrás del sistema o cierre sin pasar por Back(): el job en curso no debe seguir ni encadenar otro.
    protected override void OnDestroy()
    {
        if (_busyJob)
            AbortFlow();
        base.OnDestroy();
    }

    async void Exclusive(Func<Task> work)
    {
        if (_busyJob)
            return;
        _busyJob = true;
        _abortRequested = false;
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
        ShowBusy(UiUtil.Str(this, Resource.String.g_variant_resolving, _ecuName));
        var variant = await Diag.ResolveVariantAsync(_ecuName);
        HideBusy();
        if (string.IsNullOrWhiteSpace(variant))
        {
            _results.ShowError(UiUtil.Str(this, Resource.String.g_group_title, _ecuName),
                GetString(Resource.String.g_variant_failed) + " " + GetString(Resource.String.ecu_retry_hint));
            return null;
        }
        _sgbd = variant;
        UpdateSubtitle();
        _results.ShowInfo(UiUtil.Str(this, Resource.String.g_group_title, _ecuName),
            UiUtil.Str(this, Resource.String.g_variant_found, _sgbd), append: true);
        return _sgbd;
    }

    async Task<IReadOnlyList<JobInfo>?> EnsureJobsAsync(string sgbd)
    {
        if (_jobs is { Count: > 0 })
            return _jobs;
        ShowBusy(UiUtil.Str(this, Resource.String.g_jobs_reading, sgbd));
        var jobs = await Diag.GetJobsAsync(sgbd);
        HideBusy();
        if (jobs.Count == 0)
        {
            _results.ShowError(sgbd, UiUtil.Str(this, Resource.String.g_jobs_failed, sgbd));
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

    async Task<JobResult?> RunJobCore(string sgbd, string job, string heading, string args, string results)
    {
        int gen = ++_generation;
        _heading.Text = heading;
        ShowBusy($"{sgbd} : {job}…", null, AbortFlow);
        try
        {
            var r = await Diag.RunJobAsync(sgbd, job, args, results);
            if (gen != _generation)
                return null;
            _results.ShowResult(sgbd, job, r);
            return r;
        }
        catch (Exception ex)
        {
            if (gen == _generation)
                _results.ShowException(sgbd, job, ex);
            return null;
        }
    }

    void ConfirmClearFaults()
    {
        var name = _sgbd ?? _ecuName;
        JobDialogs.ConfirmJob(this, GetString(Resource.String.g_clear_title)!,
            UiUtil.Str(this, Resource.String.g_clear_msg, name) + "\n\n" + GetString(Resource.String.fb_clear_hint),
            name, "FS_LOESCHEN", "",
            GetString(Resource.String.g_clear_yes)!,
            onYes: () => Exclusive(async () =>
            {
                var sgbd = await EnsureSgbdAsync();
                if (sgbd == null)
                    return;
                // Copia de seguridad automática: la memoria de errores se lee y se guarda en el historial antes de borrarla.
                var backup = await ReadFaultsCore(sgbd, HistoryStore.KindBackup);
                // Abortado o pantalla cerrada (atrás del sistema, Abortar justo al acabar la lectura): no se borra nada.
                if (_abortRequested || IsFinishing || IsDestroyed)
                    return;
                if (backup != null)
                    await ClearFaultsCore(sgbd, GetString(Resource.String.fb_backup_saved)!);
                else
                    JobDialogs.Confirm(this, GetString(Resource.String.fb_backup_failed_title)!, GetString(Resource.String.fb_backup_failed_msg)!,
                        GetString(Resource.String.fb_clear_anyway)!,
                        () => Exclusive(() => ClearFaultsCore(sgbd, GetString(Resource.String.fb_backup_none)!)));
            }),
            extra: (GetString(Resource.String.sf_save_faults)!, SaveFaults));
    }

    async Task ClearFaultsCore(string sgbd, string backupStatus)
    {
        await RunJobCore(sgbd, "FS_LOESCHEN", GetString(Resource.String.g_clear_title)!, "", "");
        _results.ShowInfo(GetString(Resource.String.g_head_fault_memory)!,
            backupStatus + "\n" + GetString(Resource.String.ecu_reread_faults), append: true);
    }

    // Lee FS_LESEN por la cola y, si la respuesta es correcta, la guarda en el historial; null si falla.
    async Task<JobResult?> ReadFaultsCore(string sgbd, string kind)
    {
        var r = await RunJobCore(sgbd, "FS_LESEN", GetString(Resource.String.g_head_fault_memory)!, "", "");
        if (r == null || DiagLogic.StatusError(r, "") != null)
            return null;
        HistoryStore.Save(this, _chassis, _ecuName, sgbd, kind, r);
        return r;
    }

    // F4, «Guardar errores» e Informe: lee la memoria de errores y, si se ha leído bien, pasa la respuesta a then.
    void ReadFaults(Action<string, JobResult>? then = null) =>
        Exclusive(async () =>
        {
            var sgbd = await EnsureSgbdAsync();
            if (sgbd != null && await ReadFaultsCore(sgbd, HistoryStore.KindRead) is { } r)
                then?.Invoke(sgbd, r);
        });

    // "Guardar errores" del aviso de borrado: lee la memoria de errores y la comparte en CSV.
    void SaveFaults() => ReadFaults((_, _) => _results.Share());

    void ShareReport() => ReadFaults((sgbd, r) => ReportExporter.Share(this, _chassis, _ecuName, sgbd, r));

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
                _results.ShowInfo(title, UiUtil.Str(this, Resource.String.g_no_jobs_prefix, sgbd, prefix ?? ""));
                return;
            }
            JobDialogs.PickJob(this, $"{title} - {sgbd} ({list.Count})", list, job => onPick(sgbd, job));
        });

    void StatusMenu() =>
        PickJob("STATUS_", "Status", (sgbd, job) =>
        {
            // Un STATUS_ que escribe o activa (p. ej. STATUS_VORGEBEN) no se repite en bucle: una vez y tras confirmar.
            bool readOnly = JobPolicy.IsReadOnly(job.Name);
            void Start(string args, string res)
            {
                if (readOnly)
                    StartPolling(sgbd, job.Name, args, res);
                else
                    RunChecked(sgbd, job.Name, args, res);
            }
            if (job.Arguments.Count == 0)
                Start("", "");
            else
                JobDialogs.AskArguments(this, sgbd, job, GetString(readOnly ? Resource.String.g_read : Resource.String.g_continue)!, Start);
        });

    void SteuernMenu() =>
        PickJob("STEUERN", "Steuern", (sgbd, job) =>
            JobDialogs.AskArguments(this, sgbd, job, GetString(Resource.String.g_continue)!, (args, res) =>
                JobDialogs.ConfirmJob(this, GetString(Resource.String.g_steuern_title)!,
                    GetString(Resource.String.g_steuern_warning)!, sgbd, job.Name, args,
                    GetString(Resource.String.g_run)!,
                    onYes: () => Exclusive(() => RunJobCore(sgbd, job.Name, "Steuern: " + job.Name, args, res)))));

    void JobsMenu() =>
        PickJob(null, GetString(Resource.String.sf_jobs_title)!, (sgbd, job) =>
            JobDialogs.AskArguments(this, sgbd, job, GetString(Resource.String.g_run)!, (args, res) => RunChecked(sgbd, job.Name, args, res)));

    // Los jobs de solo lectura se ejecutan directamente; el resto pide confirmación con SGBD, JOB y argumentos.
    void RunChecked(string sgbd, string job, string args, string results)
    {
        void Run() => Exclusive(() => RunJobCore(sgbd, job, UiUtil.Str(this, Resource.String.sf_job_heading, job), args, results));
        if (JobPolicy.IsReadOnly(job))
            Run();
        else
            JobDialogs.ConfirmJob(this, GetString(Resource.String.sf_write_title)!, GetString(Resource.String.sf_write_msg)!,
                sgbd, job, args, GetString(Resource.String.g_run)!, Run);
    }

    void StartPolling(string sgbd, string job, string args, string results)
    {
        StopPolling();
        int gen = ++_generation;
        int count = 0;
        _heading.Text = UiUtil.Str(this, Resource.String.sf_status_heading, job);
        _results.Clear();
        ShowBusy($"{job}: " + GetString(Resource.String.ecu_polling_hint), GetString(Resource.String.g_stop), StopPolling);
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
        _heading.Text += " " + GetString(Resource.String.g_stopped);
    }

    void CopyResults()
    {
        var text = _results.PlainText();
        if (text.Length == 0)
            return;
        if (GetSystemService(ClipboardService) is ClipboardManager clipboard)
        {
            clipboard.PrimaryClip = ClipData.NewPlainText("InpaDroid", text);
            UiUtil.Toast(this, GetString(Resource.String.g_results_copied)!);
        }
    }
}
