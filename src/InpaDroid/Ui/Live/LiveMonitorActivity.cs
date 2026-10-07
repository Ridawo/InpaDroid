using System.Globalization;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui.Live;

/// <summary>
/// Monitor en vivo: ejecuta repetidamente un job STATUS_* y dibuja los resultados numéricos elegidos.
/// Todas las llamadas a ediabaslib pasan por la cola de DiagService (RunJobAsync); aquí nunca se toca EdiabasNet.
/// </summary>
[Activity(Label = "Monitor en vivo", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
                           | ConfigChanges.UiMode | ConfigChanges.Density | ConfigChanges.FontScale | ConfigChanges.Locale)]
public class LiveMonitorActivity : Activity
{
    public const string ExtraEcu = "live_ecu";
    public const string ExtraSgbd = "live_sgbd";
    public const string ExtraJob = "live_job";
    public const string ExtraArgs = "live_args";
    public const string ExtraResults = "live_results";   // string[] con los nombres de resultado numéricos

    static readonly int[] IntervalsMs = [250, 500, 1000, 2000];
    static readonly int[] Windows = [60, 120, 300];
    // Los extras de un Intent viajan por Binder (~1 MB): por encima de esto el CSV se recorta.
    const int MaxShareChars = 100_000;

    string _sgbd = "", _job = "", _args = "", _ecu = "";
    string[] _names = [];
    readonly LiveSeriesData _data = new();
    LiveChartView _chart = null!;
    TextView _status = null!;
    Button _pauseBtn = null!, _intervalBtn = null!, _windowBtn = null!;
    LinearLayout _chips = null!;
    readonly List<TextView> _chipViews = [];
    readonly PollRunner _poll = new();
    DiagService? _inFlight;   // servicio con un job de este monitor en curso
    bool _userPaused;         // pausado con el botón: al volver a primer plano no se reanuda solo
    int _intervalIdx = 1, _windowIdx = 1;

    // GetString(int, params Java.Lang.Object[]) avisa (CS8604) con las conversiones implícitas desde string.
    string Str(int id, params string[] args) =>
        GetString(id, args.Select(a => (Java.Lang.Object)new Java.Lang.String(a)).ToArray());

    static string Num(double v) => double.IsNaN(v) ? "--" : v.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>Intent listo para lanzar con StartActivity.</summary>
    public static Intent CreateIntent(Context ctx, string ecu, string sgbd, string job, string args, string[] results)
    {
        var i = new Intent(ctx, typeof(LiveMonitorActivity));
        i.PutExtra(ExtraEcu, ecu);
        i.PutExtra(ExtraSgbd, sgbd);
        i.PutExtra(ExtraJob, job);
        i.PutExtra(ExtraArgs, args);
        i.PutExtra(ExtraResults, results);
        return i;
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _ecu = Intent?.GetStringExtra(ExtraEcu) ?? "";
        _sgbd = Intent?.GetStringExtra(ExtraSgbd) ?? "";
        _job = Intent?.GetStringExtra(ExtraJob) ?? "";
        _args = Intent?.GetStringExtra(ExtraArgs) ?? "";
        _names = Intent?.GetStringArrayExtra(ExtraResults) ?? [];
        Title = GetString(Resource.String.live_title) + " - " + _ecu;

        int p = UiUtil.Dp(this, 8);
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(p, p, p, p);

        _status = new TextView(this) { Text = GetString(Resource.String.live_waiting) };
        root.AddView(_status, new LinearLayout.LayoutParams(-1, -2));

        var chipScroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        _chips = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        chipScroll.AddView(_chips, new FrameLayout.LayoutParams(-2, -2));
        root.AddView(chipScroll, new LinearLayout.LayoutParams(-1, -2));

        _chart = new LiveChartView(this, _data);
        root.AddView(_chart, new LinearLayout.LayoutParams(-1, 0, 1f) { TopMargin = p / 2, BottomMargin = p / 2 });

        _data.Window = Windows[_windowIdx];
        _intervalBtn = BigButton("");
        _intervalBtn.Click += (_, _) =>
        {
            _intervalIdx = (_intervalIdx + 1) % IntervalsMs.Length;
            UpdateSettingLabels();
            if (_poll.Running)
            {
                // El intervalo se fija al arrancar el bucle: se reinicia con el nuevo.
                Stop();
                Start();
            }
        };
        _windowBtn = BigButton("");
        _windowBtn.Click += (_, _) =>
        {
            _windowIdx = (_windowIdx + 1) % Windows.Length;
            _data.Window = Windows[_windowIdx];
            UpdateSettingLabels();
            _chart.Invalidate();
        };
        UpdateSettingLabels();
        root.AddView(Row(_intervalBtn, _windowBtn), new LinearLayout.LayoutParams(-1, -2));

        _pauseBtn = BigButton(GetString(Resource.String.live_pause));
        _pauseBtn.Click += (_, _) =>
        {
            _userPaused = _poll.Running;
            if (_userPaused)
                Stop();
            else
                Start();
        };
        var clear = BigButton(GetString(Resource.String.live_clear));
        clear.Click += (_, _) =>
        {
            _data.Clear();
            RefreshChips();
            _chart.Invalidate();
        };
        root.AddView(Row(_pauseBtn, clear), new LinearLayout.LayoutParams(-1, -2));

        var export = BigButton(GetString(Resource.String.live_export));
        export.Click += (_, _) => Export();
        root.AddView(export, new LinearLayout.LayoutParams(-1, -2));

        SetContentView(root);
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (!_userPaused)
            Start();
    }

