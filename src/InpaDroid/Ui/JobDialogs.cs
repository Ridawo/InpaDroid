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
            jobs.Select(j => new TwoLineAdapter.Row(j.Name, FirstLine(j.Comment), null, UiUtil.BlueDark, j)));
        list.Adapter = adapter;
        void UpdateCount() => count.Text = UiUtil.Str(activity, Resource.String.g_jd_count, adapter.Count, adapter.TotalCount);
        UpdateCount();

        var dialog = new AlertDialog.Builder(activity)
            .SetTitle(title)!
            .SetView(view)!
            .SetNegativeButton(Resource.String.g_cancel, (_, _) => { })!
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
                    + (job.Comment.Length > 0 ? UiUtil.Str(activity, Resource.String.g_jd_comment, job.Comment) + "\n" : "")
                    + UiUtil.Str(activity, Resource.String.g_jd_args,
                        job.Arguments.Count > 0 ? string.Join(", ", job.Arguments) : activity.GetString(Resource.String.g_jd_none)) + "\n"
                    + UiUtil.Str(activity, Resource.String.g_jd_results, job.Results.Count > 0 ? string.Join(", ", job.Results) : "-");
        if (job.Arguments.Count > 0)
            args.Hint = string.Join(";", job.Arguments);

        var dialog = new AlertDialog.Builder(activity)
            .SetTitle(job.Name)!
            .SetView(view)!
            .SetPositiveButton(runText, (_, _) => onRun(args.Text?.Trim() ?? "", results.Text?.Trim() ?? ""))!
            .SetNegativeButton(Resource.String.g_cancel, (_, _) => { })!
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

    /// <summary>
    /// Confirmación de un job que no es de solo lectura: SGBD, JOB y argumentos tal cual se enviarán y, si la batería
    /// está baja o sin leer, un aviso destacado encima (no bloquea: el usuario puede seguir).
    /// </summary>
    public static void ConfirmJob(Activity activity, string title, string message, string sgbd, string job, string args,
        string yesText, Action onYes, (string Text, Action Click)? extra = null)
    {
        var details = $"SGBD: {sgbd}\nJOB: {job}\nARGS: {(args.Length > 0 ? args : activity.GetString(Resource.String.g_jd_none))}";
        Confirm(activity, title, details + "\n\n" + message, yesText, onYes, BatteryBanner(activity), extra);
    }

    // Última tensión leída por la cabecera (InpaActivity la refresca cada ~2 s); null si es correcta.
    static string? BatteryBanner(Activity activity)
    {
        var volts = activity is InpaActivity { HeaderFresh: true }
            ? BatteryGuard.ParseHeader(activity.FindViewById<TextView>(Resource.Id.inpa_batt_text)?.Text) : null;
        var min = BatteryGuard.MinVolts.ToString("0.0");
        return BatteryGuard.Check(volts) switch
        {
            BatteryGuard.State.Low => UiUtil.Str(activity, Resource.String.sf_batt_low, volts!.Value.ToString("0.0"), min),
            BatteryGuard.State.Unknown => UiUtil.Str(activity, Resource.String.sf_batt_unknown, min),
            _ => null,
        };
    }

    /// <summary>
    /// Confirmación de una acción destructiva o que activa componentes: botón de acción en rojo y aviso destacado.
    /// banner: aviso extra en bloque invertido encima del mensaje; extra: botón neutro (p. ej. guardar antes).
    /// </summary>
    public static void Confirm(Activity activity, string title, string message, string yesText, Action onYes,
        string? banner = null, (string Text, Action Click)? extra = null)
    {
        var builder = new AlertDialog.Builder(activity)
            .SetPositiveButton(yesText, (_, _) => onYes())!
            .SetNegativeButton(Resource.String.g_cancel, (_, _) => { })!;
        if (extra is { } e)
            builder.SetNeutralButton(e.Text, (_, _) => e.Click());

        int p = UiUtil.Dp(activity, 16);
        var content = new LinearLayout(activity) { Orientation = Android.Widget.Orientation.Vertical };
        if (banner != null)
        {
            // Colores invertidos y letra mayor que el mensaje: no puede pasar desapercibido.
            var alert = new TextView(activity) { Text = banner };
            alert.SetTextColor(UiUtil.ErrorBg);
            alert.SetTextSize(Android.Util.ComplexUnitType.Sp, 18);
            alert.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
            alert.SetBackgroundColor(UiUtil.ErrorText);
            alert.SetPadding(p, p, p, p);
            var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            lp.BottomMargin = p / 2;
            content.AddView(alert, lp);
        }
        var warn = new TextView(activity) { Text = message };
        warn.SetTextColor(UiUtil.ErrorText);
        warn.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
        warn.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
        warn.SetBackgroundColor(UiUtil.ErrorBg);
        warn.SetPadding(p, p, p, p);
        content.AddView(warn);
        var box = new ScrollView(activity);
        box.SetPadding(p, p / 2, p, 0);
        box.AddView(content);
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
            dialog.GetButton((int)Android.Content.DialogButtonType.Neutral)?.SetMinHeight(UiUtil.Dp(activity, UiUtil.TouchDp));
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
            .SetPositiveButton(Resource.String.g_ok, (_, _) => { })!
            .Show();
    }

    static string FirstLine(string text)
    {
        int nl = text.IndexOf('\n');
        return (nl >= 0 ? text[..nl] : text).Trim();
    }
}
