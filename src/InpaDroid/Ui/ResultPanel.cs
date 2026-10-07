using System.Text;
using Android.Graphics;
using Android.Text;
using Android.Text.Method;
using Android.Text.Style;
using Android.Util;
using Android.Widget;
using InpaDroid.Diag;
using ICharSequence = Java.Lang.ICharSequence;

namespace InpaDroid.Ui;

/// <summary>
/// Lista de resultados: un bloque (TextView monoespaciado) por set, con líneas "NOMBRE : valor".
/// Si se vuelve a mostrar algo con el mismo número de bloques, se actualiza en el sitio (Status cada 1 s).
/// </summary>
internal sealed class ResultPanel
{
    const int MaxNamePad = 22;

    enum Kind { Header, Set, Fault, Error, Info }

    sealed record Block(ICharSequence Text, Kind Kind);

    readonly LinearLayout _root;

    public ResultPanel(LinearLayout root) => _root = root;

    const int MaxExports = 200;   // tope del CSV: en modo bucle el append no tiene fin
    readonly List<ResultExporter.Entry> _exports = [];

    public void Clear()
    {
        _exports.Clear();
        _root.RemoveAllViews();
    }

    /// <summary>Comparte (hoja de compartir de Android) los resultados mostrados como CSV.</summary>
    public void Share()
    {
        if (_exports.Count > 0)
            ResultExporter.Share(_root.Context!, _exports, _root.Context!.GetString(Resource.String.share_results)!);
    }

