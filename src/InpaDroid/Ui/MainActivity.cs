using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using InpaDroid.Diag;
using InpaDroid.Ui.Chassis;

namespace InpaDroid.Ui;

/// <summary>Pantalla de inicio, como el startus.ips de INPA.</summary>
[Activity(Label = "InpaDroid", MainLauncher = true, Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class MainActivity : InpaActivity
{
    ResultPanel _results = null!;
    bool _working;
    bool _aborted;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        InitInpa(Resource.Layout.activity_main, "InpaDroid");
        FindViewById(Resource.Id.inpa_back)!.Visibility = ViewStates.Gone;
        FindViewById(Resource.Id.inpa_title)!.SetPadding(UiUtil.Dp(this, 16), 0, 0, 0);
        SetSubtitle("EDIABAS: ediabaslib");
        _results = new ResultPanel(FindViewById<LinearLayout>(Resource.Id.main_results)!);

        BuildMenu();
    }

    protected override void OnResume()
    {
        base.OnResume();
        UpdateStatus();
    }

    void UpdateStatus()
    {
        var s = DiagHolder.LoadSettings(this);
        string iface = s.Type switch
        {
            AdapterType.UsbKDcan => "Cable USB K+DCAN",
            AdapterType.Enet => $"WiFi ENET ({s.EnetHost})",
            AdapterType.Elm327Bluetooth => s.BluetoothAddress.Length > 0
                ? $"ELM327 Bluetooth ({s.BluetoothAddress})" : GetString(Resource.String.main_adapter_none),
            AdapterType.Elm327Wifi => $"ELM327 WiFi ({s.ElmWifiHost})",
            _ => GetString(Resource.String.main_adapter_none),
        };
        FindViewById<TextView>(Resource.Id.main_status)!.Text = iface;
    }

    void BuildMenu()
    {
        var menu = FindViewById<LinearLayout>(Resource.Id.main_menu)!;
        menu.RemoveAllViews();
        // Chasis nuevo: su <id>_catalog.json en Ui/Chassis + una tarjeta aquí.
        var cards = new (int Title, int Desc, int Icon, bool Primary, Action Act)[]
        {
            (Resource.String.main_card_identify, Resource.String.main_card_identify_desc, Resource.Drawable.ic_ident, true, IdentifyVehicle),
            (Resource.String.main_card_ecu, Resource.String.main_card_ecu_desc, Resource.Drawable.ic_list, false,
                () => StartActivity(new Intent(this, typeof(EcuSelectActivity)))),
            (Resource.String.main_card_e39, Resource.String.main_card_e39_desc, Resource.Drawable.ic_car, false, () => ChassisMenuActivity.Start(this, "e39")),
            (Resource.String.main_card_e46, Resource.String.main_card_e46_desc, Resource.Drawable.ic_car, false, () => ChassisMenuActivity.Start(this, "e46")),
            (Resource.String.main_card_e60, Resource.String.main_card_e60_desc, Resource.Drawable.ic_car, false, () => ChassisMenuActivity.Start(this, "e60")),
            (Resource.String.main_card_e90, Resource.String.main_card_e90_desc, Resource.Drawable.ic_car, false, () => ChassisMenuActivity.Start(this, "e90")),
            (Resource.String.main_card_settings, Resource.String.main_card_settings_desc, Resource.Drawable.ic_tune, false,
                () => StartActivity(new Intent(this, typeof(SettingsActivity)))),
            (Resource.String.main_card_info, Resource.String.main_card_info_desc, Resource.Drawable.ic_info, false, ShowInfo),
            (Resource.String.main_card_exit, Resource.String.main_card_exit_desc, Resource.Drawable.ic_exit, false, Exit),
        };
        int gap = UiUtil.Dp(this, 6);
        LinearLayout? row = null;
        for (int i = 0; i < cards.Length; i++)
        {
            if (i % 2 == 0)
            {
                row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                menu.AddView(row, new LinearLayout.LayoutParams(-1, -2));
            }
            var c = cards[i];
            var card = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true };
            card.SetBackgroundResource(c.Primary ? Resource.Drawable.bg_card_primary : Resource.Drawable.bg_card);
            int pad = UiUtil.Dp(this, 14);
            card.SetPadding(pad, pad, pad, pad);
            card.SetMinimumHeight(UiUtil.Dp(this, 110));
            var icon = new ImageView(this);
            icon.SetImageResource(c.Icon);
            icon.ImageTintList = Android.Content.Res.ColorStateList.ValueOf(new Android.Graphics.Color(
                Resources!.GetColor(c.Primary ? Resource.Color.m_on_primary : Resource.Color.m_primary, Theme)));
            card.AddView(icon, new LinearLayout.LayoutParams(UiUtil.Dp(this, 28), UiUtil.Dp(this, 28))
                { BottomMargin = UiUtil.Dp(this, 6) });
            var title = new TextView(this);
            title.SetTextAppearance(Resource.Style.MText_Title);
            title.SetTextSize(ComplexUnitType.Sp, 16);
            title.SetText(c.Title);
            var desc = new TextView(this);
            desc.SetTextAppearance(Resource.Style.MText_Caption);
            desc.SetText(c.Desc);
            if (c.Primary)
            {
                var on = Resources!.GetColor(Resource.Color.m_on_primary, Theme);
                title.SetTextColor(on);
                desc.SetTextColor(on);
            }
            card.AddView(title);
            card.AddView(desc);
            var act = c.Act;
            card.Click += (_, _) => act();
            var lp = new LinearLayout.LayoutParams(0, -2, 1f);
            lp.SetMargins(gap, gap, gap, gap);
            row!.AddView(card, lp);
        }
        if (cards.Length % 2 == 1)
            row!.AddView(new View(this), new LinearLayout.LayoutParams(0, 1, 1f));
    }

    void ShowInfo()
    {
        string version = "?";
        try
        {
#pragma warning disable CS0618, CA1422 // GetPackageInfo(string, int) obsoleto en API 33, pero vale para todas
            version = PackageManager?.GetPackageInfo(PackageName ?? "", 0)?.VersionName ?? "?";
#pragma warning restore CS0618, CA1422
        }
        catch
        {
        }
        var s = DiagHolder.LoadSettings(this);
        string iface = s.Type switch
        {
            AdapterType.UsbKDcan => "USB K+DCAN (FTDI)",
            AdapterType.Enet => $"ENET ({s.EnetHost})",
            AdapterType.Elm327Bluetooth => $"ELM327 Bluetooth ({(s.BluetoothAddress.Length > 0 ? s.BluetoothAddress : GetString(Resource.String.g_not_chosen))})",
            AdapterType.Elm327Wifi => $"ELM327 WiFi ({s.ElmWifiHost})",
            _ => s.Type.ToString(),
        };
        string text =
            GetString(Resource.String.g_info_title) + "\n\n" +
            UiUtil.Str(this, Resource.String.g_info_version, version) + "\n" +
            GetString(Resource.String.g_info_engine) + "\n\n" +
            "EDIABAS\n" +
            $"Ecu path       : {s.EcuPath}\n" +
            UiUtil.Str(this, Resource.String.g_info_interface, iface) + "\n\n" +
            GetString(Resource.String.g_info_nofiles);
        JobDialogs.Message(this, GetString(Resource.String.main_card_info)!, text);
    }

    async void IdentifyVehicle()
    {
        if (_working)
            return;
        _working = true;
        _aborted = false;
        _results.ShowInfo(GetString(Resource.String.main_card_identify)!, GetString(Resource.String.g_ident_searching)!);
        ScrollToResults();
        ShowBusy(GetString(Resource.String.g_ident_busy)!, GetString(Resource.String.g_abort), () =>
        {
            _aborted = true;
            AbortJob();
        });
        try
        {
            var svc = Diag;
            var files = await Task.Run(() => svc.ScanEcuFolder());
            bool Has(string name) => files.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            var steps = new List<(string Sgbd, string Job)>();
            if (Has("UTILITY"))
            {
                steps.Add(("UTILITY", "STATUS_UBATT"));
                steps.Add(("UTILITY", "STATUS_ZUENDUNG"));
            }
            if (Has("CAS"))
            {
                steps.Add(("CAS", "C_FG_LESEN"));   // número de bastidor
                steps.Add(("CAS", "C_FA_LESEN"));   // orden de vehículo (FA)
            }
            if (steps.Count == 0)
            {
                _results.ShowError(GetString(Resource.String.main_card_identify)!, GetString(Resource.String.g_ident_no_sgbd)!);
                return;
            }

            _results.Clear();
            foreach (var (sgbd, job) in steps)
            {
                if (_aborted)
                {
                    _results.ShowError(GetString(Resource.String.main_card_identify)!, GetString(Resource.String.g_aborted_by_user)!, append: true);
                    break;
                }
                try
                {
                    var r = await svc.RunJobAsync(sgbd, job);
                    _results.ShowResult(sgbd, job, r, append: true);
                }
                catch (Exception ex)
                {
                    _results.ShowException(sgbd, job, ex, append: true);
                }
            }
        }
        catch (Exception ex)
        {
            _results.ShowError(GetString(Resource.String.main_card_identify)!, "ERROR: " + UiUtil.Describe(ex));
        }
        finally
        {
            _working = false;
            HideBusy();
            ScrollToResults();
        }
    }

    // Con las tarjetas ocupando la pantalla, el resultado de Identificar quedaría fuera de vista.
    void ScrollToResults()
    {
        var scroll = FindViewById<ScrollView>(Resource.Id.main_scroll);
        var results = FindViewById(Resource.Id.main_results);
        if (scroll != null && results != null)
            scroll.Post(() => scroll.SmoothScrollTo(0, results.Top));
    }

    // Desde Android 12 el atrás en la actividad raíz no la cierra: sin esto la conexión con el adaptador quedaría abierta.
    void Exit()
    {
        _ = DiagHolder.Reset();
        FinishAffinity();
    }

    protected override void OnDestroy()
    {
        if (IsFinishing)
            _ = DiagHolder.Reset();
        base.OnDestroy();
    }
}
