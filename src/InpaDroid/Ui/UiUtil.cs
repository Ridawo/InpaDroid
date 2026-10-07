using Android.Content;
using Android.Graphics;
using Android.Widget;

namespace InpaDroid.Ui;

/// <summary>Colores y utilidades pequeñas compartidas por las pantallas.</summary>
internal static class UiUtil
{
    public static readonly Color Blue = Color.ParseColor("#1C69D4");
    public static readonly Color BlueDark = Color.ParseColor("#0B3E8A");
    public static readonly Color LedOn = Color.ParseColor("#18B818");
    public static readonly Color LedOff = Color.ParseColor("#707070");
    public static readonly Color ErrorText = Color.ParseColor("#C00000");
    public static readonly Color ErrorBg = Color.ParseColor("#FFE2E2");
    public static readonly Color OkText = Color.ParseColor("#006A00");
    public static readonly Color FaultBg = Color.ParseColor("#FFE97F");
    public static readonly Color FaultText = Color.ParseColor("#A00000");
    public static readonly Color FaultBlockBg = Color.ParseColor("#FFF6E0");
    public static readonly Color BlockBg = Color.ParseColor("#FFFFFF");
    public static readonly Color HeaderBg = Color.ParseColor("#DCE6F6");
    public static readonly Color InfoBg = Color.ParseColor("#ECE9E2");
    public static readonly Color KeyBg = Color.ParseColor("#E4E1DA");
    public static readonly Color KeyPressed = Color.ParseColor("#A9C4EE");
    public static readonly Color KeyBorder = Color.ParseColor("#404040");
    public static readonly Color ShiftOn = Color.ParseColor("#F2C200");

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

    public static void Toast(Context ctx, string text) =>
        Android.Widget.Toast.MakeText(ctx, text, ToastLength.Short)?.Show();

    /// <summary>Texto corto de una excepción, sin trazas.</summary>
    public static string Describe(Exception ex)
    {
        var inner = ex.GetBaseException();
        var msg = string.IsNullOrWhiteSpace(inner.Message) ? inner.GetType().Name : inner.Message;
        return ex is OperationCanceledException ? "Cancelado" : msg;
    }
}
