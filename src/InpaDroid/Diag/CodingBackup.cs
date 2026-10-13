using System.Globalization;
using System.Text.Json;

namespace InpaDroid.Diag;

// Lógica pura (sin Android), enlazada también desde tests/InpaDroid.Tests.
// Copia de seguridad de la codificación: solo lectura. Escribir codificación necesita datos propietarios (NCS Expert)
// y puede dejar inservible una centralita, así que aquí no hay restaurar ni escribir.
public static class CodingBackup
{
    // Lecturas de codificación y orden de vehículo (FA/FG/AIF/ZIF); los nombres que contienen CODIER y terminan en _LESEN también.
    static readonly string[] ReadJobPrefixes =
        ["C_FA_LESEN", "C_FG_LESEN", "C_C_LESEN", "FA_LESEN", "AIF_LESEN", "ZIF_LESEN", "ZIF_BACKUP_LESEN"];

    /// <summary>Job que lee datos de codificación u orden de vehículo; nunca uno con marca de escritura.</summary>
    public static bool IsCodingReadJob(string job)
    {
        job = job.Trim();
        if (JobPolicy.HasWriteMarker(job))
            return false;
        if (job.Contains("CODIER", StringComparison.OrdinalIgnoreCase) && job.EndsWith("_LESEN", StringComparison.OrdinalIgnoreCase))
            return true;
        foreach (var p in ReadJobPrefixes)
            if (job.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public sealed class Entry
    {
        /// <summary>"set:NOMBRE", con el set contado desde 1 (como en ResultPanel).</summary>
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public sealed record Change(string Key, string Before, string After);

    public sealed record Comparison(IReadOnlyList<Change> Changed, IReadOnlyList<Entry> Added, IReadOnlyList<Entry> Removed);

    public sealed record Snapshot
    {
        /// <summary>Momento de la lectura en UTC, ISO 8601 ("o"): ordena bien como texto.</summary>
        public string When { get; set; } = "";
        public string Chassis { get; set; } = "";
        public string EcuTitle { get; set; } = "";
        public string Sgbd { get; set; } = "";
        public string Job { get; set; } = "";
        /// <summary>Argumentos del job (bloque/dirección); vacío en copias antiguas.</summary>
        public string Args { get; set; } = "";
        public List<Entry> Entries { get; set; } = [];

        public string ToJson() => JsonSerializer.Serialize(this);
    }

    /// <summary>Aplana todos los sets del resultado en pares clave/valor con el número de set delante.</summary>
    public static Snapshot FromResult(DateTime when, string chassis, string ecuTitle, string sgbd, string job, JobResult r, string args = "")
    {
        var entries = new List<Entry>();
        for (int i = 0; i < r.Sets.Count; i++)
            foreach (var (name, value) in r.Sets[i])
                entries.Add(new Entry { Key = $"{i + 1}:{name}", Value = value });
        return new Snapshot
        {
            When = when.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            Chassis = chassis, EcuTitle = ecuTitle, Sgbd = sgbd, Job = job, Args = args ?? "", Entries = entries,
        };
    }

    /// <summary>Lee una copia; null si el texto está vacío o corrupto. Rellena los campos que faltan.</summary>
    public static Snapshot? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var s = JsonSerializer.Deserialize<Snapshot>(json);
            if (s == null)
                return null;
            s.When ??= "";
            s.Chassis ??= "";
            s.EcuTitle ??= "";
            s.Sgbd ??= "";
            s.Job ??= "";
            s.Args ??= "";
            s.Entries = (s.Entries ?? []).Where(e => e != null).ToList();
            foreach (var e in s.Entries)
            {
                e.Key ??= "";
                e.Value ??= "";
            }
            return s;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Compara por clave: valores cambiados, claves nuevas (solo en after) y desaparecidas (solo en before).</summary>
    public static Comparison Diff(Snapshot before, Snapshot after)
    {
        var old = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in before.Entries)
            old.TryAdd(e.Key, e.Value);
        var cur = after.Entries.Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = new List<Change>();
        var added = new List<Entry>();
        foreach (var e in after.Entries)
        {
            if (!old.TryGetValue(e.Key, out var was))
                added.Add(e);
            else if (was != e.Value)
                changed.Add(new Change(e.Key, was, e.Value));
        }
        return new Comparison(changed, added, before.Entries.Where(e => !cur.Contains(e.Key)).ToList());
    }

    /// <summary>La copia anterior a current (misma SGBD, job y argumentos, fecha menor) en una lista cualquiera, o null.</summary>
    public static Snapshot? Previous(IEnumerable<Snapshot> all, Snapshot current) =>
        all.Where(s => string.Equals(s.Sgbd, current.Sgbd, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(s.Job, current.Job, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(s.Args, current.Args, StringComparison.OrdinalIgnoreCase)
                       && string.CompareOrdinal(s.When, current.When) < 0)
            .MaxBy(s => s.When, StringComparer.Ordinal);
}
