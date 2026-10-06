using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using EdiabasLib;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

/// <summary>Ajustes: interfaz, dispositivo Bluetooth, hosts ENET / ELM WiFi y carpeta ECU.</summary>
[Activity(Label = "Ajustes", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class SettingsActivity : InpaActivity
{
    const int RequestBluetooth = 1;
    const int RequestFolder = 2;
    const string PermConnect = "android.permission.BLUETOOTH_CONNECT";
    const string PermScan = "android.permission.BLUETOOTH_SCAN";
    static readonly Android.Graphics.Color WarnText = Android.Graphics.Color.ParseColor("#C05800");

    const string KLineNote =
        "E39 y coches anteriores a 2007 (línea K): usa el cable K+DCAN USB con el interruptor de pines 7/8 " +
        "en posición antigua (puenteado). ELM327 y ENET no sirven con estos coches. En E39 anteriores a 09/2000 " +
        "puede hacer falta el adaptador del conector redondo de 20 pines del vano motor.";

    RadioGroup _adapterGroup = null!;
    View _btSection = null!, _enetSection = null!, _wifiSection = null!, _usbSection = null!;
    TextView _btSelected = null!, _ecuInfo = null!, _status = null!, _adapterWarn = null!, _usbInfo = null!;
    LinearLayout _btList = null!;
    EditText _enetHost = null!, _wifiHost = null!, _ecuPath = null!;
    string _btAddress = "";
    string _defaultPath = "";
    bool _saving;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        InitInpa(Resource.Layout.activity_settings, "Ajustes");
        SetSubtitle("Interfaz y carpeta ECU");

        _adapterGroup = FindViewById<RadioGroup>(Resource.Id.set_adapter_group)!;
        _btSection = FindViewById(Resource.Id.set_bt_section)!;
        _enetSection = FindViewById(Resource.Id.set_enet_section)!;
        _wifiSection = FindViewById(Resource.Id.set_wifi_section)!;
        _btSelected = FindViewById<TextView>(Resource.Id.set_bt_selected)!;
        _btList = FindViewById<LinearLayout>(Resource.Id.set_bt_list)!;
        _enetHost = FindViewById<EditText>(Resource.Id.set_enet_host)!;
        _wifiHost = FindViewById<EditText>(Resource.Id.set_wifi_host)!;
        _ecuPath = FindViewById<EditText>(Resource.Id.set_ecu_path)!;
        _ecuInfo = FindViewById<TextView>(Resource.Id.set_ecu_info)!;
        _status = FindViewById<TextView>(Resource.Id.set_status)!;
        AddAdapterHelpViews();

        _defaultPath = DiagHolder.DefaultEcuPath(this);
        var s = DiagHolder.LoadSettings(this);
        _adapterGroup.Check(s.Type switch
        {
            AdapterType.Enet => Resource.Id.set_adapter_enet,
            AdapterType.Elm327Bluetooth => Resource.Id.set_adapter_elmbt,
            AdapterType.Elm327Wifi => Resource.Id.set_adapter_elmwifi,
            _ => Resource.Id.set_adapter_usb,
        });
        _btAddress = s.BluetoothAddress;
        _enetHost.Text = s.EnetHost;
        _wifiHost.Text = s.ElmWifiHost;
        _ecuPath.Text = s.EcuPath;
        UpdateBtSelected(null);
        UpdateSections();

        _adapterGroup.CheckedChange += (_, _) =>
        {
            UpdateSections();
            if (SelectedType() == AdapterType.Elm327Bluetooth)
                RefreshBluetooth();
        };
        FindViewById<Button>(Resource.Id.set_bt_refresh)!.Click += (_, _) => RefreshBluetooth();
        FindViewById<Button>(Resource.Id.set_ecu_default)!.Click += (_, _) => _ecuPath.Text = _defaultPath;
        FindViewById<Button>(Resource.Id.set_ecu_pick)!.Click += (_, _) => PickFolder();
        FindViewById<Button>(Resource.Id.set_save)!.Click += (_, _) => Save();

        SetKey(2, "Guardar", Save);
        SetKey(10, "Volver", Finish);
        RefreshKeys();

        ShowEcuCount(s.EcuPath);
        if (s.Type == AdapterType.Elm327Bluetooth)
            RefreshBluetooth();
    }

    AdapterType SelectedType()
    {
        int id = _adapterGroup.CheckedRadioButtonId;
        if (id == Resource.Id.set_adapter_enet) return AdapterType.Enet;
        if (id == Resource.Id.set_adapter_elmbt) return AdapterType.Elm327Bluetooth;
        if (id == Resource.Id.set_adapter_elmwifi) return AdapterType.Elm327Wifi;
        return AdapterType.UsbKDcan;
    }

    void UpdateSections()
    {
        var type = SelectedType();
        _btSection.Visibility = type == AdapterType.Elm327Bluetooth ? ViewStates.Visible : ViewStates.Gone;
        _enetSection.Visibility = type == AdapterType.Enet ? ViewStates.Visible : ViewStates.Gone;
        _wifiSection.Visibility = type == AdapterType.Elm327Wifi ? ViewStates.Visible : ViewStates.Gone;
        _usbSection.Visibility = type == AdapterType.UsbKDcan ? ViewStates.Visible : ViewStates.Gone;
        _adapterWarn.Text = type switch
        {
            AdapterType.Enet => "Aviso: ENET solo funciona con las series F/G (conexión Ethernet). No sirve para un E39.",
            AdapterType.Elm327Bluetooth or AdapterType.Elm327Wifi =>
                "Aviso: el ELM327 solo funciona con coches D-CAN (aprox. 2007 en adelante). No sirve para un E39.",
            _ => "",
        };
        _adapterWarn.Visibility = _adapterWarn.Text.Length > 0 ? ViewStates.Visible : ViewStates.Gone;
        if (type == AdapterType.UsbKDcan)
            UpdateUsbInfo();
    }

    protected override void OnResume()
    {
        base.OnResume();
        // El diálogo de permiso USB pausa la actividad: al volver se ve el resultado.
        if (SelectedType() == AdapterType.UsbKDcan)
            UpdateUsbInfo();
    }

    // Vistas añadidas por código tras el selector de interfaz (el layout no se toca).
    void AddAdapterHelpViews()
    {
        var parent = (ViewGroup)_adapterGroup.Parent!;
        int index = parent.IndexOfChild(_adapterGroup) + 1;
        int p = UiUtil.Dp(this, 4);

        var note = new TextView(this) { Text = KLineNote };
        note.SetPadding(p, p, p, p);
        note.SetTextColor(Android.Graphics.Color.Black);
        note.SetBackgroundColor(UiUtil.InfoBg);
        parent.AddView(note, index++);

        _adapterWarn = new TextView(this) { Visibility = ViewStates.Gone };
        _adapterWarn.SetPadding(p, p, p, p);
        _adapterWarn.SetTextColor(WarnText);
        _adapterWarn.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
        parent.AddView(_adapterWarn, index++);

        var usb = new LinearLayout(this) { Orientation = Orientation.Vertical };
        _usbInfo = new TextView(this) { Typeface = Android.Graphics.Typeface.Monospace };
        _usbInfo.SetPadding(p, p, p, p);
        usb.AddView(_usbInfo);
        var detect = new Button(this) { Text = "Detectar cable" };
        detect.SetAllCaps(false);
        detect.Click += (_, _) =>
        {
            UsbPermission.RequestIfNeeded(this, force: true);
            UpdateUsbInfo();
        };
        usb.AddView(detect, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
        _usbSection = usb;
        parent.AddView(usb, index);
    }

    void UpdateUsbInfo()
    {
        try
        {
            if (GetSystemService(UsbService) is not UsbManager usb)
            {
                SetUsbInfo("Este dispositivo no admite USB host.", UiUtil.ErrorText);
                return;
            }
            var drivers = EdFtdiInterface.GetDriverList(usb);
            if (drivers.Count == 0)
            {
                SetUsbInfo("Cable FTDI: no detectado. Conéctalo con un adaptador OTG y pulsa \"Detectar cable\".", UiUtil.ErrorText);
                return;
            }
            bool granted = drivers.All(d => usb.HasPermission(d.Device));
            SetUsbInfo(granted
                    ? "Cable FTDI: detectado. Permiso USB: concedido."
                    : "Cable FTDI: detectado. Permiso USB: pendiente (acepta el aviso de Android; si no sale, desconecta y vuelve a conectar el cable).",
                granted ? UiUtil.OkText : WarnText);
        }
        catch (Exception ex)
        {
            SetUsbInfo("Error USB: " + UiUtil.Describe(ex), UiUtil.ErrorText);
        }
    }

    void SetUsbInfo(string text, Android.Graphics.Color color)
    {
        _usbInfo.Text = text;
        _usbInfo.SetTextColor(color);
    }

    void UpdateBtSelected(string? name)
    {
        _btSelected.Text = _btAddress.Length == 0
            ? "Seleccionado: (ninguno)"
            : $"Seleccionado: {_btAddress}" + (string.IsNullOrEmpty(name) ? "" : $"  ({name})");
    }

    bool HasBluetoothPermission() =>
        Build.VERSION.SdkInt < BuildVersionCodes.S
        || (CheckSelfPermission(PermConnect) == Permission.Granted && CheckSelfPermission(PermScan) == Permission.Granted);

    void RefreshBluetooth()
    {
        if (!HasBluetoothPermission())
        {
            RequestPermissions(new[] { PermConnect, PermScan }, RequestBluetooth);
            return;
        }
        _btList.RemoveAllViews();
        try
        {
            var manager = GetSystemService(BluetoothService) as BluetoothManager;
            var adapter = manager?.Adapter;
            if (adapter == null)
            {
                AddBtMessage("Este dispositivo no tiene Bluetooth.", true);
                return;
            }
            if (!adapter.IsEnabled)
            {
                AddBtMessage("El Bluetooth está desactivado. Actívalo y pulsa de nuevo.", true);
                return;
            }
            var devices = adapter.BondedDevices?.ToList() ?? new List<BluetoothDevice>();
            if (devices.Count == 0)
            {
                AddBtMessage("No hay dispositivos emparejados. Empareja el ELM327 en los ajustes de Android (PIN habitual 1234 o 0000).", true);
                return;
            }
            foreach (var d in devices.OrderBy(d => d.Name ?? ""))
            {
                string address = d.Address ?? "";
                string name = d.Name ?? "(sin nombre)";
                if (address == _btAddress)
                    UpdateBtSelected(name);
                var row = new TextView(this)
                {
                    Text = $"{name}\n{address}",
                    Typeface = Android.Graphics.Typeface.Monospace,
                    Clickable = true,
                };
                int p = UiUtil.Dp(this, 8);
                row.SetPadding(p, p, p, p);
                row.SetTextColor(Android.Graphics.Color.Black);
                if (address == _btAddress)
                    row.SetBackgroundColor(UiUtil.HeaderBg);
                row.Click += (_, _) =>
                {
                    _btAddress = address;
                    UpdateBtSelected(name);
                    for (int i = 0; i < _btList.ChildCount; i++)
                        _btList.GetChildAt(i)?.SetBackgroundColor(Android.Graphics.Color.Transparent);
                    row.SetBackgroundColor(UiUtil.HeaderBg);
                };
                _btList.AddView(row);
            }
        }
        catch (Exception ex)
        {
            AddBtMessage("Error Bluetooth: " + UiUtil.Describe(ex), true);
        }
    }

    void AddBtMessage(string text, bool error)
    {
        var tv = new TextView(this) { Text = text };
        int p = UiUtil.Dp(this, 8);
        tv.SetPadding(p, p, p, p);
        tv.SetTextColor(error ? UiUtil.ErrorText : Android.Graphics.Color.Black);
        _btList.AddView(tv);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != RequestBluetooth)
            return;
        if (grantResults.Length > 0 && grantResults.All(g => g == Permission.Granted))
        {
            RefreshBluetooth();
        }
        else
        {
            _btList.RemoveAllViews();
            AddBtMessage("Permiso de Bluetooth denegado. Si marcaste \"No volver a preguntar\", actívalo manualmente:", true);
            var btn = new Button(this) { Text = "Abrir Ajustes de Android" };
            btn.SetAllCaps(false);
            btn.Click += (_, _) =>
            {
                var intent = new Android.Content.Intent(Android.Provider.Settings.ActionApplicationDetailsSettings,
                    Android.Net.Uri.Parse("package:" + PackageName));
                try { StartActivity(intent); } catch (Exception) { }
            };
            _btList.AddView(btn, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
        }
    }

    void PickFolder()
    {
        try
        {
            StartActivityForResult(new Intent(Intent.ActionOpenDocumentTree), RequestFolder);
        }
        catch (Exception ex)
        {
            ShowStatus("No se puede abrir el selector de carpetas: " + UiUtil.Describe(ex), true);
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != RequestFolder || resultCode != Result.Ok || data?.Data == null)
            return;
        try
        {
            // El selector devuelve un URI de documento; EDIABAS necesita una ruta de archivo.
            // "primary:Carpeta" -> /storage/emulated/0/Carpeta ; "XXXX-XXXX:Carpeta" -> /storage/XXXX-XXXX/Carpeta
            var docId = DocumentsContract.GetTreeDocumentId(data.Data) ?? "";
            int colon = docId.IndexOf(':');
            string volume = colon >= 0 ? docId[..colon] : docId;
            string rel = colon >= 0 ? docId[(colon + 1)..] : "";
            // Solo "primary" o un volumen tipo "1A2B-3C4D" se pueden pasar a ruta; Descargas, Documentos, etc. no.
            bool isVolume = volume.Equals("primary", StringComparison.OrdinalIgnoreCase)
                            || System.Text.RegularExpressions.Regex.IsMatch(volume, "^[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}$");
            if (data.Data.Authority != "com.android.externalstorage.documents" || !isVolume)
            {
                ShowStatus("Esa carpeta no se puede usar como ruta. Elígela desde el almacenamiento interno " +
                           "o la tarjeta SD, o usa la carpeta por defecto.", true);
                return;
            }
            string root = volume.Equals("primary", StringComparison.OrdinalIgnoreCase)
#pragma warning disable CS0618, CA1422 // ExternalStorageDirectory obsoleto, pero es la ruta que ve EDIABAS
                ? Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? "/storage/emulated/0"
#pragma warning restore CS0618, CA1422
                : "/storage/" + volume;
            _ecuPath.Text = rel.Length > 0 ? Path.Combine(root, rel) : root;
            ShowStatus("Carpeta elegida. Si después no aparecen archivos, Android no deja leerla: " +
                       "usa la carpeta por defecto de la app.", false);
        }
        catch (Exception ex)
        {
            ShowStatus("Carpeta no válida: " + UiUtil.Describe(ex), true);
        }
    }

    async void ShowEcuCount(string path)
    {
        _ecuInfo.SetTextColor(Android.Graphics.Color.Black);
        _ecuInfo.Text = $"Por defecto: {_defaultPath}\nBuscando .prg/.grp…";
        try
        {
            var svc = Diag;
            var files = await Task.Run(() => svc.ScanEcuFolder());
            int groups = files.Count(f => f.IsGroup);
            _ecuInfo.Text = $"Por defecto: {_defaultPath}\nEn {path}: {files.Count - groups} .prg, {groups} .grp";
            if (files.Count == 0)
                _ecuInfo.SetTextColor(UiUtil.ErrorText);
        }
        catch (Exception ex)
        {
            _ecuInfo.SetTextColor(UiUtil.ErrorText);
            _ecuInfo.Text = $"Por defecto: {_defaultPath}\nError: {UiUtil.Describe(ex)}";
        }
    }

    async void Save()
    {
        if (_saving)
            return;
        _saving = true;
        try
        {
            var s = DiagHolder.LoadSettings(this);
            s.Type = SelectedType();
            s.BluetoothAddress = _btAddress;

            string enetRaw = _enetHost.Text?.Trim() ?? "";
            if (enetRaw.Length > 0 && !enetRaw.Equals("auto", StringComparison.OrdinalIgnoreCase)
                && !System.Text.RegularExpressions.Regex.IsMatch(enetRaw, @"^[A-Za-z0-9.\-]+$"))
            {
                ShowStatus("Host ENET inválido: usa una IP, un hostname o \"auto\".", true);
                return;
            }

            string wifiRaw = _wifiHost.Text?.Trim() ?? "";
            if (wifiRaw.Length > 0)
            {
                var wifiMatch = System.Text.RegularExpressions.Regex.Match(wifiRaw, @"^([A-Za-z0-9.\-]+)(?::(\d+))?$");
                if (!wifiMatch.Success
                    || (wifiMatch.Groups[2].Success && (int.TryParse(wifiMatch.Groups[2].Value, out int port) ? port < 1 || port > 65535 : true)))
                {
                    ShowStatus("Host WiFi inválido: usa el formato «host:puerto», p.ej. 192.168.0.10:35000.", true);
                    return;
                }
            }

            s.EnetHost = enetRaw.Length == 0 ? "auto" : enetRaw;
            s.ElmWifiHost = wifiRaw.Length == 0 ? "192.168.0.10:35000" : wifiRaw;
            s.EcuPath = string.IsNullOrWhiteSpace(_ecuPath.Text) ? _defaultPath : _ecuPath.Text.Trim();
            _ecuPath.Text = s.EcuPath;
            DiagHolder.TryCreateDirectory(s.EcuPath);
            s.Save(this);

            ShowStatus("Guardando y reiniciando la interfaz…", false);
            await DiagHolder.Reset();   // el siguiente uso crea un DiagService con los ajustes nuevos

            if (s.Type == AdapterType.Elm327Bluetooth && s.BluetoothAddress.Length == 0)
                ShowStatus("Guardado, pero falta elegir el dispositivo Bluetooth.", true);
            else
                ShowStatus("Ajustes guardados.", false);
            ShowEcuCount(s.EcuPath);
        }
        catch (Exception ex)
        {
            ShowStatus("Error al guardar: " + UiUtil.Describe(ex), true);
        }
        finally
        {
            _saving = false;
        }
    }

    void ShowStatus(string text, bool error)
    {
        _status.Text = text;
        _status.SetTextColor(error ? UiUtil.ErrorText : UiUtil.OkText);
    }
}
