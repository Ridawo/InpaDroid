using InpaDroid.Diag;

namespace InpaDroid.Tests;

public sealed class EcuImportTests
{
    [Theory]
    [InlineData("D_0012.prg", "D_0012.prg")]
    [InlineData("D_0012.PRG", "D_0012.PRG")]
    [InlineData("GRUPPE.Grp", "GRUPPE.Grp")]
    [InlineData("Ecu/sub/D_0012.prg", "D_0012.prg")]
    [InlineData("Ecu\\sub\\D_0012.prg", "D_0012.prg")]
    [InlineData("../../evil/D_0012.prg", "D_0012.prg")]   // zip-slip: solo cuenta el último segmento
    [InlineData("/abs/path/X.prg", "X.prg")]
    public void AcceptedName_UsesLastSegmentOnly(string path, string expected) =>
        Assert.Equal(expected, EcuImport.AcceptedName(path));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a.ipo")]
    [InlineData("a.exe")]
    [InlineData("a.prg.exe")]
    [InlineData("readme.txt")]
    [InlineData(".prg")]
    [InlineData("dir/")]
    [InlineData("..")]
    [InlineData("dir/..")]
    [InlineData("__MACOSX/._a.prg")]
    [InlineData("a:b.prg")]
    [InlineData("a\0b.prg")]
    public void AcceptedName_RejectsOthers(string? path) => Assert.Null(EcuImport.AcceptedName(path));

    [Fact]
    public void AcceptedName_RejectsTooLongName() =>
        Assert.Null(EcuImport.AcceptedName(new string('a', 252) + ".prg"));

    [Theory]
    [InlineData("pack.zip", true)]
    [InlineData("PACK.ZIP", true)]
    [InlineData("a.prg", false)]
    [InlineData(null, false)]
    public void IsZip_ByExtension(string? name, bool expected) => Assert.Equal(expected, EcuImport.IsZip(name));

    [Fact]
    public void CaseCollision_DifferentCase_ReturnsExisting() =>
        Assert.Equal("d_0012.prg", EcuImport.CaseCollision("D_0012.prg", ["d_0012.prg", "other.prg"]));

    [Fact]
    public void CaseCollision_SameName_IsNotCollision() =>
        Assert.Null(EcuImport.CaseCollision("D_0012.prg", ["D_0012.prg"]));

    [Fact]
    public void CaseCollision_NoMatch_ReturnsNull() =>
        Assert.Null(EcuImport.CaseCollision("A.prg", ["B.prg"]));

    [Fact]
    public void AllowedBytes_CapsByFileAndRemainingTotal()
    {
        Assert.Equal(EcuImport.MaxFileBytes, EcuImport.AllowedBytes(0));
        Assert.Equal(1000, EcuImport.AllowedBytes(EcuImport.MaxTotalBytes - 1000));
        Assert.Equal(0, EcuImport.AllowedBytes(EcuImport.MaxTotalBytes));
        Assert.Equal(0, EcuImport.AllowedBytes(EcuImport.MaxTotalBytes + 5));
    }

    [Fact]
    public void ImportSummary_Fail_CountsAndKeepsFirstError()
    {
        var s = new ImportSummary();
        s.Fail("a.prg", "uno");
        s.Fail("b.prg", "dos");
        Assert.Equal(2, s.Failed);
        Assert.Equal("a.prg: uno", s.FirstError);
    }
}