    protected override void OnPause()
    {
        Stop();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        Stop();
        base.OnDestroy();
    }

    void Start()
    {
        if (_poll.Running || _sgbd.Length == 0 || _job.Length == 0)
            return;
        _pauseBtn.Text = GetString(Resource.String.live_pause);
        // Solo se piden al ECU los resultados elegidos; DiagService añade JOB_STATUS por su cuenta.
        string results = string.Join(";", _names);
        // Se espera cada respuesta antes de encolar el siguiente job, así la cola nunca se acumula.
        _poll.Start(async ct =>
        {
            try
            {
                var diag = DiagHolder.Get(this);
                _inFlight = diag;
                var r = await diag.RunJobAsync(_sgbd, _job, _args, results);
                if (ct.IsCancellationRequested)
                    return false;
                Show(r);
                return true;
            }
            finally
            {
                // Tras Stop/Start rápido otro bucle puede tener ya su propio job en curso: no se le pisa.
                if (_poll.IsCurrent(ct))
                    _inFlight = null;
            }
        }, ex => _status.Text = Str(Resource.String.live_error, UiUtil.Describe(ex)), IntervalsMs[_intervalIdx], 50);
    }

    void Stop()
    {
        if (!_poll.Stop())
            return;
        _pauseBtn.Text = GetString(Resource.String.live_resume);
        // Interrumpe el job en curso para liberar el adaptador cuanto antes. Solo si es nuestro: Abort cancela
        // también lo que otras pantallas tengan en cola, y DiagHolder.Get crearía un servicio solo para abortar.
        _inFlight?.Abort();
        _inFlight = null;
    }

    void Show(JobResult r)
    {
        if (!r.Ok || (r.JobStatus.Length > 0 && !r.JobStatus.Equals("OKAY", StringComparison.OrdinalIgnoreCase)))
        {
            _status.Text = Str(Resource.String.live_error, r.Ok ? r.JobStatus : r.Error);
            return;
        }
        var sample = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var set in r.Sets)
        {
            foreach (var (key, value) in set)
            {
                if (key.Equals("JOB_STATUS", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (_names.Length > 0 && !_names.Contains(key, StringComparer.OrdinalIgnoreCase))
                    continue;
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && double.IsFinite(d))
                    sample[key] = d;
            }
        }
        if (sample.Count == 0)
        {
            _status.Text = GetString(Resource.String.live_no_numeric);
            return;
        }
        _data.AddSample(DateTime.Now, sample);
        if (_chipViews.Count != _data.Series.Count)
            RefreshChips();
        else
            UpdateChipTexts();
        _chart.Invalidate();
        _status.Text = Str(Resource.String.live_status, _sgbd, _job, _data.Count.ToString(CultureInfo.CurrentCulture));
    }

