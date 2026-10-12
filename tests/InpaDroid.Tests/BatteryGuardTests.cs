using InpaDroid.Diag;

namespace InpaDroid.Tests;

public sealed class BatteryGuardTests
{
    [Theory]
    [InlineData(12.6)]
    [InlineData(14.2)]
    [InlineData(11.5)]   // el mínimo exacto se acepta
    public void Check_AtOrAboveMinimum_IsOk(double volts) => Assert.Equal(BatteryGuard.State.Ok, BatteryGuard.Check(volts));

    [Theory]
    [InlineData(11.49)]
    [InlineData(10.0)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Check_BelowMinimum_IsLow(double volts) => Assert.Equal(BatteryGuard.State.Low, BatteryGuard.Check(volts));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Check_NotANumber_IsUnknown(double volts) => Assert.Equal(BatteryGuard.State.Unknown, BatteryGuard.Check(volts));

    [Fact]
    public void Check_NoReading_IsUnknown() => Assert.Equal(BatteryGuard.State.Unknown, BatteryGuard.Check(null));

    [Theory]
    [InlineData("on  12,3 V", 12.3)]   // formato español
    [InlineData("on  12.3 V", 12.3)]   // formato inglés
    [InlineData(" on  11,5 V ", 11.5)]
    [InlineData("on  9 V", 9.0)]
    [InlineData("off", 0.0)]
    [InlineData("OFF", 0.0)]
    public void ParseHeader_ReadsVoltage(string text, double expected) =>
        Assert.Equal(expected, BatteryGuard.ParseHeader(text)!.Value, 3);

    [Theory]
    [InlineData("--")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("on")]
    public void ParseHeader_WithoutReading_IsNull(string? text) => Assert.Null(BatteryGuard.ParseHeader(text));

    [Theory]
    [InlineData("on  12,6 V", BatteryGuard.State.Ok)]
    [InlineData("on  11,4 V", BatteryGuard.State.Low)]
    [InlineData("off", BatteryGuard.State.Low)]
    [InlineData("--", BatteryGuard.State.Unknown)]
    public void HeaderText_GivesState(string text, BatteryGuard.State expected) =>
        Assert.Equal(expected, BatteryGuard.Check(BatteryGuard.ParseHeader(text)));
}
