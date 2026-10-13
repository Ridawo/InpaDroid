using System.Text;
using InpaDroid.Diag;
using InpaDroid.Ui.Chassis;

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
/// Un job que el monitor pide una vez por ciclo, con los resultados solicitados y el nombre de serie de cada uno.
/// Sin resultados se grafican todos los numéricos que devuelva, cada uno con su nombre.
/// </summary>
public sealed record LiveJob(string Job, string Args, IReadOnlyList<string> Results, IReadOnlyList<string> Series)
{
    /// <summary>Serie en la que va un resultado devuelto por el ECU; null si no se pidió.</summary>
    public string? SeriesOf(string result)
    {
        if (Results.Count == 0)
            return result;
        for (int i = 0; i < Results.Count; i++)
        {
            if (Results[i].Equals(result, StringComparison.OrdinalIgnoreCase))
                return Series[i];
        }
        return null;
    }

    /// <summary>
    /// Agrupa los valores elegidos por job y argumentos (mismo criterio que el sondeo de ChassisEcuActivity): cada job se
    /// pide una sola vez por ciclo aunque aporte varios resultados. La serie se llama como el resultado; si varios jobs
    /// devuelven el mismo nombre se antepone el job (y sus argumentos) para que no se mezclen en una columna.
    /// </summary>
    public static List<LiveJob> Group(IEnumerable<ChassisValue> values)
    {
        var picked = values.Select(v => (Job: v.Job.Trim(), v.Args, Result: v.Result.Trim()))
            .DistinctBy(v => (v.Job.ToUpperInvariant(), v.Args, v.Result.ToUpperInvariant())).ToList();
        var shared = picked.GroupBy(v => v.Result, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return picked.GroupBy(v => (Job: v.Job.ToUpperInvariant(), v.Args))
            .Select(g => new LiveJob(g.First().Job, g.Key.Args, g.Select(v => v.Result).ToList(),
                g.Select(v => !shared.Contains(v.Result) ? v.Result
                    : v.Args.Length > 0 ? $"{v.Job}({v.Args})/{v.Result}" : $"{v.Job}/{v.Result}").ToList()))
            .ToList();
    }
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
    /// </summary>
    public string ToCsv(Func<string, string> escape)
    {
        var sb = new StringBuilder();
        LiveLog.AppendHeader(sb, _series.Select(s => s.Name), escape);
        for (int i = 0; i < _times.Count; i++)
            LiveLog.AppendRow(sb, _times[i], _series.Select(s => s.Data[i]));
        return sb.ToString();
    }
}
