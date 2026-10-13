using System.Globalization;
using System.Text;

namespace InpaDroid.Diag;

// Lógica pura (sin Android), enlazada también desde tests/InpaDroid.Tests.
// Formato CSV del monitor en vivo, compartido por la exportación de la sesión y la grabación a fichero:
// timestamp + una columna por serie, separador coma, CRLF, hueco = celda vacía.
public static class LiveLog
{
    const string FilePrefix = "inpadroid-live-";

    /// <summary>Nombre del fichero de una grabación (inpadroid-live-yyyyMMdd-HHmmss.csv); si ya existe se añade -2, -3...</summary>
    public static string FileName(DateTime start, Func<string, bool> exists)
    {
        string stem = FilePrefix + start.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string name = stem + ".csv";
        for (int i = 2; exists(name); i++)
            name = $"{stem}-{i}.csv";
        return name;
    }

    /// <summary>Columnas de una grabación: las series previstas o, si no se conocen de antemano, las de la primera muestra.</summary>
    public static IReadOnlyList<string> Columns(IReadOnlyList<string> planned, IEnumerable<string> sampleKeys) =>
        planned.Count > 0 ? planned : sampleKeys.ToList();

    /// <summary>True si la muestra trae una serie sin columna: la cabecera ya escrita no la cubre y toca empezar otro fichero.</summary>
    public static bool NeedsNewFile(IReadOnlyList<string> columns, IEnumerable<string> sampleKeys) =>
        sampleKeys.Any(k => !columns.Contains(k, StringComparer.OrdinalIgnoreCase));

    /// <summary>Línea de cabecera; <paramref name="escape"/> escapa los nombres de serie (la exportación usa ResultExporter.Cell).</summary>
    public static string Header(IEnumerable<string> columns, Func<string, string> escape)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, columns, escape);
        return sb.ToString();
    }

    /// <summary>Fila de una muestra con las columnas en orden; las series ausentes quedan vacías.</summary>
    public static string Row(DateTime time, IReadOnlyList<string> columns, IReadOnlyDictionary<string, double> sample)
    {
        var sb = new StringBuilder();
        AppendRow(sb, time, columns.Select(c => sample.TryGetValue(c, out double v) ? v : double.NaN));
        return sb.ToString();
    }

    public static void AppendHeader(StringBuilder sb, IEnumerable<string> columns, Func<string, string> escape)
    {
        sb.Append("timestamp");
        foreach (var c in columns)
            sb.Append(',').Append(escape(c));
        sb.Append("\r\n");
    }

    // Los números y la fecha van en cultura invariante y no necesitan escape.
    public static void AppendRow(StringBuilder sb, DateTime time, IEnumerable<double> values)
    {
        sb.Append(time.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        foreach (double v in values)
        {
            sb.Append(',');
            if (double.IsFinite(v))
                sb.Append(v.ToString("R", CultureInfo.InvariantCulture));
        }
        sb.Append("\r\n");
    }
}
