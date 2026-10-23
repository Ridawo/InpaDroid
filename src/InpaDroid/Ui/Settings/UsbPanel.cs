using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.Views;
using Android.Widget;
using EdiabasLib;

namespace InpaDroid.Ui.Settings;

/// <summary>Panel de ajustes del cable K+DCAN USB: estado de detección y permiso.</summary>
class UsbPanel
{
    readonly Activity _activity;
    readonly TextView _info;

    public View Section { get; }

    public UsbPanel(Activity activity, int padding)
    {
        _activity = activity;
        var usb = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        _info = new TextView(activity);
        _info.SetPadding(padding, padding, padding, padding);
        usb.AddView(_info);
        var detect = UiUtil.BigButton(activity, activity.GetString(Resource.String.st_usb_detect));
        detect.Click += (_, _) =>
        {
            UsbPermission.RequestIfNeeded(activity, force: true);
            Update();
        };
        usb.AddView(detect, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        Section = usb;
    }

    public void Update()
    {
        try
        {
            if (_activity.GetSystemService(Context.UsbService) is not UsbManager usb)
            {
                SetInfo(_activity.GetString(Resource.String.st_usb_no_host), UiUtil.ErrorText);
                return;
            }
            var drivers = EdFtdiInterface.GetDriverList(usb);
            if (drivers.Count == 0)
            {
                SetInfo(_activity.GetString(Resource.String.st_usb_not_detected), UiUtil.ErrorText);
                return;
            }
            bool granted = drivers.All(d => usb.HasPermission(d.Device));
            SetInfo(granted
                    ? _activity.GetString(Resource.String.st_usb_granted)
                    : _activity.GetString(Resource.String.st_usb_pending),
                granted ? UiUtil.OkText : UiUtil.WarnText);
        }
        catch (Exception ex)
        {
            SetInfo(UiUtil.Str(_activity, Resource.String.st_usb_error, UiUtil.Describe(ex)), UiUtil.ErrorText);
        }
    }

    void SetInfo(string text, Android.Graphics.Color color)
    {
        _info.Text = text;
        _info.SetTextColor(color);
    }
}
