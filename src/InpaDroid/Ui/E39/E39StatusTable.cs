using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using InpaDroid.Diag;

namespace InpaDroid.Ui.E39;

public static class E39StatusTable
{
    internal static string? StatusError(JobResult r)
    {
        if (!r.Ok)
            return r.Error.Length > 0 ? r.Error : "El job ha fallado";
        if (r.JobStatus.Length > 0 && !r.JobStatus.Equals("OKAY", StringComparison.OrdinalIgnoreCase))
            return "JOB_STATUS: " + r.JobStatus;
        return null;
    }

    internal static string? FindResult(JobResult r, string name)
    {
        foreach (var set in r.Sets)
        {
            foreach (var (key, value) in set)
            {
                if (key.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                    return value;
            }
        }
        return null;
    }

    internal static SpannableStringBuilder BuildTable(E39Page page, Dictionary<E39Value, (string Text, bool Error)> cells, int count)
    {
        int labelPad = Math.Min(E39EcuActivity.MaxLabelPad, page.Values.Count == 0 ? 0 : page.Values.Max(v => v.Label.Length)) + 3;
        int valuePad = Math.Min(E39EcuActivity.MaxValuePad, cells.Values.Where(c => !c.Error).Select(c => c.Text.Length).DefaultIfEmpty(0).Max());

        var sb = new SpannableStringBuilder();
        Append(sb, page.Title, new StyleSpan(TypefaceStyle.Bold), new ForegroundColorSpan(UiUtil.BlueDark));
        sb.Append($"   {DateTime.Now:HH:mm:ss}   #{count}\n");
        foreach (var v in page.Values)
        {
            sb.Append("\n");
            sb.Append(v.Label.Length + 1 >= labelPad ? v.Label + " " : (v.Label + " ").PadRight(labelPad - 1, '.') + " ");
            var (text, error) = cells.GetValueOrDefault(v, ("--", true));
            if (error)
                Append(sb, text, new ForegroundColorSpan(UiUtil.ErrorText));
            else
            {
                Append(sb, text.PadLeft(valuePad), new StyleSpan(TypefaceStyle.Bold));
                if (v.Unit.Length > 0)
                    sb.Append(" " + v.Unit);
            }
        }
        return sb;
    }

    static void Append(SpannableStringBuilder sb, string text, params Java.Lang.Object[] spans)
    {
        int start = sb.Length();
        sb.Append(text);
        foreach (var span in spans)
            sb.SetSpan(span, start, sb.Length(), SpanTypes.ExclusiveExclusive);
    }
}
