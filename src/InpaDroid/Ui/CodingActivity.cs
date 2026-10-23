using System.Globalization;
using System.Text;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

/// <summary>
/// Copia de la codificación de una centralita: lee los jobs de codificación / orden de vehículo, guarda cada lectura,
/// la comparte en CSV y la compara con la anterior. Solo lectura: aquí no se escribe ni se restaura nada.
/// </summary>
[Activity(Label = "@string/cd_title", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
                           | ConfigChanges.UiMode | ConfigChanges.Locale)]
public class CodingActivity : EcuScreenActivity
{
    const string ExtraChassis = "cd_chassis", ExtraEcuTitle = "cd_ecu_title", ExtraSgbd = "cd_sgbd";

    string _chassis = "", _ecuTitle = "", _ecuName = "";
    CodingBackup.Snapshot? _current;        // copia mostrada: la recién leída o la abierta de la lista

    protected override string EcuSgbd => _ecuName;

    protected override string VariantBusyText => UiUtil.Str(this, Resource.String.g_variant_resolving, _ecuName);

    protected override void OnVariantFailed(bool notify)
    {
        var message = GetString(Resource.String.g_variant_failed)!;
        _results.ShowError(_ecuName, message);
        if (notify)
            UiUtil.Toast(this, message);
    }

    public static void Start(Context ctx, string chassis, string ecuTitle, string sgbd)
    {
        var intent = new Intent(ctx, typeof(CodingActivity));
        intent.PutExtra(ExtraChassis, chassis);
        intent.PutExtra(ExtraEcuTitle, ecuTitle);
        intent.PutExtra(ExtraSgbd, sgbd);
        ctx.StartActivity(intent);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _chassis = Intent?.GetStringExtra(ExtraChassis) ?? "";
        _ecuTitle = Intent?.GetStringExtra(ExtraEcuTitle) ?? "";
        _ecuName = Intent?.GetStringExtra(ExtraSgbd) ?? "";

        InitInpa(Resource.Layout.coding_activity, GetString(Resource.String.cd_title)!);
        _heading = FindViewById<TextView>(Resource.Id.coding_heading)!;
        _results = new ResultPanel(FindViewById<LinearLayout>(Resource.Id.coding_results)!);
        SetSubtitle(_ecuTitle.Length > 0 ? $"{_ecuTitle} ({_ecuName})" : _ecuName);

        SetKey(1, GetString(Resource.String.cd_key_read)!, ReadCoding, Resource.Drawable.ic_tune);
        SetKey(2, GetString(Resource.String.cd_key_share)!, ShareCsv, Resource.Drawable.ic_copy);
        SetKey(3, GetString(Resource.String.cd_key_compare)!, Compare, Resource.Drawable.ic_list);
        SetKey(4, GetString(Resource.String.cd_key_snapshots)!, Snapshots, Resource.Drawable.ic_info);
        SetKey(10, GetString(Resource.String.g_key_back)!, Finish);
        RefreshKeys();

        _heading.Text = GetString(Resource.String.cd_title);
        _results.ShowInfo(GetString(Resource.String.cd_safety_title)!, GetString(Resource.String.cd_safety_msg)!);
    }

    // Solo se ofrecen los jobs que CodingBackup reconoce como lectura (sin marca de escritura).
    void ReadCoding() =>
        Exclusive(async () =>
        {
            var sgbd = await EnsureSgbdAsync();
            if (sgbd == null)
                return;
            var jobs = await EnsureJobsAsync(sgbd);
            if (jobs == null)
                return;
            var list = jobs.Where(j => CodingBackup.IsCodingReadJob(j.Name)).ToList();
            if (list.Count == 0)
            {
                _results.ShowInfo(GetString(Resource.String.cd_key_read)!, UiUtil.Str(this, Resource.String.cd_no_jobs, sgbd));
                return;
            }
            JobDialogs.PickJob(this, $"{GetString(Resource.String.cd_key_read)} - {sgbd} ({list.Count})", list, job =>
            {
                if (job.Arguments.Count == 0)
                    RunRead(sgbd, job.Name, "", "");
                else
                    JobDialogs.AskArguments(this, sgbd, job, GetString(Resource.String.g_read)!, (args, res) => RunRead(sgbd, job.Name, args, res));
            });
        });

    // Ejecuta la lectura por la cola normal y, si termina bien, guarda la copia.
    void RunRead(string sgbd, string job, string args, string results) =>
        Exclusive(async () =>
        {
            var r = await RunJobCore(sgbd, job, UiUtil.Str(this, Resource.String.sf_job_heading, job), args, results);
            if (r == null)
                return;
            if (DiagLogic.StatusError(r, "") != null)
            {
                _results.ShowError(GetString(Resource.String.cd_title)!, GetString(Resource.String.cd_not_saved)!, append: true);
                return;
            }
            var snap = CodingBackup.FromResult(DateTime.UtcNow, _chassis, _ecuTitle, sgbd, job, r, args);
            _current = snap;
            _results.ShowInfo(GetString(Resource.String.cd_title)!, CodingStore.Save(this, snap)
                ? UiUtil.Str(this, Resource.String.cd_saved, snap.Entries.Count)
                : GetString(Resource.String.cd_save_failed)!, append: true);
        });

