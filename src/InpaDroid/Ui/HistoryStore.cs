using Android.Content;
using InpaDroid.Diag;
using Path = System.IO.Path;

namespace InpaDroid.Ui;

/// <summary>Historial de lecturas de errores: un JSON pequeño por sesión en FilesDir/history.</summary>
public static class HistoryStore
{
    public const string KindRead = SessionRecord.KindRead, KindBackup = SessionRecord.KindBackup;

    const int MaxFiles = 200;

    static string Dir(Context ctx) => Path.Combine(ctx.FilesDir?.AbsolutePath ?? "", "history");

    /// <summary>Guarda la sesión si la lectura fue correcta. Nunca lanza: el historial no puede estropear una lectura.</summary>
    public static void Save(Context ctx, string chassis, string ecuTitle, string sgbd, string kind, JobResult faults)
    {
        try
        {
            if (!faults.Ok)
                return;
            var now = DateTime.UtcNow;
            var rec = SessionRecord.FromFaults(now, chassis, ecuTitle, sgbd, kind, faults);
            var dir = Dir(ctx);
            Directory.CreateDirectory(dir);
            // Nombre plano y saneado: la fecha ordena los archivos y el tipo evita choques en el mismo instante.
            var safeKind = new string((kind ?? "").Where(char.IsAsciiLetterOrDigit).ToArray());
            File.WriteAllText(Path.Combine(dir, $"{now:yyyyMMddTHHmmssfff}_{safeKind}.json"), rec.ToJson());
            Prune(dir);
        }
        catch
        {
            // Mejor esfuerzo.
        }
    }

    static void Prune(string dir)
    {
        var files = Directory.GetFiles(dir, "*.json");
        if (files.Length <= MaxFiles)
            return;
        Array.Sort(files, StringComparer.Ordinal);
        foreach (var f in files.Take(files.Length - MaxFiles))
            File.Delete(f);
    }

    /// <summary>Todas las sesiones legibles, la más reciente primero. Los archivos corruptos se ignoran.</summary>
    public static List<SessionRecord> Load(Context ctx)
    {
        var result = new List<SessionRecord>();
        try
        {
            var dir = Dir(ctx);
            if (!Directory.Exists(dir))
                return result;
            var files = Directory.GetFiles(dir, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var f in files.Reverse())
            {
                try
                {
                    if (SessionRecord.FromJson(File.ReadAllText(f)) is { } r)
                        result.Add(r);
                }
                catch (IOException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return result;
    }

    public static void DeleteAll(Context ctx)
    {
        try
        {
            var dir = Dir(ctx);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
