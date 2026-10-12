using InpaDroid.Diag;

namespace InpaDroid.Tests;

public sealed class StatusResultTests
{
    const string Failed = "The job failed";

    static JobResult Result(string status, params (string Key, string Value)[][] sets) => new()
    {
        Ok = true,
        JobStatus = status,
        Sets = sets.Select(s => (IReadOnlyDictionary<string, string>)s.ToDictionary(p => p.Key, p => p.Value)).ToList(),
    };

    [Fact]
    public void StatusError_NullWhenOkay_AnyCase()
    {
        Assert.Null(DiagLogic.StatusError(Result("OKAY"), Failed));
        Assert.Null(DiagLogic.StatusError(Result("okay"), Failed));
        Assert.Null(DiagLogic.StatusError(Result(""), Failed));
    }

    [Fact]
    public void StatusError_ReportsOtherJobStatus()
    {
        Assert.Equal("JOB_STATUS: ERROR_ECU_REJECTED", DiagLogic.StatusError(Result("ERROR_ECU_REJECTED"), Failed));
    }

    [Fact]
    public void StatusError_FailedJob_UsesErrorOrFallback()
    {
        Assert.Equal("boom", DiagLogic.StatusError(new JobResult { Ok = false, Error = "boom" }, Failed));
        Assert.Equal(Failed, DiagLogic.StatusError(new JobResult { Ok = false }, Failed));
    }

    [Fact]
    public void FindResult_IgnoresCaseAndTrimsName()
    {
        var r = Result("OKAY", [("STAT_RPM_WERT", "850")]);
        Assert.Equal("850", DiagLogic.FindResult(r, "stat_rpm_wert"));
        Assert.Equal("850", DiagLogic.FindResult(r, "  STAT_RPM_WERT "));
    }

    [Fact]
    public void FindResult_SearchesAllSets_FirstMatchWins()
    {
        var r = Result("OKAY", [("A", "1")], [("B", "2"), ("A", "3")]);
        Assert.Equal("2", DiagLogic.FindResult(r, "B"));
        Assert.Equal("1", DiagLogic.FindResult(r, "A"));
    }

    [Fact]
    public void FindResult_NullWhenMissing()
    {
        Assert.Null(DiagLogic.FindResult(Result("OKAY", [("A", "1")]), "Z"));
        Assert.Null(DiagLogic.FindResult(new JobResult(), "A"));
    }
}
