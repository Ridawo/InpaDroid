using Android.App;
using Android.Views;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

/// <summary>Diálogos de jobs al estilo Tool32: lista filtrable, datos del job + argumentos, confirmaciones.</summary>
internal static class JobDialogs
{
    public static void PickJob(Activity activity, string title, IReadOnlyList<JobInfo> jobs, Action<JobInfo> onPick)
    {
        var view = activity.LayoutInflater.Inflate(Resource.Layout.dialog_job_list, null)!;
        var search = view.FindViewById<EditText>(Resource.Id.job_search)!;
        var count = view.FindViewById<TextView>(Resource.Id.job_count)!;
        var list = view.FindViewById<ListView>(Resource.Id.job_list)!;

        // Altura de la lista: como mucho la mitad de la pantalla (para que quepa también en horizontal).
        int screenH = activity.Resources?.DisplayMetrics?.HeightPixels ?? 1000;
        var lp = list.LayoutParameters!;
        lp.Height = Math.Min(UiUtil.Dp(activity, 360), screenH / 2);
        list.LayoutParameters = lp;

        var adapter = new TwoLineAdapter(activity,
            jobs.Select(j => new TwoLineAdapter.Row(j.Name, FirstLine(j.Comment), null, UiUtil.Blue, j)));
        list.Adapter = adapter;
        void UpdateCount() => count.Text = $"{adapter.Count} de {adapter.TotalCount} jobs";
        UpdateCount();

        var dialog = new AlertDialog.Builder(activity)
            .SetTitle(title)!
            .SetView(view)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .Create()!;
        search.TextChanged += (_, _) =>
        {
            adapter.Filter(search.Text);
            UpdateCount();
        };
        list.ItemClick += (_, e) =>
        {
            var job = (JobInfo)adapter.GetRow(e.Position).Tag;
            dialog.Dismiss();
            onPick(job);
        };
        dialog.ShowEvent += (_, _) =>
            dialog.GetButton((int)Android.Content.DialogButtonType.Negative)?.SetMinHeight(UiUtil.Dp(activity, UiUtil.TouchDp));
        dialog.Show();
    }

    public static void AskArguments(Activity activity, string sgbd, JobInfo job, string runText,
        Action<string, string> onRun)
    {
        var view = activity.LayoutInflater.Inflate(Resource.Layout.dialog_job_run, null)!;
        var info = view.FindViewById<TextView>(Resource.Id.jobrun_info)!;
        var args = view.FindViewById<EditText>(Resource.Id.jobrun_args)!;
        var results = view.FindViewById<EditText>(Resource.Id.jobrun_results)!;

        info.Text = $"SGBD       : {sgbd}\nJOB        : {job.Name}\n"
                    + (job.Comment.Length > 0 ? $"Comentario : {job.Comment}\n" : "")
                    + "Argumentos : " + (job.Arguments.Count > 0 ? string.Join(", ", job.Arguments) : "(ninguno)") + "\n"
                    + "Resultados : " + (job.Results.Count > 0 ? string.Join(", ", job.Results) : "-");
        if (job.Arguments.Count > 0)
            args.Hint = string.Join(";", job.Arguments);

        var dialog = new AlertDialog.Builder(activity)
            .SetTitle(job.Name)!
            .SetView(view)!
            .SetPositiveButton(runText, (_, _) => onRun(args.Text?.Trim() ?? "", results.Text?.Trim() ?? ""))!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .Create()!;
        dialog.ShowEvent += (_, _) =>
        {
            var yes = dialog.GetButton((int)Android.Content.DialogButtonType.Positive);
            yes?.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
            yes?.SetMinHeight(UiUtil.Dp(activity, UiUtil.TouchDp));
            dialog.GetButton((int)Android.Content.DialogButtonType.Negative)?.SetMinHeight(UiUtil.Dp(activity, UiUtil.TouchDp));
        };
        dialog.Show();
    }

    /// <param name="danger">null = se detecta por el título (⚠ / Borrar / Steuern): botón de acción en rojo y aviso destacado.</param>
    public static void Confirm(Activity activity, string title, string message, string yesText, Action onYes,
        bool? danger = null)
    {
        bool isDanger = danger ?? (title.Contains('⚠') || title.Contains("Borrar", StringComparison.OrdinalIgnoreCase)
                                   || title.Contains("componentes", StringComparison.OrdinalIgnoreCase));
        var builder = new AlertDialog.Builder(activity)
            .SetPositiveButton(yesText, (_, _) => onYes())!
            .SetNegativeButton("Cancelar", (_, _) => { })!;
        if (!isDanger)
        {
            builder.SetTitle(title)!.SetMessage(message)!.Show();
            return;
        }

        var warn = new TextView(activity) { Text = message };
        warn.SetTextColor(UiUtil.ErrorText);
        warn.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
        warn.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
        warn.SetBackgroundColor(UiUtil.ErrorBg);
        int p = UiUtil.Dp(activity, 16);
        warn.SetPadding(p, p, p, p);
        var box = new ScrollView(activity);
        box.SetPadding(p, p / 2, p, 0);
        box.AddView(warn);
        var dialog = builder.SetTitle(title)!.SetView(box)!.Create()!;
        dialog.ShowEvent += (_, _) =>
        {
            var yes = dialog.GetButton((int)Android.Content.DialogButtonType.Positive);
            if (yes != null)
            {
                yes.SetTextColor(UiUtil.ErrorText);
                yes.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
                yes.SetMinHeight(UiUtil.Dp(activity, UiUtil.TouchDp));
            }
            dialog.GetButton((int)Android.Content.DialogButtonType.Negative)?.SetMinHeight(UiUtil.Dp(activity, UiUtil.TouchDp));
        };
        dialog.Show();
    }

    public static void Message(Activity activity, string title, string message)
    {
        var text = new TextView(activity) { Text = message, Typeface = Android.Graphics.Typeface.Monospace };
        text.SetTextIsSelectable(true);
        int p = UiUtil.Dp(activity, 16);
        text.SetPadding(p, p / 2, p, 0);
        var scroll = new ScrollView(activity);
        scroll.AddView(text);
        new AlertDialog.Builder(activity)
            .SetTitle(title)!
            .SetView(scroll)!
            .SetPositiveButton("OK", (_, _) => { })!
            .Show();
    }

    static string FirstLine(string text)
    {
        int nl = text.IndexOf('\n');
        return (nl >= 0 ? text[..nl] : text).Trim();
    }
}
