using InpaDroid.Diag;

namespace InpaDroid.Tests;

public sealed class ReportBuilderTests
{
    static readonly ReportLabels L = new("es", "Informe", "Fecha", "Chasis", "Centralita", "SGBD", "VIN", "Taller",
        "Código", "Descripción", "Estado", "Contador", "Condiciones", "Activo", "Histórico", "Desconocido",
        "Sin fallos", "Aviso");

    static string Html(params FaultDetail[] faults) =>
        ReportBuilder.ToHtml(new ReportData(new DateTime(2026, 1, 2, 3, 4, 0), "E46", "DME", "MS43", faults), L);

    static FaultDetail Fault(string desc, FaultState st = FaultState.Active, params FaultCondition[] c) =>
        new("12", desc, st, "3", c);

    [Fact]
    public void Escapes_FaultText_SgbdAndConditions()
    {
        var html = ReportBuilder.ToHtml(new ReportData(DateTime.Now, "E46", "<b>x</b>", "\"S\"&",
            [Fault("<script>alert(1)</script> \"q\" 'a'", FaultState.Active, new FaultCondition("<i>", "\"v\"", "u"))]), L);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<b>x", html);
        Assert.DoesNotContain("<i>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("&quot;q&quot;", html);
        Assert.Contains("&quot;S&quot;&amp;", html);
    }

    [Fact]
    public void NoFaults_ShowsMessageAndNoTable()
    {
        var html = Html();
        Assert.Contains("Sin fallos", html);
        Assert.DoesNotContain("<table", html);
        Assert.Contains("Aviso", html);
    }

    [Fact]
    public void StateLabels()
    {
        var html = Html(Fault("a", FaultState.Active), Fault("b", FaultState.History), Fault("c", FaultState.Unknown));
        Assert.Contains("<td>Activo</td>", html);
        Assert.Contains("<td>" + System.Net.WebUtility.HtmlEncode("Histórico") + "</td>", html);
        Assert.Contains("<td>Desconocido</td>", html);
    }

    [Fact]
    public void SelfContained_NoScriptsOrExternalResources()
    {
        var html = Html(Fault("a"));
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("http", html);
        Assert.Contains("2026-01-02 03:04", html);
        Assert.DoesNotContain("VIN:", html);
    }

    [Fact]
    public void OptionalVinAndWorkshop_AreEscapedAndShown()
    {
        var html = ReportBuilder.ToHtml(new ReportData(DateTime.Now, "E46", "DME", "MS43", [], "WBA<1>", "Taller <X>"), L);
        Assert.Contains("VIN:</strong> WBA&lt;1&gt;", html);
        Assert.Contains("Taller &lt;X&gt;", html);
    }
}
