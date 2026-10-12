using InpaDroid.Diag;

namespace InpaDroid.Tests;

public sealed class JobPolicyWriteGuardTests
{
    // Empiezan por un prefijo de lectura pero escriben o activan: deben pedir confirmación y no reenviarse solos.
    [Theory]
    [InlineData("STATUS_VORGEBEN")]
    [InlineData("STATUS_ZAEHLER_RUECKSETZEN")]
    [InlineData("STATUS_ADAPTION_RESET")]
    [InlineData("INFO_SPEICHERN")]
    [InlineData("status_vorgeben")]
    [InlineData("IDENT_SCHREIBEN")]
    [InlineData("SERIENNUMMER_SCHREIBEN")]
    [InlineData("INFO_LOESCHEN")]
    [InlineData("FS_LESEN_UND_LOESCHEN")]
    [InlineData("STATUS_STEUERN_TEST")]
    public void ReadPrefixWithWriteMarker_IsNotReadOnly(string job) => Assert.False(JobPolicy.IsReadOnly(job));

    [Theory]
    [InlineData("STATUS_STEUERGERAET")]   // "STEUER" sin la N de STEUERN sigue siendo lectura
    [InlineData("STATUS_IO_LESEN")]
    [InlineData("STATUS_DIGITAL_GM3_EA")]
    public void ReadJobs_StayReadOnly(string job) => Assert.True(JobPolicy.IsReadOnly(job));
}
