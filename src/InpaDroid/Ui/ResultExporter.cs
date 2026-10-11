using System.Globalization;
using System.Text;
using Android.Content;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

/// <summary>Formatea resultados de jobs como CSV y los comparte como fichero con la hoja de compartir de Android.</summary>
internal static class ResultExporter
{
    public sealed record Entry(DateTime Time, string Ecu, string Job, JobResult Result);

    const string Header = "timestamp,ecu,job,job_status,ok,set,name,value";

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

    public static void Share(Context ctx, IEnumerable<Entry> entries, string title) =>
        ShareCsv(ctx, ToCsv(entries), title);

    /// <summary>Escribe el CSV en cache/exports y lo comparte como fichero (content:// de <see cref="ExportProvider"/>).</summary>
    public static void ShareCsv(Context ctx, string csv, string title)
    {
        try
        {
            var dir = ExportProvider.Dir(ctx);
            // Solo se borran los antiguos: una app receptora puede seguir leyendo el último compartido.
            long cutoff = Java.Lang.JavaSystem.CurrentTimeMillis() - 24 * 3600 * 1000L;
            foreach (var old in dir.ListFiles() ?? [])
            {
                if (old.LastModified() < cutoff)
                    old.Delete();
            }
            var now = DateTime.Now;
            string stamp = now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
            string name = $"inpadroid-{stamp}.csv";
            File.WriteAllText(System.IO.Path.Combine(dir.AbsolutePath, name), csv, new UTF8Encoding(true));

            var uri = ExportProvider.UriFor(name);
            var send = new Intent(Intent.ActionSend);
            send.SetType("text/csv");
            send.PutExtra(Intent.ExtraSubject, "InpaDroid " + now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            send.PutExtra(Intent.ExtraStream, uri);
            send.ClipData = ClipData.NewRawUri(name, uri);
            send.AddFlags(ActivityFlags.GrantReadUriPermission);
            var chooser = Intent.CreateChooser(send, title)!;
            if (ctx is not Android.App.Activity)
                chooser.AddFlags(ActivityFlags.NewTask);
            ctx.StartActivity(chooser);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Java.Lang.RuntimeException)
        {
            // Sin app que reciba el fichero (ActivityNotFoundException) o sin poder escribirlo en cache.
            UiUtil.Toast(ctx, UiUtil.Str(ctx, Resource.String.live_export_failed, UiUtil.Describe(ex)));
        }
    }
}
