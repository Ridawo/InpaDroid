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

    sealed record Block(ICharSequence Text, Kind Kind, string Title = "", IReadOnlyList<KeyValuePair<string, string>>? Rows = null);

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
            var child = _root.GetChildAt(i);
            if (child is TextView tv)
                sb.AppendLine(WithoutLinks(tv.TextFormatted)).AppendLine();
            else if (child.Tag is TableTag tag)
            {
                sb.AppendLine(tag.Block.Title);
                int pad = Math.Min(MaxNamePad, tag.Block.Rows!.Select(r => r.Key.Length).DefaultIfEmpty(0).Max());
                foreach (var (name, value) in tag.Block.Rows!)
                    sb.AppendLine(name.PadRight(pad) + " : " + value.Replace("\n", "\n" + new string(' ', pad + 3)));
                sb.AppendLine();
            }
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

    /// <summary>Fila de botones táctiles bajo el último bloque (p. ej. Reintentar / Ajustes).</summary>
    public void AddActions(params (string Label, Action Click)[] actions)
    {
        var ctx = _root.Context!;
        var row = new LinearLayout(ctx) { Orientation = Android.Widget.Orientation.Horizontal };
        foreach (var (label, click) in actions)
        {
            var b = UiUtil.BigButton(ctx, label);
            b.SetBackgroundResource(Resource.Drawable.bg_card_primary);
            b.SetTextColor(new Color(ctx.GetColor(Resource.Color.m_on_primary)));
            b.Click += (_, _) => click();
            var lp = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f);
            int m = UiUtil.Dp(ctx, 4);
            lp.SetMargins(m, m, m, m);
            row.AddView(b, lp);
        }
        _root.AddView(row);
    }

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
                var child = _root.GetChildAt(i);
                if (child is TextView tv && blocks[i].Rows == null)
                    Apply(tv, blocks[i]);
                else if (child is LinearLayout card && blocks[i].Rows != null)
                    FillTable(card, blocks[i]);
                else
                {
                    // Cambió el tipo de bloque en esa posición: se reconstruye todo.
                    _root.RemoveAllViews();
                    break;
                }
            }
            if (_root.ChildCount == blocks.Count)
                return;
        }
        if (!append)
            _root.RemoveAllViews();
        int m = UiUtil.Dp(ctx, 5);
        foreach (var b in blocks)
        {
            if (b.Rows != null)
            {
                var card = new LinearLayout(ctx) { Orientation = Android.Widget.Orientation.Vertical };
                FillTable(card, b);
                var clp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent,
                    LinearLayout.LayoutParams.WrapContent);
                clp.SetMargins(0, m, 0, m);
                _root.AddView(card, clp);
                continue;
            }
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

    sealed class TableTag(Block block) : Java.Lang.Object
    {
        public Block Block { get; set; } = block;
    }

    /// <summary>Tarjeta clave/valor: título del set y una fila por resultado (nombre tenue a la izquierda, valor a la derecha).</summary>
    static void FillTable(LinearLayout card, Block b)
    {
        var ctx = card.Context!;
        // Refresco del Status (cada ~1 s): mismas filas, solo cambian los valores; no se reconstruyen las vistas.
        if (card.Tag is TableTag prev && prev.Block.Kind == b.Kind && prev.Block.Title == b.Title
            && prev.Block.Rows!.Select(r => r.Key).SequenceEqual(b.Rows!.Select(r => r.Key)))
        {
            prev.Block = b;
            for (int i = 0; i < b.Rows!.Count; i++)
            {
                if (card.GetChildAt(i + 1) is LinearLayout row && row.GetChildAt(1) is TextView val
                    && val.Text != b.Rows[i].Value)
                    val.Text = b.Rows[i].Value;
            }
            return;
        }
        card.Tag = new TableTag(b);
        card.RemoveAllViews();
        int p = UiUtil.Dp(ctx, 14);
        card.SetPadding(p, p, p, p);
        card.SetBackgroundResource(b.Kind == Kind.Fault ? Resource.Drawable.bg_card_danger : Resource.Drawable.bg_card);

        var title = new TextView(ctx) { Text = b.Title };
        title.SetTypeface(null, TypefaceStyle.Bold);
        title.SetTextSize(ComplexUnitType.Sp, 14);
        title.SetTextColor(UiUtil.Blue);
        card.AddView(title);

        int gap = UiUtil.Dp(ctx, 4);
        var secondary = UiUtil.Res(ctx, Resource.Color.m_text_secondary);
        var normal = UiUtil.Res(ctx, Resource.Color.m_text);
        var faultText = UiUtil.FaultText;
        var faultBg = UiUtil.FaultBg;
        foreach (var (name, value) in b.Rows!)
        {
            bool fault = name is "F_ORT_NR" or "F_ORT_TEXT";
            var row = new LinearLayout(ctx) { Orientation = Android.Widget.Orientation.Horizontal };
            row.SetPadding(0, gap, 0, gap);
            var key = new TextView(ctx) { Text = name };
            key.SetTextSize(ComplexUnitType.Sp, 12);
            key.SetTextColor(secondary);
            row.AddView(key, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f));
            var val = new TextView(ctx) { Text = value, Gravity = Android.Views.GravityFlags.End };
            val.SetTextSize(ComplexUnitType.Sp, 14);
            val.SetTextIsSelectable(true);
            val.SetTypeface(null, TypefaceStyle.Bold);
            val.SetTextColor(fault ? faultText : normal);
            if (fault)
                val.SetBackgroundColor(faultBg);
            row.AddView(val, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1.4f));
            card.AddView(row);
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
            h.Append("\n" + (faults == 0 ? ctx.GetString(Resource.String.g_rp_no_faults) : UiUtil.Str(ctx, Resource.String.g_rp_faults_count, faults)));
        blocks.Add(new Block(h, Kind.Header));

        if (!r.Ok)
            blocks.Add(new Block(Styled("ERROR", r.Error.Length > 0 ? r.Error : ctx.GetString(Resource.String.g_rp_job_failed), UiUtil.ErrorText),
                Kind.Error));

        for (int i = 0; i < r.Sets.Count; i++)
        {
            var set = r.Sets[i];
            bool fault = IsFaultSet(set);
            blocks.Add(new Block(new Java.Lang.String(""), fault ? Kind.Fault : Kind.Set,
                fault ? UiUtil.Str(ctx, Resource.String.g_rp_fault_n, i + 1) : $"Set {i + 1}", set.ToList()));
        }
        if (r.Ok && r.Sets.Count == 0)
            blocks.Add(new Block(new Java.Lang.String(ctx.GetString(Resource.String.g_rp_no_results)), Kind.Info));
        return blocks;
    }

    static bool IsFaultSet(IReadOnlyDictionary<string, string> set) => set.ContainsKey("F_ORT_NR");

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
