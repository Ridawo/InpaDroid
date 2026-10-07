using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui.Chassis;

/// <summary>Lista de centralitas de un chasis (id en el extra <see cref="ChassisEcuActivity.ExtraChassisId"/>), como la selección de SG de INPA.</summary>
[Activity(Label = "BMW", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class ChassisMenuActivity : InpaActivity
{

    ChassisInfo _chassis = null!;

    /// <summary>Abre el menú del chasis indicado (p.ej. "e39").</summary>
    public static void Start(Context ctx, string chassisId)
    {
        var intent = new Intent(ctx, typeof(ChassisMenuActivity));
        intent.PutExtra(ChassisEcuActivity.ExtraChassisId, chassisId);
        ctx.StartActivity(intent);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // La lista y ChassisEcuActivity deben ver el mismo catálogo (override de la carpeta ECU incluido), también
        // si Android recrea esta pantalla tras matar el proceso, antes de que DiagHolder lo haya cargado.
        ChassisCatalog.TryLoadFromFolder(DiagHolder.LoadSettings(this).EcuPath);
        _chassis = ChassisCatalog.Get(Intent?.GetStringExtra(ChassisEcuActivity.ExtraChassisId) ?? "");
        InitInpa(Resource.Layout.chassis_menu, _chassis.Name);
        SetSubtitle(_chassis.Subtitle);

        var heading = FindViewById<TextView>(Resource.Id.chassis_menu_heading)!;
        var list = FindViewById<ListView>(Resource.Id.chassis_menu_list)!;
        var ecus = _chassis.Ecus;
        heading.Text = ecus.Count == 0 ? $"Catálogo {_chassis.Name} vacío" : $"Selección de centralita ({ecus.Count})";
        if (!_chassis.Verified && ecus.Count > 0)
            heading.Text += " - sin verificar";

        var adapter = new TwoLineAdapter(this, ecus.Select((e, i) => new TwoLineAdapter.Row(
            e.Title, e.Sgbd, (i + 1).ToString("00"), UiUtil.Blue, i)));
        list.Adapter = adapter;
        list.ItemClick += (_, e) => OpenEcu((int)adapter.GetRow(e.Position).Tag);

        SetKey(1, "Info", ShowInfo);
        SetKey(10, "Volver", Finish);
        RefreshKeys();
    }

    void OpenEcu(int index)
    {
        var intent = new Intent(this, typeof(ChassisEcuActivity));
        intent.PutExtra(ChassisEcuActivity.ExtraChassisId, _chassis.Id);
        intent.PutExtra(ChassisEcuActivity.ExtraEcuIndex, index);
        StartActivity(intent);
    }

    void ShowInfo()
    {
        var s = DiagHolder.LoadSettings(this);
        string iface = $"Interfaz actual : {s.Type}" + (s.Type == AdapterType.UsbKDcan ? "" : "  <- cámbiala en Ajustes");
        string text = _chassis.Info
            .Replace("{iface}", iface)
            .Replace("{ecupath}", $"Ecu path        : {s.EcuPath}");
        JobDialogs.Message(this, _chassis.InfoTitle, text);
    }
}
