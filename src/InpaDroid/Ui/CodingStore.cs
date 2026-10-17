using Android.Content;
using InpaDroid.Diag;
using Path = System.IO.Path;

namespace InpaDroid.Ui;

/// <summary>Copias de la codificación leída: un JSON pequeño por lectura en FilesDir/coding.</summary>
public static class CodingStore
{
    const int MaxFiles = 100;

    static string Dir(Context ctx) => Path.Combine(ctx.FilesDir?.AbsolutePath ?? "", "coding");

    /// <summary>Guarda la copia. Nunca lanza: devuelve false si no se ha podido escribir.</summary>
    public static bool Save(Context ctx, CodingBackup.Snapshot snapshot)
    {
        try
        {
            var dir = Dir(ctx);
            Directory.CreateDirectory(dir);
            // Nombre plano y saneado: la fecha ordena los archivos y SGBD + job evitan choques en el mismo instante.
            var safe = new string((snapshot.Sgbd + "_" + snapshot.Job).Where(c => char.IsAsciiLetterOrDigit(c) || c == '_').ToArray());
            File.WriteAllText(Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMddTHHmmssfff}_{safe}.json"), snapshot.ToJson());
            Prune(dir);
            return true;
        }
        catch
        {
            // Mejor esfuerzo.
            return false;
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

    /// <summary>Todas las copias legibles, la más reciente primero. Los archivos corruptos se ignoran.</summary>
    public static List<CodingBackup.Snapshot> Load(Context ctx)
    {
        var result = new List<CodingBackup.Snapshot>();
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
                    if (CodingBackup.FromJson(File.ReadAllText(f)) is { } s)
                        result.Add(s);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return result;
    }
}
