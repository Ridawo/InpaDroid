using System.Collections.Concurrent;
using InpaDroid.Diag;
using System.Text.Json;

namespace InpaDroid.Ui.Chassis;

// Catálogos de chasis cargados de JSON. Añadir un chasis = un <id>_catalog.json en esta carpeta
// (se incrusta en el ensamblado, ver InpaDroid.csproj) + su entrada en el menú principal.
//
// Formato: objeto { id, name, subtitle, infoTitle, info, verified, ecus: [...] }. En "info" se pueden usar
// los marcadores {iface} y {ecupath}. Un archivo <id>_catalog.json en la carpeta ECU del usuario sustituye
// al incrustado; ahí también vale un array simple de centralitas (solo cambia las centralitas).
public static class ChassisCatalog
{
    const string ResourcePrefix = "chassis.";
    const string DefaultInfoTitle = "Información";
    const string ResourceSuffix = "_catalog.json";

    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    // Los incrustados se parsean por id en el primer uso; _chassis solo guarda los que sustituye la carpeta ECU.
    static readonly Lazy<string[]> EmbeddedIds = new(() => typeof(ChassisCatalog).Assembly.GetManifestResourceNames()
        .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(ResourceSuffix, StringComparison.Ordinal))
        .Select(n => n[ResourcePrefix.Length..^ResourceSuffix.Length]).ToArray());
    static readonly ConcurrentDictionary<string, ChassisInfo?> Embedded = new();
    static IReadOnlyDictionary<string, ChassisInfo>? _chassis;

    /// <summary>Chasis por id; si no existe devuelve uno vacío (la UI lo muestra como catálogo vacío).</summary>
    public static ChassisInfo Get(string id) =>
        (_chassis != null && _chassis.TryGetValue(id, out var c) ? c : GetEmbedded(id))
        ?? new ChassisInfo(id, id.ToUpperInvariant(), "", DefaultInfoTitle, "", false, []);

    /// <summary>
    /// Sustituye los catálogos incrustados por los JSON de la carpeta ECU, si existen y son válidos. Parte siempre
    /// de los incrustados: un override borrado o de otra carpeta no se queda pegado al recargar.
    /// </summary>
    public static void TryLoadFromFolder(string ecuPath)
    {
        if (string.IsNullOrWhiteSpace(ecuPath))
        {
            _chassis = null;
            return;
        }
        var merged = new Dictionary<string, ChassisInfo>();
        foreach (var id in EmbeddedIds.Value)
        {
            string file = Path.Combine(ecuPath, id + ResourceSuffix);
            if (!File.Exists(file))
                continue;
            try
            {
                using var stream = File.OpenRead(file);
                merged[id] = Parse(stream, id, GetEmbedded(id));
            }
            catch (Exception)
            {
                // Error de parseo: se queda el catálogo incrustado sin romper la app.
            }
        }
        _chassis = merged;
    }

    static ChassisInfo? GetEmbedded(string id) =>
        Array.IndexOf(EmbeddedIds.Value, id) < 0 ? null : Embedded.GetOrAdd(id, LoadEmbedded);

    static ChassisInfo? LoadEmbedded(string id)
    {
        try
        {
            using var stream = typeof(ChassisCatalog).Assembly.GetManifestResourceStream(ResourcePrefix + id + ResourceSuffix)!;
            return Parse(stream, id, null);
        }
        catch (Exception)
        {
            return null;   // Recurso corrupto: se omite ese chasis.
        }
    }

    // El id sale siempre del nombre de archivo (<id>_catalog.json): es la clave con la que la UI pide el chasis,
    // así que un "id" distinto dentro del JSON no puede dejar el catálogo inaccesible.
    static ChassisInfo Parse(Stream stream, string id, ChassisInfo? fallback)
    {
        using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            if (fallback == null)
                throw new JsonException("Falta el objeto de chasis");
            var ecus = doc.RootElement.Deserialize<EcuDto[]>(Options);
            return ecus is { Length: > 0 } ? fallback with { Ecus = ecus.Select(ToEcu).ToList() } : fallback;
        }
        var dto = doc.RootElement.Deserialize<ChassisDto>(Options) ?? throw new JsonException("JSON vacío");
        return new ChassisInfo(id, !string.IsNullOrEmpty(dto.Name) ? dto.Name : id.ToUpperInvariant(), dto.Subtitle ?? "",
            !string.IsNullOrEmpty(dto.InfoTitle) ? dto.InfoTitle : DefaultInfoTitle, dto.Info ?? "", dto.Verified ?? fallback?.Verified ?? true,
            (dto.Ecus ?? []).Select(ToEcu).ToList());
    }

    // Un JSON editado a mano puede traer null explícitos: se normalizan aquí para que la UI nunca los vea.
    // Los valores de status se repiten en bucle sin confirmar, así que solo se admiten jobs de lectura.
    static ChassisEcu ToEcu(EcuDto d) => new(
        d.Title ?? "", d.Sgbd ?? "",
        (d.StatusPages ?? []).Select(p => new ChassisPage(p.Title ?? "",
            (p.Values ?? []).Where(v => JobPolicy.IsReadOnly(v.Job ?? ""))
                .Select(v => new ChassisValue(v.Job!, v.Result ?? "", v.Label ?? "", v.Unit ?? "", v.Args ?? "")).ToList())).ToList(),
        (d.Actions ?? []).Select(a => new ChassisAction(a.Title ?? "", a.Job ?? "", a.Args ?? "", a.Warning ?? "")).ToList(),
        ReadJobOr(d.IdentJob, "IDENT"), ReadJobOr(d.FsReadJob, "FS_LESEN"), d.FsClearJob ?? "FS_LOESCHEN");

    static string ReadJobOr(string? job, string fallback) => job != null && JobPolicy.IsReadOnly(job) ? job : fallback;

    sealed class ChassisDto
    {
        public string Id { get; init; } = "";   // solo informativo; manda el nombre de archivo
        public string Name { get; init; } = "";
        public string Subtitle { get; init; } = "";
        public string InfoTitle { get; init; } = "";
        public string Info { get; init; } = "";
        public bool? Verified { get; init; }
        public List<EcuDto> Ecus { get; init; } = [];
    }

    sealed class EcuDto
    {
        public string Title { get; init; } = "";
        public string Sgbd { get; init; } = "";
        public string IdentJob { get; init; } = "IDENT";
        public string FsReadJob { get; init; } = "FS_LESEN";
        public string FsClearJob { get; init; } = "FS_LOESCHEN";
        public List<PageDto> StatusPages { get; init; } = [];
        public List<ActionDto> Actions { get; init; } = [];
    }

    sealed class PageDto
    {
        public string Title { get; init; } = "";
        public List<ValueDto> Values { get; init; } = [];
    }

    sealed class ValueDto
    {
        public string Job { get; init; } = "";
        public string Result { get; init; } = "";
        public string Label { get; init; } = "";
        public string Unit { get; init; } = "";
        public string Args { get; init; } = "";
    }

    sealed class ActionDto
    {
        public string Title { get; init; } = "";
        public string Job { get; init; } = "";
        public string Args { get; init; } = "";
        public string Warning { get; init; } = "";
    }
}
