using InpaDroid.Diag;

namespace InpaDroid.Tests;

public class DiagLogicTests
{
    sealed class FakeEdiabas(string code, Exception? inner = null) : Exception("ediabas", inner)
    {
        public string Code { get; } = code;
    }

    static string? CodeOf(Exception e) => (e as FakeEdiabas)?.Code;

    static bool Adapter(Exception e) => DiagLogic.IsAdapterConnectionError(e, CodeOf);

    [Theory]
    [InlineData("EDIABAS_IFH_0001")]
    [InlineData("EDIABAS_IFH_0002")]
    [InlineData("EDIABAS_IFH_0003")]
    [InlineData("EDIABAS_IFH_0017")]
    [InlineData("EDIABAS_IFH_0018")]
    [InlineData("EDIABAS_IFH_0019")]
    public void AdapterIfhCodes_AreAdapterErrors(string code) => Assert.True(Adapter(new FakeEdiabas(code)));

    [Theory]
    [InlineData("EDIABAS_IFH_0008")]
    [InlineData("EDIABAS_IFH_0009")]
    [InlineData("EDIABAS_IFH_0010")]
    [InlineData("EDIABAS_BIP_0001")]
    public void EcuOrOtherCodes_AreNotAdapterErrors(string code) => Assert.False(Adapter(new FakeEdiabas(code)));

    [Fact]
    public void IoAndSocketExceptions_AreAdapterErrors()
    {
        Assert.True(Adapter(new IOException("x")));
        Assert.True(Adapter(new System.Net.Sockets.SocketException()));
    }

    [Fact]
    public void InnerExceptionsAreWalked()
    {
        Assert.True(Adapter(new InvalidOperationException("w", new IOException())));
        Assert.True(Adapter(new FakeEdiabas("EDIABAS_IFH_0008", new FakeEdiabas("EDIABAS_IFH_0002"))));
        Assert.False(Adapter(new InvalidOperationException("w", new ArgumentException())));
    }

    [Fact]
    public void EdiabasExceptionWithEcuCode_DoesNotFallBackToIoRule()
    {
        Assert.False(Adapter(new FakeEdiabas("EDIABAS_IFH_0009")));
    }

    // Ejecuta RunWithReconnect con un work que falla `failures` veces con `ex`.
    static (int result, int calls, int prepares, int resets) Run(
        int failures, Exception ex, bool allow = true, bool canRetry = true)
    {
        int calls = 0, prepares = 0, resets = 0;
        int result = DiagLogic.RunWithReconnect(
            () => ++calls <= failures ? throw ex : 42,
            allow, Adapter, () => canRetry, () => prepares++, () => resets++);
        return (result, calls, prepares, resets);
    }

    [Fact]
    public void Success_RunsOnce()
    {
        var r = Run(0, new IOException());
        Assert.Equal((42, 1, 1, 0), r);
    }

    [Fact]
    public void AdapterError_RetriesExactlyOnce()
    {
        var r = Run(1, new IOException());
        Assert.Equal((42, 2, 2, 1), r);
    }

    [Fact]
    public void AdapterError_SecondFailurePropagates()
    {
        int calls = 0, resets = 0;
        Assert.Throws<IOException>(() => DiagLogic.RunWithReconnect<int>(
            () => { calls++; throw new IOException(); },
            true, Adapter, () => true, () => { }, () => resets++));
        Assert.Equal(2, calls);
        Assert.Equal(1, resets);
    }

    [Fact]
    public void NonAdapterError_DoesNotRetry()
    {
        Assert.Throws<FakeEdiabas>(() => Run(1, new FakeEdiabas("EDIABAS_IFH_0009")));
        Assert.Throws<InvalidOperationException>(() => Run(1, new InvalidOperationException()));
    }

    [Fact]
    public void AllowReconnectFalse_DoesNotRetry()
    {
        int resets = 0, calls = 0;
        Assert.Throws<IOException>(() => DiagLogic.RunWithReconnect<int>(
            () => { calls++; throw new IOException(); },
            false, Adapter, () => true, () => { }, () => resets++));
        Assert.Equal(1, calls);
        Assert.Equal(0, resets);
    }

    [Fact]
    public void AbortedOrTimedOut_DoesNotRetry()
    {
        // canRetry=false cubre abort, dispose y watchdog disparado.
        Assert.Throws<IOException>(() => Run(1, new IOException(), canRetry: false));
    }

    [Fact]
    public void PrepareRunsBeforeEachAttempt_ResetBetween()
    {
        var log = new List<string>();
        int calls = 0;
        DiagLogic.RunWithReconnect(
            () => { log.Add("work"); return ++calls == 1 ? throw new IOException() : 1; },
            true, Adapter, () => true, () => log.Add("prepare"), () => log.Add("reset"));
        Assert.Equal(["prepare", "work", "reset", "prepare", "work"], log);
    }

    [Fact]
    public void FormatValue_Formats()
    {
        Assert.Equal("", DiagLogic.FormatValue(null));
        Assert.Equal("abc", DiagLogic.FormatValue("abc"));
        Assert.Equal("-12", DiagLogic.FormatValue(-12L));
        Assert.Equal("12.5", DiagLogic.FormatValue(12.5));
        Assert.Equal("1", DiagLogic.FormatValue(1.0));
        Assert.Equal("0.123457", DiagLogic.FormatValue(0.1234567891));
        Assert.Equal("00 AB FF", DiagLogic.FormatValue(new byte[] { 0x00, 0xAB, 0xFF }));
        Assert.Equal("", DiagLogic.FormatValue(Array.Empty<byte>()));
        Assert.Equal("7", DiagLogic.FormatValue(7));
        Assert.Equal("True", DiagLogic.FormatValue(true));
    }

    [Fact]
    public void FormatValue_IsCultureInvariant()
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("es-ES");
            Assert.Equal("1.5", DiagLogic.FormatValue(1.5));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = old; }
    }
}

public class JobPolicyTests
{
    [Theory]
    [InlineData("STATUS_MOTOR")]
    [InlineData("status_motor")]
    [InlineData("  STATUS_UBATT")]
    [InlineData("IDENT")]
    [InlineData("IDENTIFIKATION")]
    [InlineData("FS_LESEN")]
    [InlineData("fs_lesen_detail")]
    [InlineData("INFO")]
    [InlineData("SERIENNUMMER_LESEN")]
    [InlineData("HS_LESEN")]
    [InlineData("IS_LESEN")]
    public void ReadJobs_AreReadOnly(string job) => Assert.True(JobPolicy.IsReadOnly(job));

    [Theory]
    [InlineData("FS_LOESCHEN")]
    [InlineData("fs_loeschen")]
    [InlineData("STEUERN_LUEFTER")]
    [InlineData(" steuern_ventil")]
    [InlineData("CODIERDATEN_SCHREIBEN")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XSTATUS_FOO")]
    public void WriteOrUnknownJobs_AreNotReadOnly(string job) => Assert.False(JobPolicy.IsReadOnly(job));
}
