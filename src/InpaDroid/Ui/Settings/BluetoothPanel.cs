using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace InpaDroid.Ui.Settings;

/// <summary>Panel de ajustes Bluetooth: lista de dispositivos emparejados y permisos.</summary>
class BluetoothPanel
{
    public const int RequestCode = 1;
    const string PermConnect = "android.permission.BLUETOOTH_CONNECT";
    const string PermScan = "android.permission.BLUETOOTH_SCAN";

    readonly Activity _activity;
    readonly LinearLayout _list;
    readonly TextView _selected;

    public string Address { get; set; }

    public BluetoothPanel(Activity activity, LinearLayout list, TextView selected, string address)
    {
        _activity = activity;
        _list = list;
        _selected = selected;
        Address = address;
    }

    public void UpdateSelected(string? name)
    {
        _selected.Text = Address.Length == 0
            ? "Seleccionado: (ninguno)"
            : $"Seleccionado: {Address}" + (string.IsNullOrEmpty(name) ? "" : $"  ({name})");
    }

    bool HasPermission() =>
        Build.VERSION.SdkInt < BuildVersionCodes.S
        || (_activity.CheckSelfPermission(PermConnect) == Permission.Granted && _activity.CheckSelfPermission(PermScan) == Permission.Granted);

    public void Refresh()
    {
        if (!HasPermission())
        {
            _activity.RequestPermissions(new[] { PermConnect, PermScan }, RequestCode);
            return;
        }
        _list.RemoveAllViews();
        try
        {
            var manager = _activity.GetSystemService(Context.BluetoothService) as BluetoothManager;
            var adapter = manager?.Adapter;
            if (adapter == null)
            {
                AddMessage("Este dispositivo no tiene Bluetooth.", true);
                return;
            }
            if (!adapter.IsEnabled)
            {
                AddMessage("El Bluetooth está desactivado. Actívalo y pulsa de nuevo.", true);
                return;
            }
            var devices = adapter.BondedDevices?.ToList() ?? new List<BluetoothDevice>();
            if (devices.Count == 0)
            {
                AddMessage("No hay dispositivos emparejados. Empareja el ELM327 en los ajustes de Android (PIN habitual 1234 o 0000).", true);
                return;
            }
            foreach (var d in devices.OrderBy(d => d.Name ?? ""))
            {
                string address = d.Address ?? "";
                string name = d.Name ?? "(sin nombre)";
                if (address == Address)
                    UpdateSelected(name);
                var row = new TextView(_activity)
                {
                    Text = $"{name}\n{address}",
                    Clickable = true,
                };
                int p = UiUtil.Dp(_activity, 12);
                row.SetPadding(p, p, p, p);
                row.SetMinHeight(UiUtil.Dp(_activity, 56));
                row.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
                row.Gravity = GravityFlags.CenterVertical;
                Style(row, address == Address);
                row.Click += (_, _) =>
                {
                    Address = address;
                    UpdateSelected(name);
                    for (int i = 0; i < _list.ChildCount; i++)
                        if (_list.GetChildAt(i) is TextView tv && tv.Clickable)
                            Style(tv, false);
                    Style(row, true);
                };
                _list.AddView(row, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = UiUtil.Dp(_activity, 8) });
            }
        }
        catch (Exception ex)
        {
            AddMessage("Error Bluetooth: " + UiUtil.Describe(ex), true);
        }
    }

    void Style(TextView row, bool selected)
    {
        row.SetBackgroundResource(selected ? Resource.Drawable.bg_card_primary : Resource.Drawable.bg_card);
        row.SetTextColor(UiUtil.Res(_activity, selected ? Resource.Color.m_on_primary : Resource.Color.m_text));
    }

    void AddMessage(string text, bool error)
    {
        var tv = new TextView(_activity) { Text = text };
        int p = UiUtil.Dp(_activity, 8);
        tv.SetPadding(p, p, p, p);
        tv.SetTextColor(error ? UiUtil.ErrorText : UiUtil.Res(_activity, Resource.Color.m_text));
        _list.AddView(tv);
    }

    public void OnPermissionResult(Permission[] grantResults)
    {
        if (grantResults.Length > 0 && grantResults.All(g => g == Permission.Granted))
        {
            Refresh();
        }
        else
        {
            _list.RemoveAllViews();
            AddMessage("Permiso de Bluetooth denegado. Si marcaste \"No volver a preguntar\", actívalo manualmente:", true);
            var btn = new Button(_activity) { Text = "Abrir Ajustes de Android" };
            btn.SetAllCaps(false);
            btn.SetMinHeight(UiUtil.Dp(_activity, UiUtil.TouchDp));
            btn.Click += (_, _) =>
            {
                var intent = new Android.Content.Intent(Android.Provider.Settings.ActionApplicationDetailsSettings,
                    Android.Net.Uri.Parse("package:" + _activity.PackageName));
                try { _activity.StartActivity(intent); } catch (Exception) { }
            };
            _list.AddView(btn, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        }
    }
}
