using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Widget;
using InpaDroid.Diag;

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
        SetSubtitle("EDIABAS: ediabaslib");
        _results = new ResultPanel(FindViewById<LinearLayout>(Resource.Id.main_results)!);

        SetKey(1, "Info", ShowInfo);
        SetKey(2, "Centralita", () => StartActivity(new Intent(this, typeof(EcuSelectActivity))));
        SetKey(3, "Identificar", IdentifyVehicle);
        SetKey(4, "E39", () => StartActivity(new Intent(this, typeof(E39.E39MenuActivity))));
        SetKey(9, "Ajustes", () => StartActivity(new Intent(this, typeof(SettingsActivity))));
        SetKey(10, "Salir", Exit);
        SetShiftKey(10, "Salir", Exit);
        RefreshKeys();
        BuildMenu();
    }

    /// <summary>Menú de texto "&lt; F1 &gt;  Información" … como la pantalla principal de INPA (cada línea se puede tocar).</summary>
    void BuildMenu()
    {
        var menu = FindViewById<LinearLayout>(Resource.Id.main_menu)!;
        menu.RemoveAllViews();
        var texts = new Dictionary<(int, bool), string>
        {
            [(1, false)] = "Información",
            [(2, false)] = "Selección de centralita",
            [(3, false)] = "Identificar vehículo",
            [(4, false)] = "Vehículo BMW E39",
            [(9, false)] = "Ajustes de interfaz",
            [(10, false)] = "Fin",
            [(10, true)] = "Salir",
        };
        foreach (var (f, shift, label) in DefinedKeys())
        {
            var line = new TextView(this)
            {
                Text = KeyCaption(f, shift, texts.GetValueOrDefault((f, shift), label)),
                Typeface = Android.Graphics.Typeface.Monospace,
                Clickable = true,
            };
            line.SetTextSize(ComplexUnitType.Sp, 15);
            line.SetTextColor(Android.Graphics.Color.Black);
            int p = UiUtil.Dp(this, 7);
            line.SetPadding(UiUtil.Dp(this, 12), p, 0, p);
            line.Click += (_, _) => PressKey(f, shift);
            menu.AddView(line);
        }
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
            AdapterType.Elm327Bluetooth => $"ELM327 Bluetooth ({(s.BluetoothAddress.Length > 0 ? s.BluetoothAddress : "sin elegir")})",
            AdapterType.Elm327Wifi => $"ELM327 WiFi ({s.ElmWifiHost})",
            _ => s.Type.ToString(),
        };
        string text =
            "InpaDroid - programas de diagnóstico\n\n" +
            $"Versión        : {version}\n" +
            "Motor EDIABAS  : ediabaslib (GPLv3)\n\n" +
            "EDIABAS\n" +
            $"Ecu path       : {s.EcuPath}\n" +
            $"Interfaz       : {iface}\n\n" +
            "No incluye archivos de BMW: copia tus .prg/.grp en la carpeta ECU.";
        JobDialogs.Message(this, "Información", text);
    }

    /// <summary>F3: lee batería/encendido (UTILITY) y bastidor/orden de vehículo (CAS) si existen los .prg.</summary>
    async void IdentifyVehicle()
    {
        if (_working)
            return;
        _working = true;
        _aborted = false;
        _results.ShowInfo("Identificar vehículo", "Buscando SGBD UTILITY / CAS en la carpeta ECU…");
        ShowBusy("Identificando vehículo…", "Abortar", () =>
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
                _results.ShowError("Identificar vehículo",
                    "No se encontraron UTILITY.prg ni CAS.prg en la carpeta ECU. Cópialos ahí o revisa Ajustes (F9).");
                return;
            }

            _results.Clear();
            foreach (var (sgbd, job) in steps)
            {
                if (_aborted)
                {
                    _results.ShowError("Identificar vehículo", "Abortado por el usuario", append: true);
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
            _results.ShowError("Identificar vehículo", "ERROR: " + UiUtil.Describe(ex));
        }
        finally
        {
            _working = false;
            HideBusy();
        }
    }

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
