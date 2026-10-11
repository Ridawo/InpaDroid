using System.Globalization;

namespace InpaDroid.Diag;

// Lógica pura de DiagService (sin Android ni EdiabasLib) para poder probarla en el proyecto de tests.
internal static class DiagLogic
{
    // Códigos EDIABAS de errores del adaptador/enlace (UART, sin respuesta del interfaz, init, acceso al dispositivo).
    // IFH-0008/0009/0010 (centralita) no cuentan: reconectar al adaptador no los arregla.
    private static readonly HashSet<string> AdapterIfhCodes = new(StringComparer.Ordinal)
    {
        "EDIABAS_IFH_0001", "EDIABAS_IFH_0002", "EDIABAS_IFH_0003",
        "EDIABAS_IFH_0017", "EDIABAS_IFH_0018", "EDIABAS_IFH_0019",
    };

    // ediabasCode devuelve el nombre del código EDIABAS si la excepción es de EdiabasNet, o null si no lo es.
    public static bool IsAdapterConnectionError(Exception ex, Func<Exception, string?> ediabasCode)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            string? code = ediabasCode(e);
            if (code != null)
            {
                if (AdapterIfhCodes.Contains(code))
                {
                    return true;
                }
            }
            else if (e is IOException or System.Net.Sockets.SocketException)
            {
                return true;
            }
        }
        return false;
    }

    // Un intento; si falla por error del adaptador y no hay cancelación (canRetry), se llama a reset y se
    // reintenta una sola vez. prepare se ejecuta antes de cada intento. Todo en el hilo de trabajo.
    public static T RunWithReconnect<T>(Func<T> work, bool allowReconnect, Func<Exception, bool> isAdapterError,
        Func<bool> canRetry, Action prepare, Action reset)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                prepare();
                return work();
            }
            catch (Exception ex) when (attempt == 0 && allowReconnect && canRetry() && isAdapterError(ex))
            {
                reset();
            }
        }
    }

    /// <summary>Motivo de fallo de un job (fallo EDIABAS o JOB_STATUS distinto de OKAY), o null si fue bien.</summary>
    public static string? StatusError(JobResult r)
    {
        if (!r.Ok)
            return r.Error.Length > 0 ? r.Error : "El job ha fallado";
        if (r.JobStatus.Length > 0 && !r.JobStatus.Equals("OKAY", StringComparison.OrdinalIgnoreCase))
            return "JOB_STATUS: " + r.JobStatus;
        return null;
    }

    /// <summary>Primer valor con ese nombre (sin distinguir mayúsculas) en cualquier set, o null.</summary>
    public static string? FindResult(JobResult r, string name)
    {
        name = name.Trim();
        foreach (var set in r.Sets)
        {
            foreach (var (key, value) in set)
            {
                if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return value;
            }
        }
        return null;
    }

    public static string FormatValue(object? value) => value switch
    {
        null => "",
        string s => s,
        long l => l.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("0.######", CultureInfo.InvariantCulture),
        byte[] bytes => BitConverter.ToString(bytes).Replace('-', ' '),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };
}
