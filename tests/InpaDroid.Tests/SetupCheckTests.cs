using InpaDroid.Diag;

namespace InpaDroid.Tests;

public class SetupCheckTests
{
    [Theory]
    [InlineData(true, 3, true)]
    [InlineData(true, 0, false)]
    [InlineData(false, 3, false)]
    [InlineData(false, 0, false)]
    public void IsComplete_NeedsAdapterAndFiles(bool adapter, int files, bool expected) =>
        Assert.Equal(expected, SetupCheck.IsComplete(adapter, files));

    [Fact]
    public void RequiredSgbds_AddsUtilityDedupesAndUppercases()
    {
        var r = SetupCheck.RequiredSgbds(["d_motor", "D_MOTOR", " ", "d_0012", "utility"]);
        Assert.Equal(["UTILITY", "D_MOTOR", "D_0012"], r);
    }

    [Fact]
    public void Missing_IsCaseInsensitive()
    {
        var present = new HashSet<string> { "UTILITY", "D_MOTOR" };
        Assert.Equal(["D_0012"], SetupCheck.Missing(["UTILITY", "d_motor", "D_0012"], present));
    }

    [Fact]
    public void ListEcuNames_ReadsOnlyPrgAndGrp()
    {
        string dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "Utility.PRG"), "");
            File.WriteAllText(Path.Combine(dir, "d_motor.grp"), "");
            File.WriteAllText(Path.Combine(dir, "notes.txt"), "");
            var names = SetupCheck.ListEcuNames(dir);
            Assert.Equal(new[] { "D_MOTOR", "UTILITY" }, names.Order());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ListEcuNames_MissingFolder_IsEmpty() =>
        Assert.Empty(SetupCheck.ListEcuNames(Path.Combine(Path.GetTempPath(), "inpadroid_no_such_dir")));

    [Theory]
    [InlineData(false, true, 12.0, true, ConnectionFault.Adapter)]
    [InlineData(true, false, 12.0, true, ConnectionFault.EcuFiles)]
    [InlineData(true, true, null, null, ConnectionFault.NoReply)]
    [InlineData(true, true, 12.1, false, ConnectionFault.IgnitionOff)]
    [InlineData(true, true, 12.1, true, ConnectionFault.None)]
    [InlineData(true, true, 12.1, null, ConnectionFault.None)]
    public void Diagnose_PicksFirstFailingLink(bool adapter, bool utility, double? ubatt, bool? ign, ConnectionFault expected) =>
        Assert.Equal(expected, SetupCheck.Diagnose(adapter, utility, ubatt, ign));
}
