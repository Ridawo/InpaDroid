namespace InpaDroid.Diag;

/// <summary>Qué falla en la prueba de conexión, de lo más básico (cable) a lo más lejano (coche).</summary>
public enum ConnectionFault { None, Adapter, EcuFiles, NoReply, IgnitionOff }

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
    /// </summary>
    public static ConnectionFault Diagnose(bool adapterReady, bool utilityPresent, double? ubatt, bool? ignition)
    {
        if (!adapterReady) return ConnectionFault.Adapter;
        if (!utilityPresent) return ConnectionFault.EcuFiles;
        if (ubatt is null or <= 0) return ConnectionFault.NoReply;   // sin tensión en el pin 16 el cable no está en un coche
        return ignition == false ? ConnectionFault.IgnitionOff : ConnectionFault.None;
    }
}
