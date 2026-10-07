namespace InpaDroid.Diag;

// Jobs que solo leen: se pueden reenviar solos tras reconectar y repetir en bucle (status). Todo lo demás
// (borrados, activaciones, codificación) lo decide el usuario.
public static class JobPolicy
{
    static readonly string[] ReadOnlyPrefixes = ["STATUS_", "IDENT", "FS_LESEN", "INFO", "SERIENNUMMER", "HS_LESEN", "IS_LESEN"];

    public static bool IsReadOnly(string job)
    {
        job = job.TrimStart();
        foreach (var p in ReadOnlyPrefixes)
            if (job.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
