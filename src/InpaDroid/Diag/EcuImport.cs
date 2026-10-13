namespace InpaDroid.Diag;

// Lógica pura (sin Android), enlazada también desde tests/InpaDroid.Tests.
/// <summary>Reglas para importar archivos ECU (.prg/.grp) desde el selector de documentos o un ZIP.</summary>
public static class EcuImport
{
    /// <summary>Tope de bytes descomprimidos en una importación (anti zip-bomb).</summary>
    public const long MaxTotalBytes = 500L * 1024 * 1024;
    /// <summary>Tope de bytes por archivo.</summary>
    public const long MaxFileBytes = 100L * 1024 * 1024;
    /// <summary>Tope de entradas de un ZIP.</summary>
    public const int MaxEntries = 20000;

    static readonly char[] BadChars = [':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// Nombre seguro para guardar en la carpeta ECU, o null si hay que omitir el archivo.
    /// Solo se usa el último segmento de la ruta (nunca la ruta de la entrada: anti zip-slip)
    /// y solo se aceptan .prg y .grp.
    /// </summary>
    public static string? AcceptedName(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        int i = path.LastIndexOfAny(['/', '\\']);
        string name = (i >= 0 ? path[(i + 1)..] : path).Trim();
        if (name.Length <= 4 || name.Length > 255 || name[0] == '.')   // ".prg" a secas y ocultos (._x.prg de macOS)
            return null;
        if (name.Any(c => char.IsControl(c) || BadChars.Contains(c)))
            return null;
        bool ext = name.EndsWith(".prg", StringComparison.OrdinalIgnoreCase)
                   || name.EndsWith(".grp", StringComparison.OrdinalIgnoreCase);
        return ext ? name : null;
    }

    public static bool IsZip(string? name) => name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Nombre existente que solo difiere en mayúsculas/minúsculas (null si no hay). El mismo nombre exacto
    /// no es colisión: se sobrescribe para poder actualizar.
    /// </summary>
    public static string? CaseCollision(string name, IEnumerable<string> existing) =>
        existing.FirstOrDefault(e => e.Equals(name, StringComparison.OrdinalIgnoreCase) && !e.Equals(name, StringComparison.Ordinal));

    /// <summary>Bytes que se pueden escribir aún para un archivo, dado lo ya importado.</summary>
    public static long AllowedBytes(long usedTotal) => Math.Max(0, Math.Min(MaxFileBytes, MaxTotalBytes - usedTotal));
}

/// <summary>Contadores de una importación.</summary>
public sealed class ImportSummary
{
    public int Imported, Skipped, Failed;
    public bool Cancelled;
    public string? FirstError;

    public void Fail(string name, string reason)
    {
        Failed++;
        FirstError ??= name + ": " + reason;
    }
}
