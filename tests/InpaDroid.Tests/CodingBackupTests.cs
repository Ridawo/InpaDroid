using InpaDroid.Diag;

namespace InpaDroid.Tests;

public sealed class CodingBackupTests
{
    static CodingBackup.Snapshot Snap(string when, string sgbd, string job, params (string Key, string Value)[] entries) => new()
    {
        When = when, Sgbd = sgbd, Job = job,
        Entries = entries.Select(e => new CodingBackup.Entry { Key = e.Key, Value = e.Value }).ToList(),
    };

    [Theory]
    [InlineData("C_FA_LESEN")]
    [InlineData("C_FG_LESEN")]
    [InlineData("C_C_LESEN")]
    [InlineData("FA_LESEN")]
    [InlineData("AIF_LESEN")]
    [InlineData("ZIF_LESEN")]
    [InlineData("ZIF_BACKUP_LESEN")]
    [InlineData("CODIERDATEN_LESEN")]
    [InlineData("STEUERGERAET_CODIERUNG_LESEN")]
    [InlineData(" c_fa_lesen")]
    public void IsCodingReadJob_RecognisesReads(string job) => Assert.True(CodingBackup.IsCodingReadJob(job));

    [Theory]
    [InlineData("C_FA_SCHREIBEN")]
    [InlineData("CODIERDATEN_SCHREIBEN")]
    [InlineData("CODIERUNG_LESEN_LOESCHEN")]
    [InlineData("FA_LESEN_VORGEBEN")]
    [InlineData("AIF_LESEN_SETZEN")]
    [InlineData("ZIF_LESEN_RESET")]
    [InlineData("C_FA_LESEN_STEUERN")]
    [InlineData("CODIERUNG")]
    [InlineData("FS_LESEN")]
    [InlineData("STATUS_LESEN")]
    [InlineData("")]
    public void IsCodingReadJob_RejectsWritesAndOthers(string job) => Assert.False(CodingBackup.IsCodingReadJob(job));

    [Fact]
    public void FromResult_FlattensAllSetsWithIndex()
    {
        var r = new JobResult
        {
            Ok = true,
            Sets =
            [
                new Dictionary<string, string> { ["FA"] = "S205A", ["BAUREIHE"] = "E46" },
                new Dictionary<string, string> { ["JOB_STATUS"] = "OKAY" },
            ],
        };
        var s = CodingBackup.FromResult(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), "e46", "LSZ", "LSZ_E46", "C_FA_LESEN", r);
        Assert.Equal(new[] { "1:FA", "1:BAUREIHE", "2:JOB_STATUS" }, s.Entries.Select(e => e.Key));
        Assert.Equal("S205A", s.Entries[0].Value);
        Assert.Equal("2026-01-02T03:04:05.0000000Z", s.When);
        Assert.Equal("C_FA_LESEN", s.Job);
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var s = Snap("2026-01-02T03:04:05.0000000Z", "LSZ_E46", "C_FA_LESEN", ("1:FA", "S205A"), ("1:X", "a,\"b\""));
        s.Chassis = "e46";
        s.EcuTitle = "LSZ";
        var back = CodingBackup.FromJson(s.ToJson())!;
        Assert.Equal("e46", back.Chassis);
        Assert.Equal("LSZ", back.EcuTitle);
        Assert.Equal("LSZ_E46", back.Sgbd);
        Assert.Equal("C_FA_LESEN", back.Job);
        Assert.Equal(s.When, back.When);
        Assert.Equal(2, back.Entries.Count);
        Assert.Equal("a,\"b\"", back.Entries[1].Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    [InlineData("null")]
    public void FromJson_CorruptReturnsNull(string? json) => Assert.Null(CodingBackup.FromJson(json));

    [Fact]
    public void FromJson_FillsMissingFields()
    {
        var s = CodingBackup.FromJson("{\"Sgbd\":null,\"Entries\":[null,{\"Key\":\"1:A\"}],\"Extra\":1}")!;
        Assert.Equal("", s.Sgbd);
        Assert.Equal("", s.When);
        var e = Assert.Single(s.Entries);
        Assert.Equal("1:A", e.Key);
        Assert.Equal("", e.Value);
        Assert.Empty(CodingBackup.FromJson("{}")!.Entries);
    }

    [Fact]
    public void Diff_FindsChangedAddedRemoved()
    {
        var before = Snap("a", "S", "J", ("1:A", "1"), ("1:B", "2"), ("1:C", "3"));
        var after = Snap("b", "S", "J", ("1:A", "1"), ("1:B", "20"), ("1:D", "4"));
        var d = CodingBackup.Diff(before, after);
        var c = Assert.Single(d.Changed);
        Assert.Equal(new CodingBackup.Change("1:B", "2", "20"), c);
        Assert.Equal("1:D", Assert.Single(d.Added).Key);
        Assert.Equal("1:C", Assert.Single(d.Removed).Key);
    }

    [Fact]
    public void Diff_IdenticalIsEmpty()
    {
        var d = CodingBackup.Diff(Snap("a", "S", "J", ("1:A", "1")), Snap("b", "S", "J", ("1:A", "1")));
        Assert.Empty(d.Changed);
        Assert.Empty(d.Added);
        Assert.Empty(d.Removed);
    }

    [Fact]
    public void Previous_SameSgbdAndJobAndOlder()
    {
        var cur = Snap("2026-03-01T00:00:00.0000000Z", "LSZ", "C_FA_LESEN");
        var all = new[]
        {
            cur,
            Snap("2026-01-01T00:00:00.0000000Z", "LSZ", "C_FA_LESEN"),
            Snap("2026-02-01T00:00:00.0000000Z", "lsz", "c_fa_lesen"),
            Snap("2026-02-15T00:00:00.0000000Z", "LSZ", "AIF_LESEN"),
            Snap("2026-02-20T00:00:00.0000000Z", "DME", "C_FA_LESEN"),
            Snap("2026-04-01T00:00:00.0000000Z", "LSZ", "C_FA_LESEN"),
        };
        Assert.Same(all[2], CodingBackup.Previous(all, cur));
        Assert.Null(CodingBackup.Previous(all, all[1]));
    }

    [Fact]
    public void Previous_DifferentArgsAreNotCompared()
    {
        var cur = Snap("2026-03-01T00:00:00.0000000Z", "LSZ", "C_FA_LESEN");
        cur.Args = "2";
        var other = Snap("2026-02-01T00:00:00.0000000Z", "LSZ", "C_FA_LESEN");
        other.Args = "1";
        var same = Snap("2026-01-01T00:00:00.0000000Z", "LSZ", "C_FA_LESEN");
        same.Args = "2";
        Assert.Same(same, CodingBackup.Previous([other, same], cur));
        Assert.Null(CodingBackup.Previous([other], cur));
    }

    [Fact]
    public void FromJson_OldFileWithoutArgsLoadsWithEmptyArgs()
    {
        var s = CodingBackup.FromJson("{\"Sgbd\":\"S\",\"Job\":\"J\",\"Entries\":[]}")!;
        Assert.Equal("", s.Args);
        Assert.Equal("", CodingBackup.FromJson("{\"Args\":null}")!.Args);
    }
}
