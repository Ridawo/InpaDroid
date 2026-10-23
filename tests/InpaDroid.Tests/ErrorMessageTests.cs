using InpaDroid.Diag;

namespace InpaDroid.Tests;

public class ErrorMessageTests
{
    [Theory]
    [InlineData("Error occurred: EDIABAS_IFH_0009", ErrorKind.NoEcuResponse)]
    [InlineData("IFH-0009: NO RESPONSE FROM CONTROLUNIT", ErrorKind.NoEcuResponse)]
    [InlineData("EDIABAS::IFH-0032", ErrorKind.NoEcuResponse)]
    [InlineData("Error occurred: EDIABAS_IFH_0008", ErrorKind.EcuConnection)]
    [InlineData("Error occurred: EDIABAS_IFH_0003", ErrorKind.AdapterLost)]
    [InlineData("ifh-0002", ErrorKind.AdapterLost)]
    [InlineData("Error occurred: EDIABAS_IFH_0018", ErrorKind.InterfaceNotFound)]
    [InlineData("Error occurred: EDIABAS_IFH_0019", ErrorKind.PortOpen)]
    [InlineData("Could not open port COM3", ErrorKind.PortOpen)]
    [InlineData("USB permission denied", ErrorKind.UsbPermission)]
    [InlineData("Error occurred: EDIABAS_SYS_0005", ErrorKind.SgbdNotFound)]
    [InlineData("Error occurred: EDIABAS_SYS_0002", ErrorKind.SgbdNotFound)]
    [InlineData("Error occurred: EDIABAS_BIP_0006", ErrorKind.SgbdInvalid)]
    [InlineData("Error occurred: EDIABAS_SYS_0008", ErrorKind.JobNotFound)]
    [InlineData("The operation timed out", ErrorKind.Timeout)]
    public void Classify_KnownCodesAndTexts(string raw, ErrorKind expected) =>
        Assert.Equal(expected, DiagLogic.Classify(raw));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Error occurred: EDIABAS_IFH_0044")]
    [InlineData("Something unexpected")]
    public void Classify_UnknownFallsBack(string? raw) =>
        Assert.Equal(ErrorKind.Unknown, DiagLogic.Classify(raw));

    [Fact]
    public void Classify_ServiceTexts()
    {
        Assert.Equal(ErrorKind.Timeout, DiagLogic.Classify(DiagLogic.TimeoutText(60)));
        Assert.Equal(ErrorKind.Cancelled, DiagLogic.Classify(DiagLogic.CancelledText));
        Assert.Equal(ErrorKind.ServiceClosed, DiagLogic.Classify(DiagLogic.ServiceClosedText));
        // The prefix wins over the IFH-0018 code that follows it.
        Assert.Equal(ErrorKind.UsbPermission, DiagLogic.Classify(DiagLogic.UsbPermissionText + ": Error occurred: EDIABAS_IFH_0018"));
    }
}
