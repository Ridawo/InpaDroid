using InpaDroid.Diag;

namespace InpaDroid.Tests;

public class EcuFolderPathTests
{
    [Theory]
    [InlineData("primary:Ecu", "/storage/emulated/0/Ecu")]
    [InlineData("primary:", "/storage/emulated/0")]
    [InlineData("primary", "/storage/emulated/0")]
    [InlineData("PRIMARY:Ecu", "/storage/emulated/0/Ecu")]
    [InlineData("primary:EDIABAS/Ecu", "/storage/emulated/0/EDIABAS/Ecu")]
    [InlineData("1A2B-3C4D:Ecu", "/storage/1A2B-3C4D/Ecu")]
    [InlineData("1a2b-3c4d:EDIABAS/Ecu", "/storage/1a2b-3c4d/EDIABAS/Ecu")]
    [InlineData("1A2B-3C4D:", "/storage/1A2B-3C4D")]
    [InlineData("1A2B-3C4D", "/storage/1A2B-3C4D")]
    [InlineData("primary:Ecu:viejo", "/storage/emulated/0/Ecu:viejo")]
    public void FromTreeDocumentId_MapsVolumes(string docId, string expected) =>
        Assert.Equal(expected, EcuFolderPath.FromTreeDocumentId(docId));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Ecu")]
    [InlineData(":Ecu")]
    [InlineData("downloads:Ecu")]
    [InlineData("home:Documentos")]
    [InlineData("raw:/storage/emulated/0/Download/Ecu")]
    [InlineData("1A2B3C4D:Ecu")]
    [InlineData("1A2B-3C4G:Ecu")]
    [InlineData("1A2B-3C4D5:Ecu")]
    public void FromTreeDocumentId_UnusableVolumeIsNull(string? docId) =>
        Assert.Null(EcuFolderPath.FromTreeDocumentId(docId));

    [Fact]
    public void FromTreeDocumentId_UsesGivenPrimaryRoot()
    {
        Assert.Equal("/mnt/sdcard/Ecu", EcuFolderPath.FromTreeDocumentId("primary:Ecu", "/mnt/sdcard"));
        Assert.Equal("/mnt/sdcard", EcuFolderPath.FromTreeDocumentId("primary:", "/mnt/sdcard"));
        Assert.Equal("/storage/1A2B-3C4D/Ecu", EcuFolderPath.FromTreeDocumentId("1A2B-3C4D:Ecu", "/mnt/sdcard"));
    }
}