    void ShareCsv()
    {
        if (_current is not { } s)
        {
            UiUtil.Toast(this, GetString(Resource.String.cd_none)!);
            return;
        }
        var prefix = string.Join(",", new[] { s.When, s.Chassis, s.EcuTitle, s.Sgbd, s.Job, s.Args }.Select(ResultExporter.Cell));
        var sb = new StringBuilder("timestamp_utc,chassis,ecu,sgbd,job,args,key,value\r\n");
        foreach (var e in s.Entries)
            sb.Append(prefix).Append(',').Append(ResultExporter.Cell(e.Key)).Append(',').Append(ResultExporter.Cell(e.Value)).Append("\r\n");
        ResultExporter.ShareCsv(this, sb.ToString(), GetString(Resource.String.cd_share_title)!);
    }

    // Compara la copia mostrada con la anterior de la misma SGBD y job.
    void Compare()
    {
        if (_current is not { } cur)
        {
            UiUtil.Toast(this, GetString(Resource.String.cd_none)!);
            return;
        }
        Exclusive(async () =>
        {
            var ctx = ApplicationContext!;
            var prev = CodingBackup.Previous(await Task.Run(() => CodingStore.Load(ctx)), cur);
            if (prev == null)
            {
                _results.ShowInfo(GetString(Resource.String.cd_key_compare)!, UiUtil.Str(this, Resource.String.cd_no_previous, cur.Sgbd, cur.Job));
                return;
            }
            var d = CodingBackup.Diff(prev, cur);
            var sb = new StringBuilder();
            Section(sb, Resource.String.cd_cmp_changed, d.Changed.Select(c => $"{c.Key} : {c.Before} → {c.After}").ToList());
            Section(sb, Resource.String.cd_cmp_added, d.Added.Select(Line).ToList());
            Section(sb, Resource.String.cd_cmp_removed, d.Removed.Select(Line).ToList());
            _heading.Text = $"{Label(cur)} · {cur.Job}";
            _results.ShowInfo(UiUtil.Str(this, Resource.String.cd_compare_head, FormatWhen(prev)), sb.ToString().TrimEnd());
        });
    }

    void Section(StringBuilder sb, int titleRes, IReadOnlyList<string> lines)
    {
        sb.Append(UiUtil.Str(this, titleRes, lines.Count)).Append('\n');
        if (lines.Count == 0)
            sb.Append(GetString(Resource.String.cd_cmp_none)).Append('\n');
        foreach (var l in lines)
            sb.Append(l).Append('\n');
        sb.Append('\n');
    }

    void Snapshots() =>
        Exclusive(async () =>
        {
            var ctx = ApplicationContext!;
            var all = await Task.Run(() => CodingStore.Load(ctx));
            if (all.Count == 0)
            {
                _results.ShowInfo(GetString(Resource.String.cd_key_snapshots)!, GetString(Resource.String.cd_snapshots_empty)!);
                return;
            }
            var adapter = new TwoLineAdapter(this, all.Select(s => new TwoLineAdapter.Row(
                Label(s), UiUtil.Str(this, Resource.String.cd_snapshot_sub, FormatWhen(s), JobLabel(s), s.Entries.Count), null, default, s)));
            new AlertDialog.Builder(this)
                .SetTitle(GetString(Resource.String.cd_key_snapshots))!
                .SetAdapter(adapter, (_, e) => ShowSnapshot((CodingBackup.Snapshot)adapter.GetRow(e.Which).Tag))!
                .SetNegativeButton(Resource.String.g_cancel, (_, _) => { })!
                .Show();
        });

    void ShowSnapshot(CodingBackup.Snapshot s)
    {
        _current = s;
        _heading.Text = $"{Label(s)} · {s.Job}";
        _results.ShowInfo($"{s.Sgbd} : {s.Job}   {FormatWhen(s)}",
            s.Entries.Count == 0 ? GetString(Resource.String.g_rp_no_results)! : string.Join("\n", s.Entries.Select(Line)));
    }

    static string Line(CodingBackup.Entry e) => $"{e.Key} : {e.Value}";

    static string JobLabel(CodingBackup.Snapshot s) => s.Args.Length > 0 ? $"{s.Job} {s.Args}" : s.Job;

    static string Label(CodingBackup.Snapshot s) => s.EcuTitle.Length > 0 ? s.EcuTitle : s.Sgbd;

    static string FormatWhen(CodingBackup.Snapshot s) =>
        DateTime.TryParse(s.When, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
            ? t.ToLocalTime().ToString("g") : s.When;
}
