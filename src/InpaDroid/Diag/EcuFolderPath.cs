using System.Text.RegularExpressions;

namespace InpaDroid.Diag;

/// <summary>Pasa el id de documento del selector de carpetas a la ruta de archivo que necesita EDIABAS.</summary>
public static class EcuFolderPath
{
    public const string DefaultPrimaryRoot = "/storage/emulated/0";

    /// <summary>
    /// "primary:Carpeta" -> {primaryRoot}/Carpeta ; "XXXX-XXXX:Carpeta" -> /storage/XXXX-XXXX/Carpeta.
    /// Null si el volumen no es "primary" ni una tarjeta (Descargas, Documentos, etc. no tienen ruta).
    /// </summary>
    public static string? FromTreeDocumentId(string? docId, string primaryRoot = DefaultPrimaryRoot)
    {
        docId ??= "";
        int colon = docId.IndexOf(':');
        string volume = colon >= 0 ? docId[..colon] : docId;
        string rel = colon >= 0 ? docId[(colon + 1)..] : "";
        bool primary = volume.Equals("primary", StringComparison.OrdinalIgnoreCase);
        if (!primary && !Regex.IsMatch(volume, "^[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}$"))
            return null;
        string root = primary ? primaryRoot : "/storage/" + volume;
        return rel.Length > 0 ? Path.Combine(root, rel) : root;
    }
}
