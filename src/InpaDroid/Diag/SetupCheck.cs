namespace InpaDroid.Diag;

/// <summary>Qué falla en la prueba de conexión, de lo más básico (cable) a lo más lejano (coche).</summary>
public enum ConnectionFault { None, Adapter, EcuFiles, NoReply, IgnitionOff, EnetNoNetwork, EnetNoReply }

/// <summary>Lógica pura de la guía de primer uso: pasos pendientes, archivos mínimos y diagnóstico de la prueba.</summary>
public static class SetupCheck
{
    /// <summary>SGBD que la cabecera y "Identificar vehículo" necesitan siempre.</summary>
    public const string UtilitySgbd = "UTILITY";

    /// <summary>La configuración está completa cuando hay adaptador y al menos un .prg/.grp.</summary>
    public static bool IsComplete(bool adapterConfigured, int ecuFileCount) => adapterConfigured && ecuFileCount > 0;

    /// <summary>Nombres (sin extensión, en mayúsculas) de los .prg/.grp de la carpeta; vacío si no existe o no se lee.</summary>
    public static IReadOnlySet<string> ListEcuNames(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return new HashSet<string>();
            return Directory.EnumerateFiles(path)
                .Where(f => f.EndsWith(".prg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".grp", StringComparison.OrdinalIgnoreCase))
                .Select(f => Path.GetFileNameWithoutExtension(f).ToUpperInvariant())
                .ToHashSet();
        }
        catch (Exception)
        {
            return new HashSet<string>();
        }
    }

    /// <summary>UTILITY más los SGBD de las centralitas del catálogo, sin repetidos y en mayúsculas.</summary>
    public static IReadOnlyList<string> RequiredSgbds(IEnumerable<string> catalogSgbds) =>
        new[] { UtilitySgbd }.Concat(catalogSgbds)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

    /// <summary>Los de <paramref name="required"/> que no están en <paramref name="present"/> (nombres en mayúsculas).</summary>
    public static IReadOnlyList<string> Missing(IEnumerable<string> required, IReadOnlySet<string> present) =>
        required.Where(r => !present.Contains(r.ToUpperInvariant())).ToList();

    /// <summary>
    /// Interpreta la lectura de batería/encendido. ubatt null = nadie contestó (cable, adaptador o coche);
    /// encendido apagado se avisa aparte porque el coche no responde a casi nada con el contacto quitado.
    /// Con ENET, si nadie contesta las causas son de red (no del cable K-line): networkUp null = no se pudo comprobar.
    /// </summary>
    public static ConnectionFault Diagnose(bool adapterReady, bool utilityPresent, double? ubatt, bool? ignition,
        bool enet = false, bool? networkUp = null)
    {
        if (!adapterReady) return ConnectionFault.Adapter;
        if (!utilityPresent) return ConnectionFault.EcuFiles;
        if (ubatt is null or <= 0)   // sin tensión en el pin 16 el cable no está en un coche
            return !enet ? ConnectionFault.NoReply : networkUp == false ? ConnectionFault.EnetNoNetwork : ConnectionFault.EnetNoReply;
        return ignition == false ? ConnectionFault.IgnitionOff : ConnectionFault.None;
    }
}

/// <summary>
/// Reglas del campo "host ENET" tal como las interpreta EdInterfaceEnet: "auto", "auto:all" o "auto:&lt;adaptador&gt;", o una IPv4
/// con protocolo y puertos opcionales (IP:HSFZ, IP:DoIP, IP:6801:6811). Los nombres de host no se resuelven en ediabaslib.
/// </summary>
public static class EnetHostRule
{
    public const string Default = "auto";

    /// <summary>Sin espacios laterales; vacío pasa a "auto".</summary>
    public static string Normalize(string? raw)
    {
        string host = raw?.Trim() ?? "";
        return host.Length == 0 ? Default : host;
    }

    /// <summary>true si el texto (tras recortar) es una forma que ediabaslib acepta; vacío vale como "auto".</summary>
    public static bool IsValid(string? raw)
    {
        string host = Normalize(raw);
        if (host.StartsWith("auto", StringComparison.OrdinalIgnoreCase))
            return host.Length == 4
                || (host[4] == ':' && host.Length > 5 && host[5..].All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'));
        var parts = host.Split(':');
        if (!IsIpv4(parts[0]))
            return false;
        int i = 1;
        if (i < parts.Length && parts[i] is var proto && (proto.Equals("HSFZ", StringComparison.OrdinalIgnoreCase)
            || proto.Equals("DoIP", StringComparison.OrdinalIgnoreCase) || proto.Equals("RPLUS", StringComparison.OrdinalIgnoreCase)))
            i++;
        int ports = 0;
        for (; i < parts.Length; i++, ports++)
        {
            if (ports == 2 || parts[i].Length is 0 or > 5 || !parts[i].All(char.IsAsciiDigit) || int.Parse(parts[i]) > 65535)
                return false;
        }
        return true;
    }

    static bool IsIpv4(string text)
    {
        var labels = text.Split('.');
        return labels.Length == 4 && labels.All(l => l.Length is >= 1 and <= 3 && l.All(char.IsAsciiDigit) && int.Parse(l) <= 255);
    }
}
