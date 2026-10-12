using System.Globalization;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;
using Android.Views.Accessibility;

namespace InpaDroid.Ui.Live;

/// <summary>
/// Gráfica de líneas multiserie con ventana deslizante. Cada serie se normaliza a su propio min/max visible
/// (RPM, temperaturas y tensiones tienen escalas muy distintas). Los datos viven en <see cref="LiveSeriesData"/>;
/// las series ocultas no se dibujan. La leyenda con valores es interactiva y está en la Activity. Solo hilo de UI.
/// </summary>
public sealed class LiveChartView : View
{
    static readonly int[] PaletteIds =
    [
        Resource.Color.inpa_series_1, Resource.Color.inpa_series_2, Resource.Color.inpa_series_3,
        Resource.Color.inpa_series_4, Resource.Color.inpa_series_5, Resource.Color.inpa_series_6,
    ];

    public static Color ColorOf(Context ctx, int index) => UiUtil.Res(ctx, PaletteIds[index % PaletteIds.Length]);

    /// <summary>Negro o blanco, el que dé más contraste (WCAG) sobre <paramref name="bg"/>, para texto sobre un chip.</summary>
    public static Color TextOn(Color bg)
    {
        static double Lin(int c8)
        {
            double c = c8 / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        double l = 0.2126 * Lin(bg.R) + 0.7152 * Lin(bg.G) + 0.0722 * Lin(bg.B);
        // Contraste con blanco = 1.05/(l+0.05); con negro = (l+0.05)/0.05.
        return 1.05 / (l + 0.05) >= (l + 0.05) / 0.05 ? Color.White : Color.Black;
    }

    readonly LiveSeriesData _data;
    readonly Paint _line = new(PaintFlags.AntiAlias);
    readonly Paint _grid = new();
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
        ApplyTheme();
    }

    /// <summary>Recalcula los colores del tema (claro/oscuro); la actividad lo llama si el modo cambia con ella abierta.</summary>
    public void ApplyTheme()
    {
        _grid.Color = UiUtil.Res(Context!, Resource.Color.m_divider);
        SetBackgroundColor(UiUtil.Res(Context!, Resource.Color.m_surface));
        Invalidate();
    }

    // Descripción dinámica para TalkBack: último/mín/máx de cada serie visible.
    public override void OnInitializeAccessibilityNodeInfo(AccessibilityNodeInfo? info)
    {
        base.OnInitializeAccessibilityNodeInfo(info);
        if (info == null)
            return;
        var parts = _data.Series.Where(s => s.Visible && !double.IsNaN(s.Last)).Select(s =>
            UiUtil.Str(Context!, Resource.String.ax_chart_series, s.Name, Fmt(s.Last), Fmt(s.Min), Fmt(s.Max))).ToList();
        info.ContentDescription = parts.Count == 0
            ? Context!.GetString(Resource.String.ax_chart_empty)
            : string.Join("; ", parts);
    }

    static string Fmt(double v) => v.ToString("0.##", CultureInfo.CurrentCulture);

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
            _line.Color = ColorOf(Context!, s.ColorIndex);
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
