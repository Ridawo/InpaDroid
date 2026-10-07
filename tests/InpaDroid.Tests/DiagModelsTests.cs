using InpaDroid.Diag;

namespace InpaDroid.Tests;

public sealed class DiagModelsTests
{
    [Fact]
    public void JobResult_Defaults_AreEmptyAndNotOk()
    {
        var r = new JobResult();
        Assert.False(r.Ok);
        Assert.Equal("", r.Error);
        Assert.Equal("", r.JobStatus);
        Assert.Empty(r.Sets);
    }

    [Fact]
    public void JobInfo_And_EcuFile_Defaults()
    {
        var j = new JobInfo();
        Assert.Equal("", j.Name);
        Assert.Empty(j.Arguments);
        Assert.Empty(j.Results);
        var e = new EcuFile();
        Assert.False(e.IsGroup);
        Assert.Equal("", e.FullPath);
    }
}
