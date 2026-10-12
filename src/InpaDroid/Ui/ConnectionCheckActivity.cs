using System.Globalization;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.OS;
using Android.Widget;
using EdiabasLib;
using InpaDroid.Diag;
using InpaDroid.Ui.Chassis;

namespace InpaDroid.Ui;

/// <summary>Guía de primer uso: adaptador, archivos ECU y una prueba de conexión de solo lectura.</summary>
[Activity(Label = "@string/ws_title", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
                           | ConfigChanges.UiMode | ConfigChanges.Locale)]
public class ConnectionCheckActivity : InpaActivity
{
    // Los chasis cuyo catálogo define qué archivos hacen falta como mínimo.
    static readonly string[] CatalogChassis = ["e39", "e46"];

    TextView _adapterState = null!, _adapterNote = null!, _filesState = null!, _filesList = null!, _testResult = null!;
    Button _testBtn = null!;
    bool _testing;

    // Mientras se prueba, la cabecera no debe lanzar su propia lectura de UTILITY.
    protected override bool PauseHeader => _testing;

    /// <summary>true si los ajustes bastan para intentar conectar (cable USB aparte: eso lo ve el paso 1).</summary>
    internal static bool AdapterConfigured(AdapterSettings s) => s.Type switch
    {
        AdapterType.Elm327Bluetooth => s.BluetoothAddress.Length > 0,
        AdapterType.Enet => s.EnetHost.Length > 0,
        AdapterType.Elm327Wifi => s.ElmWifiHost.Length > 0,
        _ => true,
    };

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        InitInpa(Resource.Layout.ws_activity_check, GetString(Resource.String.ws_title));
        SetSubtitle(GetString(Resource.String.ws_subtitle));
        _adapterState = FindViewById<TextView>(Resource.Id.ws_adapter_state)!;
        _adapterNote = FindViewById<TextView>(Resource.Id.ws_adapter_note)!;
        _filesState = FindViewById<TextView>(Resource.Id.ws_files_state)!;
        _filesList = FindViewById<TextView>(Resource.Id.ws_files_list)!;
        _testResult = FindViewById<TextView>(Resource.Id.ws_test_result)!;
        _testBtn = FindViewById<Button>(Resource.Id.ws_test_btn)!;
        void OpenSettings(object? s, EventArgs e) => StartActivity(new Intent(this, typeof(SettingsActivity)));
        FindViewById<Button>(Resource.Id.ws_adapter_btn)!.Click += OpenSettings;
        FindViewById<Button>(Resource.Id.ws_files_btn)!.Click += OpenSettings;
        _testBtn.Click += (_, _) => RunTest();
    }

    protected override void OnResume()
    {
        base.OnResume();
        UpdateAdapter();
        UpdateFiles();
    }

    // Devuelve si el adaptador está listo para conectar y deja el estado del paso 1 en pantalla.
    bool UpdateAdapter()
    {
        var s = DiagHolder.LoadSettings(this);
        bool ready = AdapterConfigured(s);
        string text;
        if (s.Type == AdapterType.UsbKDcan)
            (ready, text) = UsbState();
        else if (!ready)
            text = GetString(Resource.String.ws_adapter_bt_none);   // el único tipo sin dirección es Bluetooth
        else
            text = UiUtil.Str(this, Resource.String.ws_adapter_ready, s.Type);
        SetState(_adapterState, text, ready);
        // Avisos de compatibilidad reutilizados de los ajustes.
        _adapterNote.Text = s.Type switch
        {
            AdapterType.Enet => GetString(Resource.String.st_warn_enet),
            AdapterType.Elm327Bluetooth or AdapterType.Elm327Wifi =>
                GetString(Resource.String.st_warn_elm) + "\n\n" + GetString(Resource.String.st_kline_note),
            _ => "",
        };
        _adapterNote.Visibility = _adapterNote.Text.Length > 0 ? Android.Views.ViewStates.Visible : Android.Views.ViewStates.Gone;
        return ready;
    }

    // Misma comprobación que UsbPanel: cable FTDI presente y permiso concedido.
    (bool Ready, string Text) UsbState()
    {
        try
        {
            if (GetSystemService(UsbService) is not UsbManager usb)
                return (false, GetString(Resource.String.st_usb_no_host));
            var drivers = EdFtdiInterface.GetDriverList(usb);
            if (drivers.Count == 0)
                return (false, GetString(Resource.String.st_usb_not_detected));
            return drivers.All(d => usb.HasPermission(d.Device))
                ? (true, GetString(Resource.String.st_usb_granted))
                : (false, GetString(Resource.String.st_usb_pending));
        }
        catch (Exception ex)
        {
            return (false, UiUtil.Str(this, Resource.String.st_usb_error, UiUtil.Describe(ex)));
        }
    }

    // Devuelve si UTILITY está en la carpeta y deja el estado del paso 2 en pantalla.
    bool UpdateFiles()
    {
        string path = DiagHolder.LoadSettings(this).EcuPath;
        DiagHolder.LoadCatalogs(this, path);
        var present = SetupCheck.ListEcuNames(path);
        var required = SetupCheck.RequiredSgbds(CatalogChassis.SelectMany(id => ChassisCatalog.Get(id).Ecus.Select(e => e.Sgbd)));
        string head = UiUtil.Str(this, Resource.String.ws_files_folder, path) + "\n" + (present.Count > 0
            ? UiUtil.Str(this, Resource.String.ws_files_count, present.Count)
            : GetString(Resource.String.ws_files_none));
        SetState(_filesState, head, present.Count > 0);
        var missing = SetupCheck.Missing(required, present);
        _filesList.Text = GetString(Resource.String.ws_files_need) + "\n" + string.Join("\n", required.Select(r =>
            UiUtil.Str(this, missing.Contains(r) ? Resource.String.ws_file_missing : Resource.String.ws_file_ok, r)));
        return present.Contains(SetupCheck.UtilitySgbd);
    }

    void SetState(TextView view, string text, bool ok)
    {
        view.Text = text;
        view.SetTextColor(ok ? UiUtil.OkText : UiUtil.WarnText);
    }

    async void RunTest()
    {
        if (_testing)
            return;
        bool adapterReady = UpdateAdapter();
        bool utility = UpdateFiles();
        double? ubatt = null;
        bool? ignition = null;
        if (adapterReady && utility)
        {
            _testing = true;
            _testBtn.Enabled = false;
            _testResult.Text = "";
            ShowBusy(GetString(Resource.String.ws_test_busy)!, GetString(Resource.String.g_abort), AbortJob);
            try
            {
                // Misma lectura de solo lectura que la cabecera, por la cola de DiagService.
                (ubatt, ignition) = await Diag.ReadBatteryIgnitionAsync();
            }
            catch (Exception)
            {
                adapterReady = false;   // no se pudo ni crear el servicio: el adaptador no abre
            }
            finally
            {
                _testing = false;
                _testBtn.Enabled = true;
                HideBusy();
            }
        }
        string volts = ubatt?.ToString("0.0", CultureInfo.InvariantCulture) ?? "";
        var fault = SetupCheck.Diagnose(adapterReady, utility, ubatt, ignition);
        _testResult.SetTextColor(fault == ConnectionFault.None ? UiUtil.OkText : UiUtil.ErrorText);
        _testResult.Text = fault switch
        {
            ConnectionFault.Adapter => GetString(Resource.String.ws_test_adapter),
            ConnectionFault.EcuFiles => GetString(Resource.String.ws_test_files),
            ConnectionFault.NoReply => GetString(Resource.String.ws_test_noreply),
            ConnectionFault.IgnitionOff => UiUtil.Str(this, Resource.String.ws_test_ign_off, volts),
            _ => UiUtil.Str(this, Resource.String.ws_test_ok, volts,
                ignition == null ? GetString(Resource.String.ws_ign_unknown) : GetString(Resource.String.ws_ign_on)),
        };
    }
}
