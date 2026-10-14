using InpaDroid.Diag;

namespace InpaDroid.Tests;

public sealed class SessionRecordTests
{
    static IReadOnlyDictionary<string, string> Fault(string code, string text, string? present = "1", string? hlz = null)
    {
        var d = new Dictionary<string, string> { ["F_ORT_NR"] = code, ["F_ORT_TEXT"] = text };
        if (present != null) d["F_VORHANDEN"] = present;
        if (hlz != null) d["F_HLZ"] = hlz;
        return d;
    }

    static SessionRecord Rec(params string[] codes) => new()
    {
        Faults = codes.Select(c => new FaultEntry { Code = c, Description = "d" + c, State = "Active" }).ToList(),
    };

    [Fact]
    public void FromFaults_KeepsOnlyFaultSets()
    {
        var r = new JobResult
        {
            Ok = true,
            Sets = [Fault("12", "Sensor", "1", "3"), new Dictionary<string, string> { ["STATUS"] = "x" }, Fault("34", "Otro", "0")],
        };
        var s = SessionRecord.FromFaults(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), "e46", "DME", "MS43", SessionRecord.KindBackup, r);
        Assert.Equal(2, s.Faults.Count);
        Assert.Equal("12", s.Faults[0].Code);
        Assert.Equal("Active", s.Faults[0].State);
        Assert.Equal("3", s.Faults[0].Counter);
        Assert.Equal("History", s.Faults[1].State);
        Assert.Null(s.Faults[1].Counter);
        Assert.Equal("backup", s.Kind);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), s.WhenUtc);
    }

    [Fact]
    public void FromFaults_ConvertsLocalTimeToUtc()
    {
        var local = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Local);
        var s = SessionRecord.FromFaults(local, "", "", "", SessionRecord.KindRead, new JobResult());
        Assert.Equal(local.ToUniversalTime(), s.WhenUtc);
        Assert.Empty(s.Faults);
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var s = Rec("12", "34");
        s.When = "2026-01-02T03:04:05.0000000Z";
        s.Chassis = "e39";
        s.EcuTitle = "DME";
        s.Sgbd = "MS43";
        s.Faults[0].Counter = "5";
        var back = SessionRecord.FromJson(s.ToJson())!;
        Assert.Equal("e39", back.Chassis);
        Assert.Equal("MS43", back.Sgbd);
        Assert.Equal(2, back.Faults.Count);
        Assert.Equal("5", back.Faults[0].Counter);
        Assert.Equal(s.When, back.When);
    }

    [Fact]
    public void FromJson_IgnoresUnknownAndFillsMissing()
    {
        var s = SessionRecord.FromJson("""{"Chassis":"e46","future":{"a":1},"Faults":[{"Code":"7","extra":true}]}""")!;
        Assert.Equal("e46", s.Chassis);
        Assert.Equal("", s.Sgbd);
        Assert.Equal("read", s.Kind);
        Assert.Equal("7", s.Faults[0].Code);
        Assert.Equal("", s.Faults[0].Description);
    }

    [Fact]
    public void FromJson_NullValuesAreNormalized()
    {
        var s = SessionRecord.FromJson("""{"Chassis":null,"Faults":[null,{"Code":null}]}""")!;
        Assert.Equal("", s.Chassis);
        Assert.Single(s.Faults);
        Assert.Equal("", s.Faults[0].Code);
        Assert.Empty(SessionRecord.FromJson("""{"Faults":null}""")!.Faults);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("""{"Faults":"oops"}""")]
    [InlineData("""{"Chassis":5}""")]
    public void FromJson_CorruptOrEmpty_ReturnsNull(string? json) => Assert.Null(SessionRecord.FromJson(json));

    [Fact]
    public void WhenUtc_InvalidIsNull() => Assert.Null(new SessionRecord { When = "ayer" }.WhenUtc);

    [Fact]
    public void Compare_SplitsNewFixedPersistent()
    {
        var c = SessionRecord.Compare(Rec("1", "2", "3"), Rec("2", "3", "4"));
        Assert.Equal(["4"], c.New.Select(f => f.Code));
        Assert.Equal(["1"], c.Fixed.Select(f => f.Code));
        Assert.Equal(["2", "3"], c.Persistent.Select(f => f.Code));
    }

    [Fact]
    public void Compare_EmptyAfter_AllFixed()
    {
        var c = SessionRecord.Compare(Rec("1", "2"), Rec());
        Assert.Empty(c.New);
        Assert.Empty(c.Persistent);
        Assert.Equal(2, c.Fixed.Count);
    }

    [Fact]
    public void Compare_EmptyBefore_AllNew()
    {
        var c = SessionRecord.Compare(Rec(), Rec("1"));
        Assert.Single(c.New);
        Assert.Empty(c.Fixed);
    }
}
