using InpaDroid.Diag;
using InpaDroid.Ui.Chassis;

namespace InpaDroid.Tests;

public sealed class ScanSummaryTests
{
    const string Failed = "The job failed";

    static JobResult Result(bool ok, string status, int faultSets, string error = "") => new()
    {
        Ok = ok,
        Error = error,
        JobStatus = status,
        Sets = Enumerable.Range(0, faultSets)
            .Select(i => (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["F_ORT_NR"] = i.ToString() })
            .ToList(),
    };

    [Fact]
    public void Classify_NoFaultSets_IsOk()
    {
        Assert.Equal((ScanState.Ok, 0, ""), ScanSummary.Classify(Result(true, "OKAY", 0), Failed));
    }

    [Fact]
    public void Classify_CountsFaultSets()
    {
        Assert.Equal((ScanState.Faults, 3, ""), ScanSummary.Classify(Result(true, "OKAY", 3), Failed));
    }

    [Fact]
    public void Classify_JobFailure_IsNoResponseWithDetail()
    {
        Assert.Equal((ScanState.NoResponse, 0, "boom"), ScanSummary.Classify(Result(false, "", 0, "boom"), Failed));
        Assert.Equal((ScanState.NoResponse, 0, Failed), ScanSummary.Classify(Result(false, "", 0), Failed));
        Assert.Equal((ScanState.NoResponse, 0, "JOB_STATUS: ERROR_ECU_REJECTED"),
            ScanSummary.Classify(Result(true, "ERROR_ECU_REJECTED", 2), Failed));
    }

    [Fact]
    public void NoVariantState_DependsOnFilePresence()
    {
        Assert.Equal(ScanState.NoResponse, ScanSummary.NoVariantState("D_0012", ["d_0012", "D_MOTOR"]));
        Assert.Equal(ScanState.NotFound, ScanSummary.NoVariantState("D_0012", ["D_MOTOR"]));
        Assert.Equal(ScanState.NotFound, ScanSummary.NoVariantState("D_0012", []));
    }

    [Fact]
    public void ToCsv_HeaderAndRows_UseCellEscaper()
    {
        var csv = ScanSummary.ToCsv(
        [
            new ScanEntry("Motor, DME", "D_MOTOR", "D_N52", ScanState.Faults, 2),
            new ScanEntry("ABS", "D_ABS", "", ScanState.NotFound),
            new ScanEntry("Airbag", "D_AB", "", ScanState.Pending),
        ], s => s.Contains(',') ? $"\"{s}\"" : s);
        Assert.Equal(
            "ecu,sgbd,variant,state,faults,detail\r\n" +
            "\"Motor, DME\",D_MOTOR,D_N52,faults,2,\r\n" +
            "ABS,D_ABS,,not_found,0,\r\n" +
            "Airbag,D_AB,,not_scanned,0,\r\n", csv);
    }
}
