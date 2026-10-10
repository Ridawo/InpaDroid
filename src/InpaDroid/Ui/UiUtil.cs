using Android.Content;
using Android.Graphics;
using Android.Widget;

namespace InpaDroid.Ui;

/// <summary>Colores y utilidades pequeñas compartidas por las pantallas.</summary>
internal static class UiUtil
{
    public static readonly Color Blue = Color.ParseColor("#1C69D4");
    public static Color BlueDark => Res(Android.App.Application.Context, Resource.Color.m_accent_text);
    public static readonly Color LedOn = Color.ParseColor("#18B818");
    public static readonly Color LedOff = Color.ParseColor("#707070");
    public static Color ErrorText => Res(Android.App.Application.Context, Resource.Color.m_error);
    public static Color ErrorBg => Res(Android.App.Application.Context, Resource.Color.m_error_container);
    public static Color OkText => Res(Android.App.Application.Context, Resource.Color.m_ok);
    public static Color FaultBg => Res(Android.App.Application.Context, Resource.Color.m_fault_bg);
    public static Color FaultText => Res(Android.App.Application.Context, Resource.Color.m_fault_text);
    public static readonly Color FaultBlockBg = Color.ParseColor("#FFF6E0");
    public static readonly Color BlockBg = Color.ParseColor("#FFFFFF");
    public static readonly Color HeaderBg = Color.ParseColor("#DCE6F6");
    public static Color InfoBg => Res(Android.App.Application.Context, Resource.Color.m_info_bg);
    public static readonly Color KeyBg = Color.ParseColor("#E4E1DA");
    public static readonly Color KeyPressed = Color.ParseColor("#A9C4EE");
    public static readonly Color KeyBorder = Color.ParseColor("#404040");
    public static readonly Color ShiftOn = Color.ParseColor("#F2C200");

    public static Color WarnText => Res(Android.App.Application.Context, Resource.Color.m_warn);

    public static int Dp(Context ctx, float dp) =>
        (int)(dp * (ctx.Resources?.DisplayMetrics?.Density ?? 1f) + 0.5f);

    /// <summary>Color de recurso (API 23+).</summary>
    public static Color Res(Context ctx, int colorRes) => new(ctx.GetColor(colorRes));

    /// <summary>Altura mínima táctil recomendada (48dp).</summary>
    public const int TouchDp = 48;

    /// <summary>Fondo de tarjeta (drawable bg_card / bg_card_primary / bg_card_danger).</summary>
    public static void SetCard(Android.Views.View v, int drawableRes) =>
        v.SetBackgroundResource(drawableRes);

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

    /// <summary>Texto corto de una excepción, sin trazas.</summary>
    public static string Describe(Exception ex)
    {
        var inner = ex.GetBaseException();
        var msg = string.IsNullOrWhiteSpace(inner.Message) ? inner.GetType().Name : inner.Message;
        return ex is OperationCanceledException ? Android.App.Application.Context.GetString(Resource.String.g_cancelled) : msg;
    }
}
