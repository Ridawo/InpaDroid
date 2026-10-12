using Android.Content;
using Android.Graphics;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

/// <summary>Colores y utilidades pequeñas compartidas por las pantallas.</summary>
internal static class UiUtil
{
    public static Color BlueDark => Res(Android.App.Application.Context, Resource.Color.m_accent_text);
    public static readonly Color LedOn = Color.ParseColor("#18B818");
    public static readonly Color LedOff = Color.ParseColor("#707070");
    public static Color ErrorText => Res(Android.App.Application.Context, Resource.Color.m_error);
    public static Color ErrorBg => Res(Android.App.Application.Context, Resource.Color.m_error_container);
    public static Color OkText => Res(Android.App.Application.Context, Resource.Color.m_ok);
    public static Color FaultBg => Res(Android.App.Application.Context, Resource.Color.m_fault_bg);
    public static Color FaultText => Res(Android.App.Application.Context, Resource.Color.m_fault_text);
    public static Color InfoBg => Res(Android.App.Application.Context, Resource.Color.m_info_bg);

    public static Color WarnText => Res(Android.App.Application.Context, Resource.Color.m_warn);

    public static int Dp(Context ctx, float dp) =>
        (int)(dp * (ctx.Resources?.DisplayMetrics?.Density ?? 1f) + 0.5f);

    /// <summary>Color de recurso (API 23+).</summary>
    public static Color Res(Context ctx, int colorRes) => new(ctx.GetColor(colorRes));

    /// <summary>Altura mínima táctil recomendada (48dp).</summary>
    public const int TouchDp = 48;

    /// <summary>Botón grande (≥48dp) sin mayúsculas forzadas.</summary>
    public static Button BigButton(Context ctx, string text)
    {
        var b = new Button(ctx) { Text = text };
        b.SetAllCaps(false);
        b.SetMinHeight(Dp(ctx, TouchDp));
        b.SetMinimumHeight(Dp(ctx, TouchDp));
        return b;
    }

    /// <summary>Recurso de texto con argumentos "%1$s" (los argumentos se pasan como cadenas).</summary>
    public static string Str(Context ctx, int resId, params object?[] args) =>
        ctx.GetString(resId, args.Select(a => (Java.Lang.Object)new Java.Lang.String(a?.ToString() ?? "")).ToArray());

    public static void Toast(Context ctx, string text) =>
        Android.Widget.Toast.MakeText(ctx, text, ToastLength.Short)?.Show();

    /// <summary>Mensaje comprensible de una excepción; si se reconoce la causa, va seguido del texto técnico original.</summary>
    public static string Describe(Exception ex)
    {
        var ctx = Android.App.Application.Context;
        if (ex is OperationCanceledException)
            return ctx.GetString(Resource.String.g_cancelled)!;
        var inner = ex.GetBaseException();
        return Friendly(ctx, string.IsNullOrWhiteSpace(inner.Message) ? inner.GetType().Name : inner.Message);
    }

    /// <summary>Traduce el texto técnico de un error (p. ej. JobResult.Error) a un mensaje accionable; desconocido = tal cual.</summary>
    public static string Friendly(Context ctx, string raw)
    {
        int resId = DiagLogic.Classify(raw) switch
        {
            ErrorKind.NoEcuResponse => Resource.String.er_no_ecu_response,
            ErrorKind.EcuConnection => Resource.String.er_ecu_connection,
            ErrorKind.AdapterLost => Resource.String.er_adapter_lost,
            ErrorKind.InterfaceNotFound => Resource.String.er_interface_not_found,
            ErrorKind.UsbPermission => Resource.String.er_usb_permission,
            ErrorKind.PortOpen => Resource.String.er_port_open,
            ErrorKind.Timeout => Resource.String.er_timeout,
            ErrorKind.SgbdNotFound => Resource.String.er_sgbd_not_found,
            ErrorKind.SgbdInvalid => Resource.String.er_sgbd_invalid,
            ErrorKind.JobNotFound => Resource.String.er_job_not_found,
            ErrorKind.Cancelled => Resource.String.g_cancelled,
            ErrorKind.ServiceClosed => Resource.String.er_service_closed,
            _ => 0,
        };
        if (resId == 0)
            return raw;
        string text = ctx.GetString(resId)!;
        return resId == Resource.String.g_cancelled ? text : text + "\n" + Str(ctx, Resource.String.er_technical, raw);
    }
}
