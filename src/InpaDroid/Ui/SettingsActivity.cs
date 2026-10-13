using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using InpaDroid.Diag;
using InpaDroid.Ui.Settings;

namespace InpaDroid.Ui;

/// <summary>Ajustes: interfaz, dispositivo Bluetooth, hosts ENET / ELM WiFi y carpeta ECU.</summary>
[Activity(Label = "@string/set_title", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class SettingsActivity : InpaActivity
{
    const int RequestFolder = 2;
    const int RequestImport = 3;

    RadioGroup _adapterGroup = null!;
    View _btSection = null!, _enetSection = null!, _wifiSection = null!;
    TextView _btSelected = null!, _ecuInfo = null!, _status = null!, _adapterWarn = null!, _klineNote = null!, _enetError = null!, _wifiError = null!;
    LinearLayout _btList = null!;
    EditText _enetHost = null!, _wifiHost = null!, _ecuPath = null!;
    BluetoothPanel _bt = null!;
    UsbPanel _usb = null!;
    string _defaultPath = "";
    bool _saving;
    Button _importBtn = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        InitInpa(Resource.Layout.activity_settings, GetString(Resource.String.set_title));
        SetSubtitle(GetString(Resource.String.set_subtitle));

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
        _enetError = FindViewById<TextView>(Resource.Id.set_enet_error)!;
        _wifiError = FindViewById<TextView>(Resource.Id.set_wifi_error)!;
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
        _bt = new BluetoothPanel(this, _btList, _btSelected, s.BluetoothAddress);
        _enetHost.Text = s.EnetHost;
        _wifiHost.Text = s.ElmWifiHost;
        _ecuPath.Text = s.EcuPath;
        _bt.UpdateSelected(null);
        StyleAdapterChips();
        UpdateSections();

        _enetHost.TextChanged += (_, _) => ValidateEnet();
        _wifiHost.TextChanged += (_, _) => ValidateWifi();
        _adapterGroup.CheckedChange += (_, _) =>
        {
            StyleAdapterChips();
            UpdateSections();
            if (SelectedType() == AdapterType.Elm327Bluetooth)
                _bt.Refresh();
        };
        FindViewById<Button>(Resource.Id.set_bt_refresh)!.Click += (_, _) => _bt.Refresh();
        FindViewById<Button>(Resource.Id.set_ecu_default)!.Click += (_, _) => _ecuPath.Text = _defaultPath;
        FindViewById<Button>(Resource.Id.set_ecu_pick)!.Click += (_, _) => PickFolder();
        _importBtn = FindViewById<Button>(Resource.Id.set_ecu_import)!;
        _importBtn.Click += (_, _) =>
        {
            if (!EcuImporter.Running)
                EcuImporter.Start(this, RequestImport);
        };
        FindViewById<Button>(Resource.Id.set_save)!.Click += (_, _) => Save();

        // "Guardar" es el botón fijo del layout (set_save): no se duplica como tecla.
        SetKey(10, GetString(Resource.String.st_back), Finish);
        RefreshKeys();

        ShowEcuCount(s.EcuPath);
        if (s.Type == AdapterType.Elm327Bluetooth)
            _bt.Refresh();
    }

    // Opciones del selector como tarjetas grandes: la elegida resaltada.
    void StyleAdapterChips()
    {
        for (int i = 0; i < _adapterGroup.ChildCount; i++)
        {
            if (_adapterGroup.GetChildAt(i) is not RadioButton rb) continue;
            bool on = rb.Checked;
            rb.SetBackgroundResource(on ? Resource.Drawable.bg_card_primary : Resource.Drawable.bg_card);
            rb.SetTextColor(UiUtil.Res(this, on ? Resource.Color.m_on_primary : Resource.Color.m_text));
        }
    }

    static readonly System.Text.RegularExpressions.Regex WifiRx = new(@"^([A-Za-z0-9.\-]+)(?::(\d+))?$");

    bool ValidateEnet()
    {
        bool ok = EnetHostRule.IsValid(_enetHost.Text);
        ShowFieldError(_enetError, ok ? null : GetString(Resource.String.en_err_host));
        return ok;
    }

    bool ValidateWifi()
    {
        string raw = _wifiHost.Text?.Trim() ?? "";
        bool ok = true;
        if (raw.Length > 0)
        {
            var m = WifiRx.Match(raw);
            ok = m.Success && (!m.Groups[2].Success
                               || (int.TryParse(m.Groups[2].Value, out int port) && port >= 1 && port <= 65535));
        }
        ShowFieldError(_wifiError, ok ? null : GetString(Resource.String.set_err_wifi));
        return ok;
    }

    static void ShowFieldError(TextView tv, string? msg)
    {
        tv.Text = msg ?? "";
        tv.Visibility = msg == null ? ViewStates.Gone : ViewStates.Visible;
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
        _usb.Section.Visibility = type == AdapterType.UsbKDcan ? ViewStates.Visible : ViewStates.Gone;
        _adapterWarn.Text = type switch
        {
            AdapterType.Enet => GetString(Resource.String.st_warn_enet),
            AdapterType.Elm327Bluetooth or AdapterType.Elm327Wifi =>
                GetString(Resource.String.st_warn_elm),
            _ => "",
        };
        _adapterWarn.Visibility = _adapterWarn.Text.Length > 0 ? ViewStates.Visible : ViewStates.Gone;
        _klineNote.Visibility = type == AdapterType.Enet ? ViewStates.Gone : ViewStates.Visible;   // la nota de línea K no aplica a ENET
        if (type == AdapterType.UsbKDcan)
            _usb.Update();
    }

    protected override void OnResume()
    {
        base.OnResume();
        // El diálogo de permiso USB pausa la actividad: al volver se ve el resultado.
        if (SelectedType() == AdapterType.UsbKDcan)
            _usb.Update();
    }

    // Vistas añadidas por código tras el selector de interfaz (el layout no se toca).
    void AddAdapterHelpViews()
    {
        var parent = (ViewGroup)_adapterGroup.Parent!;
        int index = parent.IndexOfChild(_adapterGroup) + 1;
        int p = UiUtil.Dp(this, 4);

        p = UiUtil.Dp(this, 12);
        _klineNote = new TextView(this) { Text = GetString(Resource.String.st_kline_note) };
        var note = _klineNote;
        note.SetPadding(p, p, p, p);
        note.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
        note.SetTextColor(UiUtil.Res(this, Resource.Color.m_text_secondary));
        note.SetBackgroundColor(UiUtil.InfoBg);
        parent.AddView(note, index++, new LinearLayout.LayoutParams(-1, -2) { TopMargin = UiUtil.Dp(this, 8) });

        _adapterWarn = new TextView(this) { Visibility = ViewStates.Gone };
        _adapterWarn.SetPadding(p, p, p, p);
        _adapterWarn.SetTextColor(UiUtil.WarnText);
        _adapterWarn.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
        parent.AddView(_adapterWarn, index++);

        _usb = new UsbPanel(this, p);
        parent.AddView(_usb.Section, index);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != BluetoothPanel.RequestCode)
            return;
        _bt.OnPermissionResult(grantResults);
    }

    void PickFolder()
    {
        try
        {
            StartActivityForResult(new Intent(Intent.ActionOpenDocumentTree), RequestFolder);
        }
        catch (Exception ex)
        {
            ShowStatus(GetString(Resource.String.st_folder_picker_error, UiUtil.Describe(ex)), true);
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == RequestImport)
        {
            string folder = DiagHolder.LoadSettings(this).EcuPath;   // la carpeta guardada, la que usa EDIABAS
            _ = EcuImporter.HandleResult(this, resultCode, data, folder,
                (text, cancel) => ShowBusy(text, GetString(Resource.String.g_abort), cancel), HideBusy,
                (text, error) =>
                {
                    _importBtn.Enabled = true;
                    ShowStatus(text, error);
                    ShowEcuCount(folder);
                });
            _importBtn.Enabled = !EcuImporter.Running;
            return;
        }
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
                ShowStatus(GetString(Resource.String.st_folder_unusable), true);
                return;
            }
            string root = volume.Equals("primary", StringComparison.OrdinalIgnoreCase)
#pragma warning disable CS0618, CA1422 // ExternalStorageDirectory obsoleto, pero es la ruta que ve EDIABAS
                ? Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? "/storage/emulated/0"
#pragma warning restore CS0618, CA1422
                : "/storage/" + volume;
            _ecuPath.Text = rel.Length > 0 ? Path.Combine(root, rel) : root;
            ShowStatus(GetString(Resource.String.st_folder_chosen), false);
        }
        catch (Exception ex)
        {
            ShowStatus(GetString(Resource.String.st_folder_invalid, UiUtil.Describe(ex)), true);
        }
    }

    async void ShowEcuCount(string path)
    {
        _ecuInfo.SetTextColor(UiUtil.Res(this, Resource.Color.m_text_secondary));
        _ecuInfo.Text = GetString(Resource.String.st_ecu_default_searching, _defaultPath);
        try
        {
            var svc = Diag;
            var files = await Task.Run(() => svc.ScanEcuFolder());
            int groups = files.Count(f => f.IsGroup);
            _ecuInfo.Text = GetString(Resource.String.st_ecu_default_count, _defaultPath, path, (files.Count - groups).ToString(), groups.ToString());
            if (files.Count == 0)
                _ecuInfo.SetTextColor(UiUtil.ErrorText);
        }
        catch (Exception ex)
        {
            _ecuInfo.SetTextColor(UiUtil.ErrorText);
            _ecuInfo.Text = GetString(Resource.String.st_ecu_default_error, _defaultPath, UiUtil.Describe(ex));
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
            s.BluetoothAddress = _bt.Address;

            // Solo cuenta el campo del adaptador elegido: los otros están ocultos y el usuario no podría corregirlos.
            bool hostOk = s.Type switch
            {
                AdapterType.Enet => ValidateEnet(),
                AdapterType.Elm327Wifi => ValidateWifi(),
                _ => true,
            };
            if (!hostOk)
            {
                ShowStatus(GetString(Resource.String.st_check_red_field), true);
                return;
            }
            string wifiRaw = _wifiHost.Text?.Trim() ?? "";

            s.EnetHost = EnetHostRule.Normalize(_enetHost.Text);
            s.ElmWifiHost = wifiRaw.Length == 0 ? "192.168.0.10:35000" : wifiRaw;
            s.EcuPath = string.IsNullOrWhiteSpace(_ecuPath.Text) ? _defaultPath : _ecuPath.Text.Trim();
            _ecuPath.Text = s.EcuPath;
            DiagHolder.TryCreateDirectory(s.EcuPath);
            s.Save(this);

            ShowStatus(GetString(Resource.String.st_saving), false);
            await DiagHolder.Reset();   // el siguiente uso crea un DiagService con los ajustes nuevos

            if (s.Type == AdapterType.Elm327Bluetooth && s.BluetoothAddress.Length == 0)
                ShowStatus(GetString(Resource.String.st_saved_no_bt), true);
            else
            {
                ShowStatus(GetString(Resource.String.st_saved), false);
                UiUtil.Toast(this, GetString(Resource.String.st_saved)!);   // el estado queda bajo el botón Guardar, fuera de pantalla
            }
            ShowEcuCount(s.EcuPath);
        }
        catch (Exception ex)
        {
            ShowStatus(GetString(Resource.String.st_save_error, UiUtil.Describe(ex)), true);
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
