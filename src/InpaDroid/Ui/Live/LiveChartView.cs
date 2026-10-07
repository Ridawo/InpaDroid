using System.Globalization;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;

namespace InpaDroid.Ui.Live;

/// <summary>
/// Gráfica de líneas multiserie con ventana deslizante y autoescala. Cada serie se normaliza a su propio
/// min/max visible (RPM, temperaturas y tensiones tienen escalas muy distintas). Solo se toca desde el hilo de UI.
/// </summary>
public sealed class LiveChartView : View
{
    static readonly Color[] Palette =
    [
        Color.ParseColor("#1C69D4"), Color.ParseColor("#D4401C"), Color.ParseColor("#18A018"),
        Color.ParseColor("#9C27B0"), Color.ParseColor("#E69500"), Color.ParseColor("#00897B"),
    ];

    static readonly Color TextColor = Color.ParseColor("#303030");

    sealed class Series(string name, Color color)
    {
        public readonly string Name = name;
        public readonly Color Color = color;
        public readonly List<float> Values = [];   // NaN = sin dato
    }

    readonly List<Series> _series = [];
    readonly Paint _line = new(PaintFlags.AntiAlias) { StrokeWidth = 3f };
    readonly Paint _grid = new() { Color = Color.ParseColor("#D0D0D0"), StrokeWidth = 1f };
    readonly Paint _text = new(PaintFlags.AntiAlias);
    readonly Android.Graphics.Path _path = new();
    int _window = 120;

    public LiveChartView(Context context) : base(context)
    {
        _text.TextSize = TypedValue.ApplyDimension(ComplexUnitType.Sp, 12, Resources!.DisplayMetrics);
        _line.SetStyle(Paint.Style.Stroke);
        SetBackgroundColor(Color.White);
    }

    /// <summary>Número máximo de muestras visibles.</summary>
    public int WindowSize
    {
        get => _window;
        set
        {
            _window = Math.Max(10, value);
            // Al reducir la ventana se descartan las muestras sobrantes; si no, OnDraw las pintaría fuera del área.
            foreach (var s in _series)
                TrimToWindow(s.Values);
            Invalidate();
        }
    }

    void TrimToWindow(List<float> values)
    {
        if (values.Count > _window)
            values.RemoveRange(0, values.Count - _window);
    }

    public int SampleCount => _series.Count == 0 ? 0 : _series[0].Values.Count;

    public void Clear()
    {
        _series.Clear();
        Invalidate();
    }

    /// <summary>Añade una muestra (nombre -> valor); las series ausentes reciben un hueco.</summary>
    public void AddSample(IReadOnlyDictionary<string, double> sample)
    {
        foreach (var name in sample.Keys)
        {
            if (_series.Any(s => s.Name == name))
                continue;
            var s = new Series(name, Palette[_series.Count % Palette.Length]);
            for (int i = SampleCount; i > 0; i--)
                s.Values.Add(float.NaN);
            _series.Add(s);
        }
        foreach (var s in _series)
        {
            s.Values.Add(sample.TryGetValue(s.Name, out double v) ? (float)v : float.NaN);
            TrimToWindow(s.Values);
        }
        Invalidate();
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        float lh = _text.TextSize * 1.3f;
        float pad = lh * 0.6f;
        var plot = new RectF(pad, pad * 2 + lh * _series.Count, Width - pad, Height - pad);

        for (int i = 0; i < _series.Count; i++)
        {
            var s = _series[i];
            float last = s.Values[^1];
            _text.Color = s.Color;
            canvas.DrawText($"{s.Name}  {(float.IsNaN(last) ? "--" : last.ToString("0.##", CultureInfo.InvariantCulture))}",
                pad, pad + lh * (i + 1), _text);
        }
        _text.Color = TextColor;

        if (plot.Width() < 10 || plot.Height() < 10)
            return;
        for (int g = 0; g <= 4; g++)
        {
            float y = plot.Top + plot.Height() * g / 4f;
            canvas.DrawLine(plot.Left, y, plot.Right, y, _grid);
        }

        float dx = plot.Width() / (_window - 1);
        foreach (var s in _series)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (float v in s.Values)
            {
                if (float.IsNaN(v))
                    continue;
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
            if (min > max)
                continue;
            float range = max - min;
            if (range < 1e-6f)
            {
                min -= 0.5f;
                range = 1f;
            }
            _line.Color = s.Color;
            _path.Reset();
            bool pen = false;
            int offset = _window - s.Values.Count;
            for (int i = 0; i < s.Values.Count; i++)
            {
                float v = s.Values[i];
                if (float.IsNaN(v))
                {
                    pen = false;
                    continue;
                }
                float x = plot.Left + (offset + i) * dx;
                float y = plot.Bottom - (v - min) / range * plot.Height();
                if (pen)
                    _path.LineTo(x, y);
                else
                    _path.MoveTo(x, y);
                pen = true;
            }
            canvas.DrawPath(_path, _line);
        }
    }
}
