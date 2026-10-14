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
    [InlineData("STATUS_ANLERNEN")]
    [InlineData("STATUS_SPERRE")]
    [InlineData("CODIERDATEN_LESEN_UEBERNEHMEN")]
    [InlineData("STATUS_FUNKTION_AKTIVIEREN")]
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

public sealed class JobPolicyCodingReadTests
{
    // Lecturas de codificación / orden de vehículo: sin confirmación de escritura.
    [Theory]
    [InlineData("C_FA_LESEN")]
    [InlineData("C_FG_LESEN")]
    [InlineData("CODIERDATEN_LESEN")]
    [InlineData("aif_lesen")]
    [InlineData("ZIF_LESEN")]
    public void CodingReads_AreReadOnly(string job) => Assert.True(JobPolicy.IsReadOnly(job));

    // La marca de escritura sigue mandando aunque el nombre parezca de lectura de codificación.
    [Theory]
    [InlineData("C_FA_SCHREIBEN")]
    [InlineData("CODIERDATEN_LESEN_UND_SCHREIBEN")]
    [InlineData("C_FA_LESEN_RESET")]
    [InlineData("FA_LESEN_SPEICHERN")]
    [InlineData("CODIERUNG_LESEN_STEUERN")]
    public void CodingNamesWithWriteMarker_AreNotReadOnly(string job) => Assert.False(JobPolicy.IsReadOnly(job));
}
