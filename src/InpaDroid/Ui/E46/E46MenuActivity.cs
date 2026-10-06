using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui.E46;

[Activity(Label = "BMW E46", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class E46MenuActivity : InpaActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        InitInpa(Resource.Layout.e39_menu, "BMW E46");
        SetSubtitle("Línea K / D-CAN (DS2/KWP2000 / CAN)");

        var heading = FindViewById<TextView>(Resource.Id.e39_menu_heading)!;
        var list    = FindViewById<ListView>(Resource.Id.e39_menu_list)!;
        var ecus    = E46Catalog.Ecus;
        heading.Text = ecus.Count == 0 ? "Catálogo E46 vacío" : $"Selección de centralita ({ecus.Count})";

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
        var intent = new Intent(this, typeof(E46EcuActivity));
        intent.PutExtra(E46EcuActivity.ExtraEcuIndex, index);
        StartActivity(intent);
    }

    void ShowInfo()
    {
        var s = DiagHolder.LoadSettings(this);
        string text =
            "BMW E46 (Serie 3, 1998-2006)\n\n" +
            "Las centralitas del E46 van por línea K (DS2 / KWP2000).\n" +
            "Interfaz: cable USB K+DCAN (FTDI) con los pines 7 y 8 puenteados (modo K-line).\n" +
            "Nota: los E46 del facelift 2006 con módulo D-CAN de fábrica requieren modo D-CAN\n" +
            "(no implementado aún; usa la pantalla de ECU genérica para esos casos).\n\n" +
            $"Interfaz actual : {s.Type}" + (s.Type == AdapterType.UsbKDcan ? "" : "  <- cámbiala en Ajustes") + "\n" +
            $"Ecu path        : {s.EcuPath}\n\n" +
            "Copia ahí los .prg/.grp de tu instalación INPA (carpeta ecu).\n" +
            "Teclas disponibles: F2 Ident, F4 errores, Shift+F4 borrar, F5 Status, F6 Steuern, F7 todos los jobs.";
        JobDialogs.Message(this, "Información E46", text);
    }
}
