using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui.Chassis;

/// <summary>Escaneo rápido: lee la memoria de errores de todas las centralitas del catálogo, una tras otra, y muestra un resumen.</summary>
[Activity(Label = "@string/sc_key_scan", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
                           | ConfigChanges.UiMode | ConfigChanges.Locale)]
public class ChassisScanActivity : InpaActivity
{
    ChassisInfo _chassis = null!;
    TextView _heading = null!;
    ListView _list = null!;
    ScanEntry[] _entries = [];
    bool _scanning;
    bool _cancelled;
    int _done;                              // centralitas ya leídas (también tras cancelar)

    protected override bool PauseHeader => _scanning;

    public static void Start(Context ctx, string chassisId)
    {
        var intent = new Intent(ctx, typeof(ChassisScanActivity));
        intent.PutExtra(ChassisEcuActivity.ExtraChassisId, chassisId);
        ctx.StartActivity(intent);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Recreada tras matar el proceso: los índices de centralita se refieren al catálogo con el override de la carpeta ECU.
        DiagHolder.LoadCatalogs(this, DiagHolder.LoadSettings(this).EcuPath);
        _chassis = ChassisCatalog.Get(Intent?.GetStringExtra(ChassisEcuActivity.ExtraChassisId) ?? "");
        InitInpa(Resource.Layout.chassis_scan, _chassis.Name);
        SetSubtitle(GetString(Resource.String.sc_key_scan)!);
        _heading = FindViewById<TextView>(Resource.Id.chassis_scan_heading)!;
        _list = FindViewById<ListView>(Resource.Id.chassis_scan_list)!;
        _list.ItemClick += (_, e) =>
        {
            if (!_scanning)   // abrir una centralita mientras se escanea mezclaría sus jobs con los del escaneo en la cola
                OpenEcu(e.Position);
        };

        _entries = _chassis.Ecus.Select(e => new ScanEntry(e.Title, e.Sgbd, "", ScanState.Pending)).ToArray();
        _heading.Text = UiUtil.Str(this, Resource.String.sc_heading_start, _entries.Length.ToString());
        ShowRows();

        SetKey(1, GetString(Resource.String.sc_key_scan), StartScan, Resource.Drawable.ic_fault);
        SetKey(2, GetString(Resource.String.sc_key_export), Export, Resource.Drawable.ic_list);
        SetKey(10, GetString(Resource.String.ch_key_back), Back);
        RefreshKeys();
    }

    protected override bool BeforeKey(int f, bool shift)
    {
        if (_scanning && f != 10)
            return false;
        return true;
    }

    void Back()
    {
        if (_scanning)
            CancelScan();
        Finish();
    }

    protected override void OnDestroy()
    {
        if (_scanning)
            CancelScan();
        base.OnDestroy();
    }

    void CancelScan()
    {
        _cancelled = true;
        AbortJob();
    }

    void OpenEcu(int index)
    {
        var intent = new Intent(this, typeof(ChassisEcuActivity));
        intent.PutExtra(ChassisEcuActivity.ExtraChassisId, _chassis.Id);
        intent.PutExtra(ChassisEcuActivity.ExtraEcuIndex, index);
        StartActivity(intent);
    }

    // Detail guarda el texto técnico (es lo que va al CSV); en pantalla se muestra traducido.
    void ShowRows()
    {
        _list.Adapter = new TwoLineAdapter(this, _entries.Select((e, i) => new TwoLineAdapter.Row(
            e.Title, StateText(e) + (e.Detail.Length > 0 ? " - " + UiUtil.Friendly(this, e.Detail) : ""), BadgeText(e), BadgeColor(e.State), i)));
    }

    string StateText(ScanEntry e) => e.State switch
    {
        ScanState.Ok => GetString(Resource.String.sc_state_ok)!,
        ScanState.Faults => UiUtil.Str(this, Resource.String.sc_state_faults, e.Faults.ToString()),
        ScanState.NoResponse => GetString(Resource.String.sc_state_no_response)!,
        ScanState.NotFound => GetString(Resource.String.sc_state_not_found)!,
        _ => GetString(Resource.String.sc_state_pending)!,
    };

