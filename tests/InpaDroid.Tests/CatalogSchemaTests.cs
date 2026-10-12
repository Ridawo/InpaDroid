using System.Text.Json.Nodes;
using InpaDroid.Diag;

namespace InpaDroid.Tests;

// Validación estructural de los catálogos incrustados (versión en español e inglés), según docs/catalog.schema.json.
// La paridad es/en ya la cubre ChassisCatalogEnglishTests.
public sealed class CatalogSchemaTests
{
    static readonly string[] ChassisKeys = ["id", "name", "subtitle", "infoTitle", "info", "verified", "ecus"];

    public static IEnumerable<object[]> Resources() =>
        typeof(InpaDroid.Ui.Chassis.ChassisCatalog).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("chassis.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => new object[] { n });

    static JsonObject Load(string resource)
    {
        using var s = typeof(InpaDroid.Ui.Chassis.ChassisCatalog).Assembly.GetManifestResourceStream(resource)!;
        return JsonNode.Parse(s, documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true })!.AsObject();
    }

    static string Str(JsonNode? n) => n?.GetValue<string>() ?? "";

    static IEnumerable<JsonObject> Objects(JsonNode? array) => (array?.AsArray() ?? []).Select(n => n!.AsObject());

    [Fact]
    public void Resources_AreFound() => Assert.NotEmpty(Resources());

    [Theory]
    [MemberData(nameof(Resources))]
    public void Chassis_HasRequiredKeys(string resource)
    {
        var chassis = Load(resource);
        foreach (var key in ChassisKeys)
            Assert.True(chassis.ContainsKey(key), $"{resource}: falta '{key}'");
        Assert.False(string.IsNullOrWhiteSpace(Str(chassis["name"])), $"{resource}: name vacío");
        Assert.False(string.IsNullOrWhiteSpace(Str(chassis["infoTitle"])), $"{resource}: infoTitle vacío");
        Assert.NotEmpty(chassis["ecus"]!.AsArray());
    }

    [Theory]
    [MemberData(nameof(Resources))]
    public void Ecus_HaveTitleAndUniqueSgbd(string resource)
    {
        var ecus = Objects(Load(resource)["ecus"]).ToList();
        foreach (var ecu in ecus)
        {
            Assert.False(string.IsNullOrWhiteSpace(Str(ecu["title"])), $"{resource}: ECU sin title");
            Assert.False(string.IsNullOrWhiteSpace(Str(ecu["sgbd"])), $"{resource}: ECU '{Str(ecu["title"])}' sin sgbd");
        }
        var sgbd = ecus.Select(e => Str(e["sgbd"]).ToUpperInvariant()).ToList();
        Assert.Equal(sgbd.Count, sgbd.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Resources))]
    public void StatusValues_AreCompleteAndReadOnly(string resource)
    {
        foreach (var ecu in Objects(Load(resource)["ecus"]))
            foreach (var page in Objects(ecu["statusPages"]))
            {
                Assert.False(string.IsNullOrWhiteSpace(Str(page["title"])), $"{resource}/{Str(ecu["sgbd"])}: página sin title");
                foreach (var v in Objects(page["values"]))
                {
                    string where = $"{resource}/{Str(ecu["sgbd"])}/{Str(page["title"])}/{Str(v["job"])}";
                    Assert.False(string.IsNullOrWhiteSpace(Str(v["job"])), $"{where}: job vacío");
                    Assert.False(string.IsNullOrWhiteSpace(Str(v["result"])), $"{where}: result vacío");
                    Assert.False(string.IsNullOrWhiteSpace(Str(v["label"])), $"{where}: label vacío");
                    Assert.True(JobPolicy.IsReadOnly(Str(v["job"])), $"{where}: el job de status no es de solo lectura");
                }
            }
    }

    [Theory]
    [MemberData(nameof(Resources))]
    public void Actions_HaveJobAndWarningUnlessReadOnly(string resource)
    {
        foreach (var ecu in Objects(Load(resource)["ecus"]))
            foreach (var a in Objects(ecu["actions"]))
            {
                string where = $"{resource}/{Str(ecu["sgbd"])}/{Str(a["title"])}";
                Assert.False(string.IsNullOrWhiteSpace(Str(a["title"])), $"{where}: title vacío");
                Assert.False(string.IsNullOrWhiteSpace(Str(a["job"])), $"{where}: job vacío");
                if (!JobPolicy.IsReadOnly(Str(a["job"])))
                    Assert.False(string.IsNullOrWhiteSpace(Str(a["warning"])), $"{where}: acción de escritura sin warning");
            }
    }
}
