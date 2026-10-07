using Stopwatch = System.Diagnostics.Stopwatch;
using System.Globalization;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui.Live;

/// <summary>
/// Monitor en vivo: ejecuta repetidamente un job STATUS_* y dibuja los resultados numéricos elegidos.
/// Todas las llamadas a ediabaslib pasan por la cola de DiagService (RunJobAsync); aquí nunca se toca EdiabasNet.
/// </summary>
[Activity(Label = "Monitor en vivo", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class LiveMonitorActivity : Activity
{
    public const string ExtraEcu = "live_ecu";
    public const string ExtraSgbd = "live_sgbd";
    public const string ExtraJob = "live_job";
    public const string ExtraArgs = "live_args";
    public const string ExtraResults = "live_results";   // string[] con los nombres de resultado numéricos

    const int IntervalMs = 500;

    string _sgbd = "", _job = "", _args = "", _ecu = "";
    string[] _names = [];
    LiveChartView _chart = null!;
    TextView _status = null!;
    Button _pauseBtn = null!;
    CancellationTokenSource? _cts;
    DiagService? _inFlight;   // servicio con un job de este monitor en curso
    bool _userPaused;         // pausado con el botón: al volver a primer plano no se reanuda solo

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

        _chart = new LiveChartView(this);
        root.AddView(_chart, new LinearLayout.LayoutParams(-1, 0, 1f));

        var bar = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        _pauseBtn = new Button(this) { Text = GetString(Resource.String.live_pause) };
        _pauseBtn.Click += (_, _) =>
        {
            _userPaused = _cts != null;
            if (_userPaused)
                Stop();
            else
                Start();
        };
        var clear = new Button(this) { Text = GetString(Resource.String.live_clear) };
        clear.Click += (_, _) => _chart.Clear();
        bar.AddView(_pauseBtn, new LinearLayout.LayoutParams(0, -2, 1f));
        bar.AddView(clear, new LinearLayout.LayoutParams(0, -2, 1f));
        root.AddView(bar, new LinearLayout.LayoutParams(-1, -2));

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
        if (_cts != null || _sgbd.Length == 0 || _job.Length == 0)
            return;
        _cts = new CancellationTokenSource();
        _pauseBtn.Text = GetString(Resource.String.live_pause);
        PollLoop(_cts);
    }

    void Stop()
    {
        if (_cts == null)
            return;
        _cts.Cancel();
        _cts = null;
        _pauseBtn.Text = GetString(Resource.String.live_resume);
        // Interrumpe el job en curso para liberar el adaptador cuanto antes. Solo si es nuestro: Abort cancela
        // también lo que otras pantallas tengan en cola, y DiagHolder.Get crearía un servicio solo para abortar.
        _inFlight?.Abort();
        _inFlight = null;
    }

    async void PollLoop(CancellationTokenSource cts)
    {
        // Solo se piden al ECU los resultados elegidos; DiagService añade JOB_STATUS por su cuenta.
        string results = string.Join(";", _names);
        // Se espera cada respuesta antes de encolar el siguiente job, así la cola nunca se acumula.
        while (!cts.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var diag = DiagHolder.Get(this);
                _inFlight = diag;
                var r = await diag.RunJobAsync(_sgbd, _job, _args, results);
                if (cts.IsCancellationRequested)
                    break;
                Show(r);
            }
            catch (Exception ex)
            {
                if (cts.IsCancellationRequested)
                    break;
                _status.Text = GetString(Resource.String.live_error, UiUtil.Describe(ex));
            }
            finally
            {
                // Tras Stop/Start rápido otro bucle puede tener ya su propio job en curso: no se le pisa.
                if (_cts == cts)
                    _inFlight = null;
            }
            try
            {
                await Task.Delay((int)Math.Max(50, IntervalMs - sw.ElapsedMilliseconds), cts.Token);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
        }
        // Solo se sale tras Stop (ya cancelado) y nadie más usa este token.
        cts.Dispose();
    }

    void Show(JobResult r)
    {
        if (!r.Ok || (r.JobStatus.Length > 0 && !r.JobStatus.Equals("OKAY", StringComparison.OrdinalIgnoreCase)))
        {
            _status.Text = GetString(Resource.String.live_error, r.Ok ? r.JobStatus : r.Error);
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
        _chart.AddSample(sample);
        _status.Text = GetString(Resource.String.live_status, _sgbd, _job, _chart.SampleCount);
    }
}
