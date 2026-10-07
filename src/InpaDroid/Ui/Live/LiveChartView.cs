using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;

namespace InpaDroid.Ui.Live;

/// <summary>
/// Gráfica de líneas multiserie con ventana deslizante. Cada serie se normaliza a su propio min/max visible
/// (RPM, temperaturas y tensiones tienen escalas muy distintas). Los datos viven en <see cref="LiveSeriesData"/>;
/// las series ocultas no se dibujan. La leyenda con valores es interactiva y está en la Activity. Solo hilo de UI.
/// </summary>
public sealed class LiveChartView : View
{
    static readonly Color[] Palette =
    [
        Color.ParseColor("#1C69D4"), Color.ParseColor("#D4401C"), Color.ParseColor("#18A018"),
        Color.ParseColor("#9C27B0"), Color.ParseColor("#E69500"), Color.ParseColor("#00897B"),
    ];

    public static Color ColorOf(int index) => Palette[index % Palette.Length];

    readonly LiveSeriesData _data;
    readonly Paint _line = new(PaintFlags.AntiAlias);
    readonly Paint _grid = new() { Color = Color.ParseColor("#D0D0D0") };
    readonly Android.Graphics.Path _path = new();

    public LiveChartView(Context context, LiveSeriesData data) : base(context)
    {
        _data = data;
        float density = Resources?.DisplayMetrics?.Density ?? 1f;
        _line.StrokeWidth = 2.5f * density;
        _line.StrokeJoin = Paint.Join.Round;
        _line.StrokeCap = Paint.Cap.Round;
        _line.SetStyle(Paint.Style.Stroke);
        _grid.StrokeWidth = Math.Max(1f, density * 0.75f);
        SetBackgroundColor(Color.White);
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        float pad = TypedValue.ApplyDimension(ComplexUnitType.Dip, 6, Resources!.DisplayMetrics);
        var plot = new RectF(pad, pad, Width - pad, Height - pad);
        if (plot.Width() < 10 || plot.Height() < 10)
            return;
        for (int g = 0; g <= 4; g++)
        {
            float y = plot.Top + plot.Height() * g / 4f;
            canvas.DrawLine(plot.Left, y, plot.Right, y, _grid);
        }

        int window = _data.Window, start = _data.WindowStart, n = _data.Count - start;
        float dx = plot.Width() / (window - 1);
        int offset = window - n;
        foreach (var s in _data.Series)
        {
            if (!s.Visible)
                continue;
            var values = s.Values;
            double min = double.MaxValue, max = double.MinValue;
            for (int i = start; i < values.Count; i++)
            {
                if (double.IsNaN(values[i]))
                    continue;
                min = Math.Min(min, values[i]);
                max = Math.Max(max, values[i]);
            }
            if (min > max)
                continue;
            double range = max - min;
            if (range < 1e-9)
            {
                min -= 0.5;
                range = 1;
            }
            _line.Color = ColorOf(s.ColorIndex);
            _path.Reset();
            bool pen = false;
            for (int i = 0; i < n; i++)
            {
                double v = values[start + i];
                if (double.IsNaN(v))
                {
                    pen = false;
                    continue;
                }
                float x = plot.Left + (offset + i) * dx;
                float y = plot.Bottom - (float)((v - min) / range) * plot.Height();
                if (pen)
                    _path.LineTo(x, y);
                else
                {
                    // MoveTo + LineTo al mismo punto: con cap redondo dibuja un punto aislado.
                    _path.MoveTo(x, y);
                    _path.LineTo(x, y);
                }
                pen = true;
            }
            canvas.DrawPath(_path, _line);
        }
    }
}
