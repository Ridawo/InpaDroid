using System.Globalization;
using System.Text;

namespace InpaDroid.Ui.Live;

/// <summary>Una serie del monitor: valores alineados con las marcas de tiempo (NaN = sin dato) y estadística de sesión.</summary>
public sealed class LiveSeries(string name, int colorIndex)
{
    internal readonly List<double> Data = [];

    public string Name { get; } = name;
    public int ColorIndex { get; } = colorIndex;
    /// <summary>Si es false la serie no se dibuja (sigue registrándose y exportándose).</summary>
    public bool Visible { get; set; } = true;
    /// <summary>Último valor recibido; NaN si la última muestra no la traía.</summary>
    public double Last => Data.Count == 0 ? double.NaN : Data[^1];
    /// <summary>Mínimo y máximo desde el último Clear (NaN si no hay datos).</summary>
    public double Min { get; internal set; } = double.NaN;
    public double Max { get; internal set; } = double.NaN;
    public IReadOnlyList<double> Values => Data;
}

/// <summary>
/// Lógica pura (sin Android) del monitor en vivo: ventana de muestras visibles, mínimos/máximos por serie y
/// exportación CSV de la sesión. Solo se usa desde el hilo de UI.
/// </summary>
public sealed class LiveSeriesData
{
    /// <summary>Tope de muestras conservadas para exportar (a 0,25 s son ~80 min); evita crecer sin límite.</summary>
    public const int MaxSessionSamples = 20_000;
    public const int MinWindow = 10;

    readonly List<LiveSeries> _series = [];
    readonly List<DateTime> _times = [];
    int _window = 120;

    public IReadOnlyList<LiveSeries> Series => _series;
    public int Count => _times.Count;

    /// <summary>Número de muestras visibles en la gráfica (las últimas).</summary>
    public int Window
    {
        get => _window;
        set => _window = Math.Max(MinWindow, value);
    }

    /// <summary>Índice de la primera muestra dentro de la ventana.</summary>
    public int WindowStart => Math.Max(0, Count - _window);

    public void Clear()
    {
        _series.Clear();
        _times.Clear();
    }

    /// <summary>Añade una muestra; las series nuevas se rellenan con huecos y las ausentes reciben un hueco.</summary>
    public void AddSample(DateTime time, IReadOnlyDictionary<string, double> sample)
    {
        foreach (var name in sample.Keys)
        {
            if (_series.Any(s => s.Name == name))
                continue;
            var s = new LiveSeries(name, _series.Count);
            for (int i = _times.Count; i > 0; i--)
                s.Data.Add(double.NaN);
            _series.Add(s);
        }
        _times.Add(time);
        foreach (var s in _series)
        {
            double v = sample.TryGetValue(s.Name, out double d) && double.IsFinite(d) ? d : double.NaN;
            s.Data.Add(v);
            if (!double.IsNaN(v))
            {
                s.Min = double.IsNaN(s.Min) ? v : Math.Min(s.Min, v);
                s.Max = double.IsNaN(s.Max) ? v : Math.Max(s.Max, v);
            }
        }
        int excess = _times.Count - MaxSessionSamples;
        if (excess > 0)
        {
            _times.RemoveRange(0, excess);
            foreach (var s in _series)
                s.Data.RemoveRange(0, excess);
        }
    }

    /// <summary>
    /// CSV ancho de la sesión: timestamp + una columna por serie (hueco = celda vacía), separador coma, CRLF.
    /// <paramref name="escape"/> escapa cada celda de texto (nombres de serie); los números y la fecha son seguros.
    /// Si el texto supera <paramref name="maxChars"/> se conservan las filas más recientes y <paramref name="truncated"/> es true.
    /// </summary>
    public string ToCsv(Func<string, string> escape, int maxChars, out bool truncated)
    {
        var header = new StringBuilder("timestamp");
        foreach (var s in _series)
            header.Append(',').Append(escape(s.Name));
        header.Append("\r\n");

        var rows = new List<string>(_times.Count);
        var sb = new StringBuilder();
        for (int i = 0; i < _times.Count; i++)
        {
            sb.Clear().Append(_times[i].ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            foreach (var s in _series)
            {
                sb.Append(',');
                if (!double.IsNaN(s.Data[i]))
                    sb.Append(s.Data[i].ToString("R", CultureInfo.InvariantCulture));
            }
            rows.Add(sb.Append("\r\n").ToString());
        }

        int total = header.Length, first = rows.Count;
        while (first > 0 && total + rows[first - 1].Length <= maxChars)
            total += rows[--first].Length;
        truncated = first > 0;

        var result = new StringBuilder(total).Append(header);
        for (int i = first; i < rows.Count; i++)
            result.Append(rows[i]);
        return result.ToString();
    }
}
