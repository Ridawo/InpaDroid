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
    public static readonly Android.Graphics.Color WarnText = Android.Graphics.Color.ParseColor("#C05800");

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
        var detect = UiUtil.BigButton(activity, "Detectar cable");
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
                SetInfo("Este dispositivo no admite USB host.", UiUtil.ErrorText);
                return;
            }
            var drivers = EdFtdiInterface.GetDriverList(usb);
            if (drivers.Count == 0)
            {
                SetInfo("Cable FTDI: no detectado. Conéctalo con un adaptador OTG y pulsa \"Detectar cable\".", UiUtil.ErrorText);
                return;
            }
            bool granted = drivers.All(d => usb.HasPermission(d.Device));
            SetInfo(granted
                    ? "Cable FTDI: detectado. Permiso USB: concedido."
                    : "Cable FTDI: detectado. Permiso USB: pendiente (acepta el aviso de Android; si no sale, desconecta y vuelve a conectar el cable).",
                granted ? UiUtil.OkText : WarnText);
        }
        catch (Exception ex)
        {
            SetInfo("Error USB: " + UiUtil.Describe(ex), UiUtil.ErrorText);
        }
    }

    void SetInfo(string text, Android.Graphics.Color color)
    {
        _info.Text = text;
        _info.SetTextColor(color);
    }
}
