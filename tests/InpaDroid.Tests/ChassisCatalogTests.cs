using System.Reflection;
using InpaDroid.Ui.Chassis;

namespace InpaDroid.Tests;

// ChassisCatalog keeps its catalogs in static state: tests that load JSON must not run in parallel.
[Collection("ChassisCatalog")]
public sealed class ChassisCatalogTests : IDisposable
{
    private static readonly FieldInfo Field =
        typeof(ChassisCatalog).GetField("_chassis", BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly object _defaults = Field.GetValue(null)!;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "inpadroid-tests-" + Guid.NewGuid().ToString("N"));

    public ChassisCatalogTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Field.SetValue(null, _defaults);
        Directory.Delete(_dir, true);
    }

    [Theory]
    [InlineData("e39")]
    [InlineData("e46")]
    [InlineData("e60")]
    [InlineData("e90")]
    public void EmbeddedCatalog_IsWellFormed(string id)
    {
        ChassisInfo c = ChassisCatalog.Get(id);
        Assert.Equal(id, c.Id);
        Assert.NotEmpty(c.Ecus);
        foreach (ChassisEcu ecu in c.Ecus)
        {
            Assert.False(string.IsNullOrWhiteSpace(ecu.Title));
            Assert.False(string.IsNullOrWhiteSpace(ecu.Sgbd));
            Assert.DoesNotContain('.', ecu.Sgbd);
            foreach (ChassisPage page in ecu.StatusPages)
                foreach (ChassisValue v in page.Values)
                {
                    Assert.False(string.IsNullOrWhiteSpace(v.Job));
                    Assert.False(string.IsNullOrWhiteSpace(v.Result));
                }
        }
    }

    [Fact]
    public void UnknownChassis_ReturnsEmptyCatalog() => Assert.Empty(ChassisCatalog.Get("zzz").Ecus);

    [Fact]
    public void FolderOverride_ArrayReplacesEcus_KeepsMetadata()
    {
        string name = ChassisCatalog.Get("e46").Name;
        File.WriteAllText(Path.Combine(_dir, "e46_catalog.json"), """[ { "title": "Test", "sgbd": "D_TEST", }, ]""");
        ChassisCatalog.TryLoadFromFolder(_dir);
        ChassisInfo c = ChassisCatalog.Get("e46");
        Assert.Equal(name, c.Name);
        Assert.Equal("D_TEST", Assert.Single(c.Ecus).Sgbd);
    }

    [Fact]
    public void FolderOverride_IdMismatch_StaysUnderFileId()
    {
        File.WriteAllText(Path.Combine(_dir, "e46_catalog.json"),
            """{ "id": "x46", "name": "Custom", "ecus": [ { "title": "Test", "sgbd": "D_TEST" } ] }""");
        ChassisCatalog.TryLoadFromFolder(_dir);
        ChassisInfo c = ChassisCatalog.Get("e46");
        Assert.Equal("e46", c.Id);
        Assert.Equal("Custom", c.Name);
        Assert.Equal("D_TEST", Assert.Single(c.Ecus).Sgbd);
    }

    [Fact]
    public void FolderOverride_RemovedFile_RestoresEmbedded()
    {
        int count = ChassisCatalog.Get("e39").Ecus.Count;
        string file = Path.Combine(_dir, "e39_catalog.json");
        File.WriteAllText(file, """[ { "title": "Test", "sgbd": "D_TEST" } ]""");
        ChassisCatalog.TryLoadFromFolder(_dir);
        Assert.Single(ChassisCatalog.Get("e39").Ecus);
        File.Delete(file);
        ChassisCatalog.TryLoadFromFolder(_dir);
        Assert.Equal(count, ChassisCatalog.Get("e39").Ecus.Count);
    }

    [Fact]
    public void FolderOverride_InvalidJson_KeepsEmbedded()
    {
        int count = ChassisCatalog.Get("e39").Ecus.Count;
        File.WriteAllText(Path.Combine(_dir, "e39_catalog.json"), "{ not json");
        ChassisCatalog.TryLoadFromFolder(_dir);
        Assert.Equal(count, ChassisCatalog.Get("e39").Ecus.Count);
    }

    [Fact]
    public void FolderOverride_MissingOrBlankPath_IsIgnored()
    {
        int count = ChassisCatalog.Get("e39").Ecus.Count;
        ChassisCatalog.TryLoadFromFolder("");
        ChassisCatalog.TryLoadFromFolder(Path.Combine(_dir, "nope"));
        Assert.Equal(count, ChassisCatalog.Get("e39").Ecus.Count);
    }

    [Fact]
    public void FolderOverride_DropsNonReadOnlyStatusJobs_AndToleratesNulls()
    {
        File.WriteAllText(Path.Combine(_dir, "e46_catalog.json"), """
            { "ecus": [ { "title": null, "sgbd": "D_TEST", "statusPages": [ { "title": "P", "values": [
                { "job": "STATUS_X", "result": "A" }, { "job": "STEUERN_X", "result": "B" }, { "job": null, "result": "C" } ] } ] } ] }
            """);
        ChassisCatalog.TryLoadFromFolder(_dir);
        ChassisEcu ecu = Assert.Single(ChassisCatalog.Get("e46").Ecus);
        Assert.Equal("", ecu.Title);
        Assert.Equal("STATUS_X", Assert.Single(Assert.Single(ecu.StatusPages).Values).Job);
    }

    [Fact]
    public void FolderOverride_NonReadOnlyIdentAndFsReadJobs_FallBackToDefaults()
    {
        File.WriteAllText(Path.Combine(_dir, "e46_catalog.json"), """
            { "ecus": [ { "title": "T", "sgbd": "D_TEST", "identJob": "STEUERN_X", "fsReadJob": "FS_LOESCHEN" },
                        { "title": "U", "sgbd": "D_TEST", "identJob": "INFO", "fsReadJob": "FS_LESEN_DETAIL" } ] }
            """);
        ChassisCatalog.TryLoadFromFolder(_dir);
        var ecus = ChassisCatalog.Get("e46").Ecus;
        Assert.Equal("IDENT", ecus[0].IdentJob);
        Assert.Equal("FS_LESEN", ecus[0].FsReadJob);
        Assert.Equal("INFO", ecus[1].IdentJob);
        Assert.Equal("FS_LESEN_DETAIL", ecus[1].FsReadJob);
    }
}
