using System.Globalization;
using System.Text.RegularExpressions;

namespace InpaDroid.Diag;

// Lógica pura de DiagService (sin Android ni EdiabasLib) para poder probarla en el proyecto de tests.

/// <summary>Causa de fallo reconocida en el texto de un error; la UI la traduce a un mensaje en el idioma de la app.</summary>
public enum ErrorKind
{
    Unknown,
    NoEcuResponse,
    EcuConnection,
    AdapterLost,
    InterfaceNotFound,
    UsbPermission,
    PortOpen,
    Timeout,
    SgbdNotFound,
    SgbdInvalid,
    JobNotFound,
    Cancelled,
    ServiceClosed,
}

internal static class DiagLogic
{
    // Textos técnicos estables que genera DiagService (no se muestran tal cual: ErrorKind los traduce).
    public const string CancelledText = "Cancelled";
    public const string ServiceClosedText = "Diagnostic service closed";
    public static string TimeoutText(int seconds) => $"Timeout: no response after {seconds} s";

    // Solo códigos EDIABAS cuyo significado consta en la tabla de EdiabasNet (ErrorDescription).
    private static readonly Dictionary<string, ErrorKind> CodeKinds = new(StringComparer.Ordinal)
    {
        ["IFH-0001"] = ErrorKind.AdapterLost,        // UART ERROR
        ["IFH-0002"] = ErrorKind.AdapterLost,        // NO RESPONSE FROM INTERFACE
        ["IFH-0003"] = ErrorKind.AdapterLost,        // DATATRANSMISSION TO INTERFACE DISTURBED
        ["IFH-0017"] = ErrorKind.AdapterLost,        // INTERFACE DEADLOCK ERROR
        ["IFH-0008"] = ErrorKind.EcuConnection,      // CONTROLUNIT CONNECTION ERROR
        ["IFH-0009"] = ErrorKind.NoEcuResponse,      // NO RESPONSE FROM CONTROLUNIT
        ["IFH-0010"] = ErrorKind.NoEcuResponse,      // DATATRANSMISSION TO CONTROLUNIT DISTURBED
        ["IFH-0032"] = ErrorKind.NoEcuResponse,      // IGNITION IS OFF
        ["IFH-0011"] = ErrorKind.InterfaceNotFound,  // UNKNOWN INTERFACE
        ["IFH-0018"] = ErrorKind.InterfaceNotFound,  // INITIALIZATION ERROR
        ["IFH-0027"] = ErrorKind.InterfaceNotFound,  // IFH NOT FOUND
        ["IFH-0019"] = ErrorKind.PortOpen,           // DEVICE ACCESS ERROR
        ["IFH-0021"] = ErrorKind.PortOpen,           // ILLEGAL PORT
        ["IFH-0029"] = ErrorKind.PortOpen,           // ACCESS DENIED
        ["SYS-0002"] = ErrorKind.SgbdNotFound,       // ECU OBJECT FILE NOT FOUND
        ["SYS-0005"] = ErrorKind.SgbdNotFound,       // OBJECT FILE NOT FOUND
        ["SYS-0020"] = ErrorKind.SgbdNotFound,       // BASE OBJECT FILE NOT FOUND
        ["SYS-0003"] = ErrorKind.SgbdInvalid,        // ECU OBJECT FILE ERROR
        ["SYS-0004"] = ErrorKind.SgbdInvalid,        // ILLEGAL FORMAT OF ECU OBJECTFILE
        ["SYS-0006"] = ErrorKind.SgbdInvalid,        // GROUP OBJECT FILE ERROR
        ["SYS-0007"] = ErrorKind.SgbdInvalid,        // ILLEGAL FORMAT OF GROUP OBJECT FILE
        ["BIP-0003"] = ErrorKind.SgbdInvalid,        // OBJECT FILE ERROR
        ["BIP-0006"] = ErrorKind.SgbdInvalid,        // BEST FILE ERROR
        ["BIP-0009"] = ErrorKind.SgbdInvalid,        // BEST VERSION ERROR
        ["SYS-0008"] = ErrorKind.JobNotFound,        // JOB NOT FOUND
    };

    // Acepta "IFH-0018", "EDIABAS_IFH_0018" y "EDIABAS::IFH-0018".
    private static readonly Regex CodeRegex = new(@"(IFH|BIP|SYS)[-_](\d{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Causa reconocida en el texto técnico de un error, o Unknown (entonces se muestra el texto tal cual).</summary>
    public static ErrorKind Classify(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ErrorKind.Unknown;
        if (raw.Equals(CancelledText, StringComparison.Ordinal))
            return ErrorKind.Cancelled;
        if (raw.Equals(ServiceClosedText, StringComparison.Ordinal))
            return ErrorKind.ServiceClosed;
        if (raw.StartsWith("Timeout:", StringComparison.Ordinal))
            return ErrorKind.Timeout;
        foreach (Match m in CodeRegex.Matches(raw))
        {
            if (CodeKinds.TryGetValue(m.Groups[1].Value.ToUpperInvariant() + "-" + m.Groups[2].Value, out var kind))
                return kind;
        }
        if (raw.Contains("could not open port", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("cannot open port", StringComparison.OrdinalIgnoreCase))
            return ErrorKind.PortOpen;
        if (raw.Contains("permission", StringComparison.OrdinalIgnoreCase) &&
            (raw.Contains("usb", StringComparison.OrdinalIgnoreCase) || raw.Contains("ftdi", StringComparison.OrdinalIgnoreCase)))
            return ErrorKind.UsbPermission;
        if (raw.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            return ErrorKind.Timeout;
        return ErrorKind.Unknown;
    }

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

    /// <summary>Motivo de fallo de un job (fallo EDIABAS o JOB_STATUS distinto de OKAY), o null si fue bien. <paramref name="failedText"/> es el texto (localizado) si el job falló sin mensaje.</summary>
    public static string? StatusError(JobResult r, string failedText)
    {
        if (!r.Ok)
            return r.Error.Length > 0 ? r.Error : failedText;
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
