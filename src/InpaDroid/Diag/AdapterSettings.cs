using Android.Content;

namespace InpaDroid.Diag;

public enum AdapterType { UsbKDcan, Enet, Elm327Bluetooth, Elm327Wifi }

public sealed class AdapterSettings
{
    private const string PrefsName = "inpadroid";

    public AdapterType Type { get; set; } = AdapterType.UsbKDcan;
    public string BluetoothAddress { get; set; } = "";        // MAC del ELM327
    public string EnetHost { get; set; } = "auto";            // IP o "auto"
    public string ElmWifiHost { get; set; } = "192.168.0.10:35000";
    public string EcuPath { get; set; } = "";                 // carpeta con .prg/.grp

    public static AdapterSettings Load(Context ctx)
    {
        var settings = new AdapterSettings();
        ISharedPreferences? prefs = ctx.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        if (prefs != null)
        {
            if (Enum.TryParse(prefs.GetString(nameof(Type), null), out AdapterType type))
            {
                settings.Type = type;
            }
            settings.BluetoothAddress = prefs.GetString(nameof(BluetoothAddress), settings.BluetoothAddress) ?? "";
            settings.EnetHost = prefs.GetString(nameof(EnetHost), settings.EnetHost) ?? "";
            settings.ElmWifiHost = prefs.GetString(nameof(ElmWifiHost), settings.ElmWifiHost) ?? "";
            settings.EcuPath = prefs.GetString(nameof(EcuPath), "") ?? "";
        }

        if (string.IsNullOrWhiteSpace(settings.EcuPath))
        {
            settings.EcuPath = DefaultEcuPath(ctx);
        }
        return settings;
    }

    public void Save(Context ctx)
    {
        ISharedPreferences? prefs = ctx.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        ISharedPreferencesEditor? editor = prefs?.Edit();
        if (editor == null)
        {
            return;
        }
        editor.PutString(nameof(Type), Type.ToString());
        editor.PutString(nameof(BluetoothAddress), BluetoothAddress);
        editor.PutString(nameof(EnetHost), EnetHost);
        editor.PutString(nameof(ElmWifiHost), ElmWifiHost);
        editor.PutString(nameof(EcuPath), EcuPath);
        editor.Apply();
    }

    // <almacenamiento externo de la app>/Ecu. Se crea para que el usuario sepa dónde copiar sus .prg/.grp.
    private static string DefaultEcuPath(Context ctx)
    {
        string baseDir = ctx.GetExternalFilesDir(null)?.AbsolutePath ?? ctx.FilesDir?.AbsolutePath ?? "";
        string ecuPath = Path.Combine(baseDir, "Ecu");
        try
        {
            Directory.CreateDirectory(ecuPath);
        }
        catch (Exception)
        {
            // la carpeta es opcional; ScanEcuFolder devuelve lista vacía si no existe
        }
        return ecuPath;
    }
}
