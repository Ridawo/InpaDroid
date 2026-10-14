using System.Text.Json.Nodes;

namespace InpaDroid.Tests;

public sealed class ChassisCatalogEnglishTests
{
    // Campos de texto que se traducen; todo lo demás (jobs, SGBD, args, unidades) debe ser idéntico.
    static readonly HashSet<string> TextKeys = ["name", "subtitle", "infoTitle", "info", "title", "label", "warning"];

    static JsonNode Load(string resource)
    {
        using var s = typeof(InpaDroid.Ui.Chassis.ChassisCatalog).Assembly.GetManifestResourceStream(resource)!;
        return JsonNode.Parse(s, documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true })!;
    }

    static void AssertSameStructure(JsonNode es, JsonNode en, string path)
    {
        switch (es)
        {
            case JsonObject eo:
                var no = Assert.IsType<JsonObject>(en);
                Assert.Equal(eo.Select(p => p.Key), no.Select(p => p.Key));
                foreach (var (key, value) in eo)
                {
                    if (TextKeys.Contains(key) && value is JsonValue)
                        Assert.False(string.IsNullOrWhiteSpace(no[key]!.GetValue<string>()), $"{path}.{key} vacío");
                    else if (value != null)
                        AssertSameStructure(value, no[key]!, $"{path}.{key}");
                }
                break;
            case JsonArray ea:
                var na = Assert.IsType<JsonArray>(en);
                Assert.Equal(ea.Count, na.Count);
                for (int i = 0; i < ea.Count; i++)
                    AssertSameStructure(ea[i]!, na[i]!, $"{path}[{i}]");
                break;
            default:
                Assert.Equal(es.ToJsonString(), en.ToJsonString());
                break;
        }
    }

    [Theory]
    [InlineData("e39")]
    [InlineData("e46")]
    [InlineData("e60")]
    [InlineData("e90")]
    [InlineData("f30")]
    [InlineData("f10")]
    public void EnglishCatalog_HasSameStructureAsSpanish(string id) =>
        AssertSameStructure(Load($"chassis.{id}_catalog.json"), Load($"chassis.{id}_catalog.en.json"), id);

    [Theory]
    [InlineData("e39")]
    [InlineData("e46")]
    public void Loader_PicksEnglishOnlyForEnglishLanguage(string id)
    {
        var es = InpaDroid.Ui.Chassis.ChassisCatalog.LoadEmbedded(id, "es")!;
        var en = InpaDroid.Ui.Chassis.ChassisCatalog.LoadEmbedded(id, "en")!;
        var fr = InpaDroid.Ui.Chassis.ChassisCatalog.LoadEmbedded(id, "fr")!;
        Assert.NotEqual(es.Ecus[0].Title, en.Ecus[0].Title);
        Assert.Equal(es.Ecus[0].Title, fr.Ecus[0].Title);
        Assert.Equal(es.Ecus[0].Sgbd, en.Ecus[0].Sgbd);
    }
}
