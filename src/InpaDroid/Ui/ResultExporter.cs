using System.Globalization;
using System.Text;
using Android.Content;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

/// <summary>Formatea resultados de jobs como CSV y los comparte con la hoja de compartir de Android (ACTION_SEND, texto).</summary>
internal static class ResultExporter
{
    public sealed record Entry(DateTime Time, string Ecu, string Job, JobResult Result);

    const string Header = "timestamp,ecu,job,job_status,ok,set,name,value";

    // Los extras de un Intent viajan por Binder (~1 MB por transacción, texto en UTF-16): por encima de
    // esto el CSV se recorta para no provocar TransactionTooLargeException.
    const int MaxShareChars = 200_000;

    public static string ToCsv(IEnumerable<Entry> entries)
    {
        var sb = new StringBuilder(Header).Append("\r\n");
        foreach (var e in entries)
        {
            string ts = e.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var prefix = new[] { ts, e.Ecu, e.Job, e.Result.JobStatus, e.Result.Ok ? "true" : "false" };
            if (e.Result.Sets.Count == 0)
            {
                // Sin result sets: una fila con el estado del job; solo es ERROR si el job ha fallado.
                Row(sb, prefix, "", e.Result.Ok ? "" : "ERROR", e.Result.Ok ? "" : e.Result.Error);
                continue;
            }
            for (int i = 0; i < e.Result.Sets.Count; i++)
                foreach (var (name, value) in e.Result.Sets[i])
                    Row(sb, prefix, (i + 1).ToString(CultureInfo.InvariantCulture), name, value);
        }
        return sb.ToString();
    }

    static void Row(StringBuilder sb, string[] prefix, string set, string name, string value)
    {
        foreach (var c in prefix)
            sb.Append(Cell(c)).Append(',');
        sb.Append(Cell(set)).Append(',').Append(Cell(name)).Append(',').Append(Cell(value)).Append("\r\n");
    }

    /// <summary>Escapa una celda CSV (RFC 4180) y neutraliza inyección de fórmulas (= + - @ tab CR).</summary>
    public static string Cell(string s)
    {
        if (s.Length > 0 && s[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            && !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            s = "'" + s;
        return s.IndexOfAny(['"', ',', '\r', '\n']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    public static void Share(Context ctx, IEnumerable<Entry> entries, string title)
    {
        string csv = ToCsv(entries);
        bool truncated = csv.Length > MaxShareChars;
        if (truncated)
        {
            // Corta tras la última fila completa que cabe, para no dejar una celda a medias.
            int cut = csv.LastIndexOf("\r\n", MaxShareChars - 1, StringComparison.Ordinal);
            csv = csv[..(cut > 0 ? cut + 2 : MaxShareChars)] + "# CSV recortado: demasiados datos para compartir\r\n";
        }

        var send = new Intent(Intent.ActionSend);
        send.SetType("text/plain");
        send.PutExtra(Intent.ExtraSubject, "InpaDroid " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        send.PutExtra(Intent.ExtraText, csv);
        var chooser = Intent.CreateChooser(send, title)!;
        if (ctx is not Android.App.Activity)
            chooser.AddFlags(ActivityFlags.NewTask);
        try
        {
            ctx.StartActivity(chooser);
            if (truncated)
                UiUtil.Toast(ctx, "CSV recortado: había demasiados datos para compartirlo entero");
        }
        catch (Java.Lang.RuntimeException ex)
        {
            // ActivityNotFoundException, o fallo de Binder (TransactionTooLargeException envuelta).
            UiUtil.Toast(ctx, "No se pudo compartir: " + UiUtil.Describe(ex));
        }
    }
}