    static string BadgeText(ScanEntry e) => e.State switch
    {
        ScanState.Ok => "OK",
        ScanState.Faults => e.Faults.ToString(),
        ScanState.NoResponse => "--",
        ScanState.NotFound => "?",
        _ => "",
    };

    static Android.Graphics.Color BadgeColor(ScanState s) => s switch
    {
        ScanState.Ok => UiUtil.OkText,
        ScanState.Faults => UiUtil.ErrorText,
        ScanState.NoResponse or ScanState.NotFound => UiUtil.WarnText,
        _ => UiUtil.LedOff,
    };

    async void StartScan()
    {
        if (_entries.Length == 0)
            return;
        _scanning = true;
        _cancelled = false;
        _done = 0;
        _entries = _entries.Select(e => e with { Variant = "", State = ScanState.Pending, Faults = 0, Detail = "" }).ToArray();
        ShowRows();
        try
        {
            for (int i = 0; i < _entries.Length && !_cancelled; i++)
            {
                var ecu = _chassis.Ecus[i];
                _heading.Text = UiUtil.Str(this, Resource.String.sc_heading_progress, (i + 1).ToString(), _entries.Length.ToString());
                ShowBusy(UiUtil.Str(this, Resource.String.sc_busy, ecu.Title, (i + 1).ToString(), _entries.Length.ToString()),
                    GetString(Resource.String.g_cancel), CancelScan);
                var entry = await ScanEcuAsync(ecu);
                // Un job abortado por Cancelar devuelve error: ese resultado no es de la centralita y se descarta.
                if (_cancelled || IsDestroyed)
                    break;
                _entries[i] = entry;
                _done = i + 1;
                ShowRows();
            }
        }
        finally
        {
            _scanning = false;
            if (!IsDestroyed)
            {
                HideBusy();
                int withFaults = _entries.Count(e => e.State == ScanState.Faults);
                int unread = _entries.Count(e => e.State is ScanState.NoResponse or ScanState.NotFound);
                _heading.Text = _cancelled
                    ? UiUtil.Str(this, Resource.String.sc_heading_cancelled, _done.ToString(), _entries.Length.ToString())
                    : unread > 0
                        ? UiUtil.Str(this, Resource.String.sc_heading_partial, withFaults.ToString(), _entries.Length.ToString(), unread.ToString())
                        : UiUtil.Str(this, Resource.String.sc_heading_done, withFaults.ToString(), _entries.Length.ToString());
            }
        }
    }

    // Misma resolución de variante (.grp -> .prg) y lectura que ChassisEcuActivity, sin tocar EdiabasNet: todo va por la cola de DiagService.
    async Task<ScanEntry> ScanEcuAsync(ChassisEcu ecu)
    {
        try
        {
            var variant = await Diag.ResolveVariantAsync(ecu.Sgbd);
            if (string.IsNullOrWhiteSpace(variant))
            {
                var files = Diag.ScanEcuFolder().Select(f => f.Name);
                return new ScanEntry(ecu.Title, ecu.Sgbd, "", ScanSummary.NoVariantState(ecu.Sgbd, files));
            }
            var r = await Diag.RunJobAsync(variant, ecu.FsReadJob);
            var (state, faults, detail) = ScanSummary.Classify(r, GetString(Resource.String.g_rp_job_failed)!);
            return new ScanEntry(ecu.Title, ecu.Sgbd, variant, state, faults, detail);
        }
        catch (Exception ex)
        {
            return new ScanEntry(ecu.Title, ecu.Sgbd, "", ScanState.NoResponse, 0, ex.GetBaseException().Message);
        }
    }

    void Export()
    {
        if (_done == 0)
        {
            UiUtil.Toast(this, GetString(Resource.String.sc_export_none)!);
            return;
        }
        ResultExporter.ShareCsv(this, ScanSummary.ToCsv(_entries, ResultExporter.Cell), GetString(Resource.String.sc_export_title)!);
    }
}
