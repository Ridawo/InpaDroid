using System.Text.RegularExpressions;

namespace InpaDroid.Diag;

/// <summary>Estado del fallo según lo que informó la centralita; Unknown si el resultado no lo trae.</summary>
public enum FaultState { Unknown, Active, History }

/// <summary>Condición de funcionamiento (F_UWn_*) guardada junto al fallo.</summary>
public sealed record FaultCondition(string Text, string Value, string Unit);

/// <summary>Fallo interpretado a partir de un set de FS_LESEN.</summary>
public sealed record FaultDetail(string Code, string Description, FaultState State, string? Counter,
    IReadOnlyList<FaultCondition> Conditions);

// Lógica pura (sin Android), enlazada también desde tests/InpaDroid.Tests.
public static class FaultInfo
{
    static readonly string[] UwParts = ["TEXT", "WERT", "EINH"];
    static readonly Regex UwKey = new(@"^F_UW(\d*)_(TEXT|WERT|EINH)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Un set es un fallo si trae el número de lugar de fallo.</summary>
    public static bool IsFault(IReadOnlyDictionary<string, string> set) => set.ContainsKey("F_ORT_NR");

    public static FaultDetail Parse(IReadOnlyDictionary<string, string> set)
    {
        string Get(string key) => set.TryGetValue(key, out var v) ? v.Trim() : "";

        // Solo F_VORHANDEN (0/1) dice si el fallo está presente ahora; sin él no se afirma nada.
        var state = Get("F_VORHANDEN") switch
        {
            "1" => FaultState.Active,
            "0" => FaultState.History,
            _ => FaultState.Unknown,
        };

        string hex = Get("F_HEX_CODE");
        string counter = Get("F_HLZ");

        // Condiciones: F_UW1_TEXT / F_UW1_WERT / F_UW1_EINH se agrupan por índice, en orden numérico.
        var groups = new SortedDictionary<int, string[]>();
        foreach (var (key, value) in set)
        {
            var m = UwKey.Match(key);
            if (!m.Success)
                continue;
            int idx = m.Groups[1].Length > 0 && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
            if (!groups.TryGetValue(idx, out var parts))
                groups[idx] = parts = new string[3];
            parts[Array.IndexOf(UwParts, m.Groups[2].Value.ToUpperInvariant())] = value.Trim();
        }
        var conditions = groups.Values
            .Select(p => new FaultCondition(p[0] ?? "", p[1] ?? "", p[2] ?? ""))
            .Where(c => c.Text.Length > 0 || c.Value.Length > 0)
            .ToList();

        return new FaultDetail(Get("F_ORT_NR"), Get("F_ORT_TEXT"), state,
            counter.Length > 0 ? counter : null, conditions);
    }
}
