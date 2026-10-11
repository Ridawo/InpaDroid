using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

[Activity(Label = "@string/g_ecu_label", Theme = "@style/InpaTheme",
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

        SetKey(1, GetString(Resource.String.g_key_info)!, () => RunOnce("INFO", GetString(Resource.String.g_head_sgbd_info)!), Resource.Drawable.ic_info);
        SetKey(2, GetString(Resource.String.g_key_ident)!, () => RunOnce("IDENT", GetString(Resource.String.g_key_ident)!), Resource.Drawable.ic_ident);
        SetKey(4, GetString(Resource.String.g_key_faults)!, () => RunOnce("FS_LESEN", GetString(Resource.String.g_head_fault_memory)!), Resource.Drawable.ic_fault);
        SetShiftKey(4, GetString(Resource.String.g_key_clear_faults)!, ConfirmClearFaults, Resource.Drawable.ic_delete);
        SetKey(5, GetString(Resource.String.g_key_live)!, StatusMenu, Resource.Drawable.ic_live);
        SetKey(6, GetString(Resource.String.g_key_actuate)!, SteuernMenu, Resource.Drawable.ic_bolt);
        SetKey(7, GetString(Resource.String.g_key_all_jobs)!, JobsMenu, Resource.Drawable.ic_list);
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
        JobDialogs.Confirm(this, GetString(Resource.String.g_clear_title)!,
            UiUtil.Str(this, Resource.String.g_clear_msg, name),
            GetString(Resource.String.g_clear_yes)!,
            danger: true,
            onYes: () => Exclusive(async () =>
            {
                var sgbd = await EnsureSgbdAsync();
                if (sgbd == null)
                    return;
                await RunJobCore(sgbd, "FS_LOESCHEN", GetString(Resource.String.g_clear_title)!, "", "");
                _results.ShowInfo(GetString(Resource.String.g_head_fault_memory)!, GetString(Resource.String.ecu_reread_faults)!, append: true);
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
                _results.ShowInfo(title, UiUtil.Str(this, Resource.String.g_no_jobs_prefix, sgbd, prefix ?? ""));
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
                JobDialogs.AskArguments(this, sgbd, job, GetString(Resource.String.g_read)!, (args, res) => StartPolling(sgbd, job.Name, args, res));
        });

    void SteuernMenu() =>
        PickJob("STEUERN", "Steuern", (sgbd, job) =>
            JobDialogs.AskArguments(this, sgbd, job, GetString(Resource.String.g_continue)!, (args, res) =>
                JobDialogs.Confirm(this, GetString(Resource.String.g_steuern_title)!,
                    UiUtil.Str(this, Resource.String.g_steuern_intro, job.Name, sgbd,
                        args.Length > 0 ? " " + UiUtil.Str(this, Resource.String.g_steuern_args, args) : "") +
                    GetString(Resource.String.g_steuern_warning),
                    GetString(Resource.String.g_run)!,
                    danger: true,
                    onYes: () => Exclusive(() => RunJobCore(sgbd, job.Name, "Steuern: " + job.Name, args, res)))));

    void JobsMenu() =>
        PickJob(null, "Jobs", (sgbd, job) =>
            JobDialogs.AskArguments(this, sgbd, job, GetString(Resource.String.g_run)!, (args, res) =>
                Exclusive(() => RunJobCore(sgbd, job.Name, "Job: " + job.Name, args, res))));

    void StartPolling(string sgbd, string job, string args, string results)
    {
        StopPolling();
        int gen = ++_generation;
        int count = 0;
        _heading.Text = "Status: " + job;
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
