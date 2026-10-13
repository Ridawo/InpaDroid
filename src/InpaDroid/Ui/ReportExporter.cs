using System.Globalization;
using Android.Content;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

/// <summary>Genera el informe HTML de fallos para el cliente y lo comparte como fichero.</summary>
public static class ReportExporter
{
    public static void Share(Context ctx, string chassis, string ecuTitle, string sgbd, JobResult faults)
    {
        var now = DateTime.Now;
        var list = faults.Sets.Where(FaultInfo.IsFault).Select(FaultInfo.Parse).ToList();
        string S(int id) => UiUtil.Str(ctx, id);
        var labels = new ReportLabels(S(Resource.String.rp_lang), S(Resource.String.rp_title), S(Resource.String.rp_date),
            S(Resource.String.rp_chassis), S(Resource.String.rp_ecu), S(Resource.String.rp_sgbd), S(Resource.String.rp_vin),
            S(Resource.String.rp_workshop), S(Resource.String.rp_code), S(Resource.String.rp_description),
            S(Resource.String.rp_state), S(Resource.String.rp_counter), S(Resource.String.rp_conditions),
            S(Resource.String.rp_active), S(Resource.String.rp_history), S(Resource.String.rp_unknown),
            S(Resource.String.rp_no_faults), S(Resource.String.rp_disclaimer));
        string html = ReportBuilder.ToHtml(new ReportData(now, chassis, ecuTitle, sgbd, list), labels);
        ResultExporter.ShareFile(ctx, $"inpadroid-report-{now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}.html",
            html, "text/html", UiUtil.Str(ctx, Resource.String.rp_subject, now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            S(Resource.String.rp_chooser), new System.Text.UTF8Encoding(false));
    }
}
