using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui.E39;

/// <summary>Menú "BMW E39": centralitas del catálogo E39, como la selección de SG de un script INPA.</summary>
[Activity(Label = "BMW E39", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class E39MenuActivity : InpaActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        InitInpa(Resource.Layout.e39_menu, "BMW E39");
        SetSubtitle("Línea K (DS2/KWP2000)");

        var heading = FindViewById<TextView>(Resource.Id.e39_menu_heading)!;
        var list = FindViewById<ListView>(Resource.Id.e39_menu_list)!;
        var ecus = E39Catalog.Ecus;
        heading.Text = ecus.Count == 0 ? "Catálogo E39 vacío" : $"Selección de centralita ({ecus.Count})";

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
        var intent = new Intent(this, typeof(E39EcuActivity));
        intent.PutExtra(E39EcuActivity.ExtraEcuIndex, index);
        StartActivity(intent);
    }

    void ShowInfo()
    {
        var s = DiagHolder.LoadSettings(this);
        string text =
            "BMW E39 (Serie 5, 1995-2004)\n\n" +
            "Las centralitas del E39 van por línea K (DS2 / KWP2000).\n" +
            "Interfaz: cable USB K+DCAN (FTDI) con los pines 7 y 8 puenteados (modo K-line).\n" +
            "ENET y ELM327 no sirven para este coche.\n\n" +
            $"Interfaz actual : {s.Type}" + (s.Type == AdapterType.UsbKDcan ? "" : "  <- cámbiala en Ajustes") + "\n" +
            $"Ecu path        : {s.EcuPath}\n\n" +
            "Copia ahí los .prg/.grp de tu instalación INPA (carpeta ecu).\n" +
            "Al abrir una centralita se identifica la variante (.grp -> .prg) y se muestran las teclas:\n" +
            "F2 Ident, F4 errores, Shift+F4 borrar, F5 Status, F6 Steuern, F7 todos los jobs.";
        JobDialogs.Message(this, "Información E39", text);
    }
}
