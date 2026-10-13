using System.Globalization;
using System.Net;
using System.Text;

namespace InpaDroid.Diag;

/// <summary>Datos del informe de fallos para el cliente.</summary>
public sealed record ReportData(DateTime When, string Chassis, string EcuTitle, string Sgbd,
    IReadOnlyList<FaultDetail> Faults, string? Vin = null, string? Workshop = null);

/// <summary>Textos visibles del informe, ya traducidos.</summary>
public sealed record ReportLabels(string Lang, string Title, string Date, string Chassis, string Ecu, string Sgbd,
    string Vin, string Workshop, string Code, string Description, string State, string Counter, string Conditions,
    string Active, string History, string Unknown, string NoFaults, string Disclaimer);

// Lógica pura (sin Android), enlazada también desde tests/InpaDroid.Tests.
public static class ReportBuilder
{
    const string Css =
        "body{font-family:sans-serif;margin:16px auto;max-width:900px;padding:0 8px;color:#111}" +
        "h1{font-size:1.4em}table{border-collapse:collapse;width:100%}" +
        "th,td{border:1px solid #999;padding:6px;text-align:left;vertical-align:top}" +
        "th{background:#eee}.meta{margin:0 0 12px}.meta p{margin:2px 0}.disclaimer{margin-top:16px;font-size:.85em;color:#555}" +
        "@media print{body{margin:0}}";

    // Todo valor dinámico (el texto del fallo viene de ficheros ECU) pasa por aquí.
    static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    public static string ToHtml(ReportData data, ReportLabels l)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html lang=\"").Append(E(l.Lang)).Append("\"><head><meta charset=\"utf-8\">")
          .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
          .Append("<title>").Append(E(l.Title)).Append("</title><style>").Append(Css).Append("</style></head><body>")
          .Append("<h1>").Append(E(l.Title)).Append("</h1><div class=\"meta\">");
        Meta(sb, l.Date, data.When.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        Meta(sb, l.Chassis, data.Chassis);
        if (!string.IsNullOrWhiteSpace(data.Vin))
            Meta(sb, l.Vin, data.Vin);
        Meta(sb, l.Ecu, data.EcuTitle);
        Meta(sb, l.Sgbd, data.Sgbd);
        if (!string.IsNullOrWhiteSpace(data.Workshop))
            Meta(sb, l.Workshop, data.Workshop);
        sb.Append("</div>");

        if (data.Faults.Count == 0)
            sb.Append("<p>").Append(E(l.NoFaults)).Append("</p>");
        else
        {
            sb.Append("<table><thead><tr>");
            foreach (var h in new[] { l.Code, l.Description, l.State, l.Counter, l.Conditions })
                sb.Append("<th>").Append(E(h)).Append("</th>");
            sb.Append("</tr></thead><tbody>");
            foreach (var f in data.Faults)
            {
                string state = f.State switch { FaultState.Active => l.Active, FaultState.History => l.History, _ => l.Unknown };
                sb.Append("<tr><td>").Append(E(f.Code)).Append("</td><td>").Append(E(f.Description))
                  .Append("</td><td>").Append(E(state)).Append("</td><td>").Append(E(f.Counter)).Append("</td><td>")
                  .Append(string.Join("<br>", f.Conditions.Select(c => E($"{c.Text}: {c.Value} {c.Unit}".Trim()))))
                  .Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
        }
        return sb.Append("<p class=\"disclaimer\">").Append(E(l.Disclaimer)).Append("</p></body></html>").ToString();
    }

    static void Meta(StringBuilder sb, string label, string? value) =>
        sb.Append("<p><strong>").Append(E(label)).Append(":</strong> ").Append(E(value)).Append("</p>");
}
