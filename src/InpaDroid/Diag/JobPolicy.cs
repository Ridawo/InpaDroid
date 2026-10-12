namespace InpaDroid.Diag;

// Jobs que solo leen: se pueden reenviar solos tras reconectar, repetir en bucle (status) y ejecutar sin confirmar.
// Todo lo demás (borrados, activaciones, codificación) lo decide el usuario.
public static class JobPolicy
{
    static readonly string[] ReadOnlyPrefixes = ["STATUS_", "IDENT", "FS_LESEN", "INFO", "SERIENNUMMER", "HS_LESEN", "IS_LESEN"];

    // Aunque empiecen por un prefijo de lectura, escriben o activan (p. ej. STATUS_VORGEBEN enciende luces desde el módulo de luces).
    static readonly string[] WriteMarkers = ["VORGEBEN", "SCHREIBEN", "LOESCHEN", "STEUERN", "SETZEN", "RESET", "ABGLEICH", "PROGRAMMIEREN", "SPEICHERN"];

    public static bool IsReadOnly(string job)
    {
        job = job.TrimStart();
        foreach (var w in WriteMarkers)
            if (job.Contains(w, StringComparison.OrdinalIgnoreCase))
                return false;
        foreach (var p in ReadOnlyPrefixes)
            if (job.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
