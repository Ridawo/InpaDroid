using InpaDroid.Diag;

namespace InpaDroid.Tests;

public sealed class FaultInfoTests
{
    static IReadOnlyDictionary<string, string> Set(params (string Key, string Value)[] p) =>
        p.ToDictionary(x => x.Key, x => x.Value);

    [Fact]
    public void IsFault_NeedsFOrtNr()
    {
        Assert.True(FaultInfo.IsFault(Set(("F_ORT_NR", "12"))));
        Assert.False(FaultInfo.IsFault(Set(("STAT_X", "1"))));
    }

    [Fact]
    public void Parse_ReadsCodeDescriptionCounter()
    {
        var f = FaultInfo.Parse(Set(("F_ORT_NR", "12"), ("F_ORT_TEXT", " Lambda sensor "), ("F_HLZ", "3")));
        Assert.Equal("12", f.Code);
        Assert.Equal("Lambda sensor", f.Description);
        Assert.Equal("3", f.Counter);
    }

    [Fact]
    public void Parse_CodeIsFaultNumberNotHexRecord()
    {
        Assert.Equal("12", FaultInfo.Parse(Set(("F_ORT_NR", "12"), ("F_HEX_CODE", "0x2A"))).Code);
    }

    [Theory]
    [InlineData("1", FaultState.Active)]
    [InlineData("0", FaultState.History)]
    [InlineData("", FaultState.Unknown)]
    public void Parse_StateFromVorhanden(string value, FaultState expected)
    {
        Assert.Equal(expected, FaultInfo.Parse(Set(("F_ORT_NR", "1"), ("F_VORHANDEN", value))).State);
    }

    [Fact]
    public void Parse_NoVorhanden_IsUnknownAndNoCounter()
    {
        var f = FaultInfo.Parse(Set(("F_ORT_NR", "1")));
        Assert.Equal(FaultState.Unknown, f.State);
        Assert.Null(f.Counter);
    }

    [Fact]
    public void Parse_GroupsConditionsByIndexNumerically()
    {
        var f = FaultInfo.Parse(Set(
            ("F_ORT_NR", "1"),
            ("F_UW10_TEXT", "Temp"), ("F_UW10_WERT", "90"), ("F_UW10_EINH", "C"),
            ("F_UW2_TEXT", "RPM"), ("F_UW2_WERT", "850"), ("F_UW2_EINH", "1/min")));
        Assert.Equal([new FaultCondition("RPM", "850", "1/min"), new FaultCondition("Temp", "90", "C")], f.Conditions);
    }

    [Fact]
    public void Parse_SkipsEmptyConditions()
    {
        var f = FaultInfo.Parse(Set(("F_ORT_NR", "1"), ("F_UW1_TEXT", ""), ("F_UW1_WERT", ""), ("F_UW2_TEXT", "Speed")));
        Assert.Equal([new FaultCondition("Speed", "", "")], f.Conditions);
    }
}
