using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InpaDroid.Diag;

/// <summary>Fallo guardado en una sesión; el estado va como texto (Active/History/Unknown) para que el JSON sea estable.</summary>
public sealed class FaultEntry
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string State { get; set; } = "";
    public string? Counter { get; set; }
}

/// <summary>Diferencias entre dos lecturas de la misma centralita, por código de fallo.</summary>
public sealed record SessionComparison(
    IReadOnlyList<FaultEntry> New, IReadOnlyList<FaultEntry> Fixed, IReadOnlyList<FaultEntry> Persistent);

// Lógica pura (sin Android), enlazada también desde tests/InpaDroid.Tests.
public sealed class SessionRecord
{
    public const string KindRead = "read", KindBackup = "backup";

    /// <summary>Momento de la lectura en UTC, ISO 8601 ("o").</summary>
    public string When { get; set; } = "";
    public string Chassis { get; set; } = "";
    public string EcuTitle { get; set; } = "";
    public string Sgbd { get; set; } = "";
    public string Kind { get; set; } = KindRead;
    public List<FaultEntry> Faults { get; set; } = [];

    public static SessionRecord FromFaults(DateTime when, string chassis, string ecuTitle, string sgbd, string kind, JobResult r)
    {
        var faults = new List<FaultEntry>();
        foreach (var set in r.Sets)
        {
            if (!FaultInfo.IsFault(set))
                continue;
            var d = FaultInfo.Parse(set);
            faults.Add(new FaultEntry { Code = d.Code, Description = d.Description, State = d.State.ToString(), Counter = d.Counter });
        }
        return new SessionRecord
        {
            When = when.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            Chassis = chassis, EcuTitle = ecuTitle, Sgbd = sgbd, Kind = kind, Faults = faults,
        };
    }

    /// <summary>Fecha UTC de la sesión, o null si el texto no es válido.</summary>
    [JsonIgnore]
    public DateTime? WhenUtc =>
        DateTime.TryParse(When, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
            ? t : null;

    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>Lee una sesión; null si el texto está vacío o corrupto. Ignora campos desconocidos y rellena los que faltan.</summary>
    public static SessionRecord? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var r = JsonSerializer.Deserialize<SessionRecord>(json);
            if (r == null)
                return null;
            r.When ??= "";
            r.Chassis ??= "";
            r.EcuTitle ??= "";
            r.Sgbd ??= "";
            r.Kind ??= KindRead;
            r.Faults = (r.Faults ?? []).Where(f => f != null).ToList();
            foreach (var f in r.Faults)
            {
                f.Code ??= "";
                f.Description ??= "";
                f.State ??= "";
            }
            return r;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Compara por código: nuevos (solo en after), solucionados (solo en before) y persistentes (en ambas, con los datos de after).</summary>
    public static SessionComparison Compare(SessionRecord before, SessionRecord after)
    {
        var old = before.Faults.Select(f => f.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cur = after.Faults.Select(f => f.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new SessionComparison(
            after.Faults.Where(f => !old.Contains(f.Code)).ToList(),
            before.Faults.Where(f => !cur.Contains(f.Code)).ToList(),
            after.Faults.Where(f => old.Contains(f.Code)).ToList());
    }
}