    // Botones de al menos 56 dp de alto: se pulsan con el móvil en la mano o en un soporte.
    Button BigButton(string text)
    {
        var b = new Button(this) { Text = text };
        b.SetMinimumHeight(UiUtil.Dp(this, 56));
        b.SetAllCaps(false);
        return b;
    }

    LinearLayout Row(params View[] views)
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        foreach (var v in views)
            row.AddView(v, new LinearLayout.LayoutParams(0, -2, 1f));
        return row;
    }

    void UpdateSettingLabels()
    {
        _intervalBtn.Text = Str(Resource.String.live_interval, (IntervalsMs[_intervalIdx] / 1000.0).ToString("0.##", CultureInfo.CurrentCulture));
        _windowBtn.Text = Str(Resource.String.live_window, Windows[_windowIdx].ToString(CultureInfo.CurrentCulture));
    }

    // Leyenda interactiva: un chip por serie con valor actual/mín/máx; tocarlo oculta o muestra la serie.
    void RefreshChips()
    {
        _chips.RemoveAllViews();
        _chipViews.Clear();
        int m = UiUtil.Dp(this, 4);
        foreach (var s in _data.Series)
        {
            var chip = new TextView(this) { Gravity = Android.Views.GravityFlags.CenterVertical };
            chip.SetMinimumHeight(UiUtil.Dp(this, 56));
            chip.SetMinimumWidth(UiUtil.Dp(this, 120));
            chip.SetPadding(m * 3, m, m * 3, m);
            chip.SetTextColor(Android.Graphics.Color.White);
            chip.Click += (_, _) =>
            {
                s.Visible = !s.Visible;
                StyleChip(chip, s);
                _chart.Invalidate();
            };
            StyleChip(chip, s);
            _chips.AddView(chip, new LinearLayout.LayoutParams(-2, -2) { RightMargin = m });
            _chipViews.Add(chip);
        }
        UpdateChipTexts();
    }

    void StyleChip(TextView chip, LiveSeries s)
    {
        var color = LiveChartView.ColorOf(s.ColorIndex);
        var bg = new Android.Graphics.Drawables.GradientDrawable();
        bg.SetCornerRadius(UiUtil.Dp(this, 8));
        bg.SetColor(s.Visible ? color : Android.Graphics.Color.White);
        bg.SetStroke(UiUtil.Dp(this, 2), color);
        chip.Background = bg;
        chip.SetTextColor(s.Visible ? Android.Graphics.Color.White : color);
    }

    void UpdateChipTexts()
    {
        for (int i = 0; i < _chipViews.Count && i < _data.Series.Count; i++)
        {
            var s = _data.Series[i];
            _chipViews[i].Text = Str(Resource.String.live_series_stats, s.Name, Num(s.Last), Num(s.Min), Num(s.Max));
        }
    }

    void Export()
    {
        if (_data.Count == 0)
        {
            UiUtil.Toast(this, GetString(Resource.String.live_export_empty));
            return;
        }
        // Mismo escape RFC 4180 y anti-inyección de fórmulas que el resto de exportaciones.
        string csv = _data.ToCsv(ResultExporter.Cell, MaxShareChars, out bool truncated);
        var send = new Intent(Intent.ActionSend);
        send.SetType("text/plain");
        send.PutExtra(Intent.ExtraSubject, "InpaDroid " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        send.PutExtra(Intent.ExtraText, csv);
        var chooser = Intent.CreateChooser(send, GetString(Resource.String.live_export_title))!;
        try
        {
            StartActivity(chooser);
            if (truncated)
                UiUtil.Toast(this, GetString(Resource.String.live_export_truncated));
        }
        catch (Java.Lang.RuntimeException ex)
        {
            UiUtil.Toast(this, Str(Resource.String.live_export_failed, UiUtil.Describe(ex)));
        }
    }
}