    /// <summary>Texto plano de todo lo mostrado (para copiar al portapapeles).</summary>
    public string PlainText()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _root.ChildCount; i++)
        {
            if (_root.GetChildAt(i) is TextView tv)
                sb.AppendLine(WithoutLinks(tv.TextFormatted)).AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>Quita los enlaces de la UI (p. ej. "Compartir CSV" de la cabecera) y el espacio que los precede.</summary>
    static string WithoutLinks(ICharSequence? text)
    {
        if (text is not ISpanned spanned)
            return text?.ToString() ?? "";
        var links = spanned.GetSpans(0, spanned.Length(), Java.Lang.Class.FromType(typeof(ClickableSpan)));
        if (links is not { Length: > 0 })
            return spanned.ToString();
        var sb = new SpannableStringBuilder(spanned);
        foreach (var link in links)
        {
            int start = sb.GetSpanStart(link), end = sb.GetSpanEnd(link);
            if (start < 0)
                continue;
            while (start > 0 && sb.CharAt(start - 1) == ' ')
                start--;
            sb.Delete(start, end);
        }
        return sb.ToString();
    }

    public void ShowResult(string sgbd, string job, JobResult result, bool append = false, string note = "")
    {
        if (!append)
            _exports.Clear();
        if (_exports.Count >= MaxExports)
            _exports.RemoveAt(0);
        _exports.Add(new ResultExporter.Entry(DateTime.Now, sgbd, job, result));
        Show(BuildResult(sgbd, job, result, note), append);
    }

    public void ShowError(string title, string message, bool append = false) =>
        Show(new List<Block> { new(Styled(title, message, UiUtil.ErrorText), Kind.Error) }, append);

    public void ShowException(string sgbd, string job, Exception ex, bool append = false) =>
        ShowError($"{sgbd} : {job}", "ERROR: " + UiUtil.Describe(ex), append);

    public void ShowInfo(string title, string text, bool append = false) =>
        Show(new List<Block> { new(Styled(title, text, null), Kind.Info) }, append);

    void Show(List<Block> blocks, bool append)
    {
        if (!append && blocks.Count > 0 && blocks[0].Kind != Kind.Header)
            _exports.Clear();
        var ctx = _root.Context!;
        if (!append && _root.ChildCount == blocks.Count)
        {
            // Misma estructura: actualizar en el sitio para no perder la posición de scroll.
            for (int i = 0; i < blocks.Count; i++)
            {
                if (_root.GetChildAt(i) is TextView tv)
                    Apply(tv, blocks[i]);
            }
            return;
        }
        if (!append)
            _root.RemoveAllViews();
        int m = UiUtil.Dp(ctx, 5);
        foreach (var b in blocks)
        {
            var tv = new TextView(ctx)
            {
                Typeface = Typeface.Monospace,
            };
            tv.SetTextSize(ComplexUnitType.Sp, 14);
            tv.SetLineSpacing(0, 1.25f);
            tv.SetTextIsSelectable(true);
            int p = UiUtil.Dp(ctx, 14);
            tv.SetPadding(p, p, p, p);
            Apply(tv, b);
            var lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent,
                LinearLayout.LayoutParams.WrapContent);
            lp.SetMargins(0, m, 0, m);
            _root.AddView(tv, lp);
        }
    }

    static void Apply(TextView tv, Block b)
    {
        tv.TextFormatted = b.Text;
        if (b.Kind == Kind.Header)
            tv.MovementMethod = LinkMovementMethod.Instance;
        var ctx = tv.Context!;
        tv.SetTextColor(b.Kind == Kind.Error ? UiUtil.ErrorText : new Color(ctx.GetColor(Resource.Color.m_text)));
        // Tarjetas; errores y fallos de la centralita en la variante de peligro. El padding se conserva.
        int l = tv.PaddingLeft, t = tv.PaddingTop, r = tv.PaddingRight, bt = tv.PaddingBottom;
        tv.SetBackgroundResource(b.Kind is Kind.Error or Kind.Fault ? Resource.Drawable.bg_card_danger : Resource.Drawable.bg_card);
        tv.SetPadding(l, t, r, bt);
    }

    sealed class ShareSpan(ResultPanel panel) : ClickableSpan
    {
        public override void OnClick(Android.Views.View widget) => panel.Share();

        // Sin color de enlace ni subrayado: el aspecto de chip lo dan los spans de fondo y texto.
        public override void UpdateDrawState(TextPaint ds) => ds.UnderlineText = false;
    }

    List<Block> BuildResult(string sgbd, string job, JobResult r, string note)
    {
        var blocks = new List<Block>();
        bool faultJob = job.StartsWith("FS_LESEN", StringComparison.OrdinalIgnoreCase);
        int faults = r.Sets.Count(IsFaultSet);

        var h = new SpannableStringBuilder();
        AppendSpan(h, $"{sgbd} : {job}", new StyleSpan(TypefaceStyle.Bold), new ForegroundColorSpan(UiUtil.BlueDark));
        h.Append($"   {DateTime.Now:HH:mm:ss}");
        if (note.Length > 0)
            h.Append("   " + note);
        h.Append("   ");
        // Chip visible (el texto va dentro del span para que PlainText lo quite entero).
        var ctx = _root.Context!;
        AppendSpan(h, "  " + ctx.GetString(Resource.String.share_results) + "  ", new ShareSpan(this),
            new BackgroundColorSpan(new Color(ctx.GetColor(Resource.Color.m_primary))),
            new ForegroundColorSpan(new Color(ctx.GetColor(Resource.Color.m_on_primary))),
            new StyleSpan(TypefaceStyle.Bold));
        if (r.JobStatus.Length > 0)
        {
            h.Append("\nJOB_STATUS : ");
            bool okay = r.JobStatus.Equals("OKAY", StringComparison.OrdinalIgnoreCase);
            AppendSpan(h, r.JobStatus, new StyleSpan(TypefaceStyle.Bold),
                new ForegroundColorSpan(okay ? UiUtil.OkText : UiUtil.ErrorText));
        }
        if (faultJob && r.Ok)
            h.Append(faults == 0 ? "\nMemoria de errores: sin errores almacenados" : $"\nErrores almacenados: {faults}");
        blocks.Add(new Block(h, Kind.Header));

        if (!r.Ok)
            blocks.Add(new Block(Styled("ERROR", r.Error.Length > 0 ? r.Error : "El job ha fallado", UiUtil.ErrorText),
                Kind.Error));

        for (int i = 0; i < r.Sets.Count; i++)
        {
            var set = r.Sets[i];
            bool fault = IsFaultSet(set);
            blocks.Add(new Block(BuildSet(i + 1, set), fault ? Kind.Fault : Kind.Set));
        }
        if (r.Ok && r.Sets.Count == 0)
            blocks.Add(new Block(new Java.Lang.String("(sin resultados)"), Kind.Info));
        return blocks;
    }

    static bool IsFaultSet(IReadOnlyDictionary<string, string> set) => set.ContainsKey("F_ORT_NR");

    static ICharSequence BuildSet(int index, IReadOnlyDictionary<string, string> set)
    {
        var sb = new SpannableStringBuilder();
        bool fault = IsFaultSet(set);
        AppendSpan(sb, fault ? $"── Error {index} ──" : $"── Set {index} ──", new StyleSpan(TypefaceStyle.Bold),
            new ForegroundColorSpan(UiUtil.Blue));
        int pad = set.Count == 0 ? 0 : Math.Min(MaxNamePad, set.Keys.Max(k => k.Length));
        foreach (var (name, value) in set)
        {
            sb.Append("\n");
            var text = name.PadRight(pad) + " : " + value.Replace("\n", "\n" + new string(' ', pad + 3));
            if (name is "F_ORT_NR" or "F_ORT_TEXT")
                AppendSpan(sb, text, new StyleSpan(TypefaceStyle.Bold), new ForegroundColorSpan(UiUtil.FaultText),
                    new BackgroundColorSpan(UiUtil.FaultBg));
            else
                sb.Append(text);
        }
        return sb;
    }

    static ICharSequence Styled(string title, string body, Color? bodyColor)
    {
        var sb = new SpannableStringBuilder();
        AppendSpan(sb, title, new StyleSpan(TypefaceStyle.Bold));
        sb.Append("\n");
        if (bodyColor is Color c)
            AppendSpan(sb, body, new ForegroundColorSpan(c));
        else
            sb.Append(body);
        return sb;
    }

    static void AppendSpan(SpannableStringBuilder sb, string text, params Java.Lang.Object[] spans)
    {
        int start = sb.Length();
        sb.Append(text);
        int end = sb.Length();
        foreach (var span in spans)
            sb.SetSpan(span, start, end, SpanTypes.ExclusiveExclusive);
    }
}
