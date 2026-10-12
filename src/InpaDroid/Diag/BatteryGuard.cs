using System.Globalization;
using System.Text.RegularExpressions;

namespace InpaDroid.Diag;

// Lógica pura (sin Android), enlazada también desde tests/InpaDroid.Tests.
// Decide si la tensión de batería permite escribir o activar algo en una centralita sin riesgo.
public static class BatteryGuard
{
    public enum State { Ok, Low, Unknown }

    // Por debajo de esta tensión una escritura puede quedar a medias y aparecen errores falsos.
    public const double MinVolts = 11.5;

    public static State Check(double? volts) =>
        volts is not double v || !double.IsFinite(v) ? State.Unknown
        : v < MinVolts ? State.Low
        : State.Ok;

    /// <summary>
    /// Tensión a partir del texto de la cabecera (InpaActivity.SetIndicators): "on  12,3 V" (coma o punto según
    /// el idioma), "off" (sin tensión = 0 V) o "--" (sin lectura = null).
    /// </summary>
    public static double? ParseHeader(string? text)
    {
        text = text?.Trim() ?? "";
        if (text.Equals("off", StringComparison.OrdinalIgnoreCase))
            return 0;
        var m = Regex.Match(text, @"\d+(?:[.,]\d+)?");
        return m.Success ? double.Parse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture) : null;
    }
}
