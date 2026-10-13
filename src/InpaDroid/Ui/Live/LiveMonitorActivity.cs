using System.Globalization;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using InpaDroid.Diag;
using InpaDroid.Ui.Chassis;

namespace InpaDroid.Ui.Live;

/// <summary>
/// Monitor en vivo: ejecuta repetidamente uno o varios jobs de lectura y dibuja los resultados numéricos elegidos;
/// opcionalmente graba cada muestra en un CSV (sin el tope de muestras de la sesión en memoria).
/// Todas las llamadas a ediabaslib pasan por la cola de DiagService (RunJobAsync); aquí nunca se toca EdiabasNet.
/// </summary>
[Activity(Label = "@string/live_title", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
                           | ConfigChanges.UiMode | ConfigChanges.Density | ConfigChanges.FontScale | ConfigChanges.Locale)]
public class LiveMonitorActivity : Activity
{
    // Declara UiMode: no se recrea al cambiar de tema, así que el gráfico recoge los colores nuevos aquí.
    public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        _chart?.ApplyTheme();
        for (int i = 0; i < _chipViews.Count && i < _data.Series.Count; i++)
            StyleChip(_chipViews[i], _data.Series[i]);
    }

    public const string ExtraEcu = "live_ecu";
    public const string ExtraSgbd = "live_sgbd";
    public const string ExtraJob = "live_job";
    public const string ExtraArgs = "live_args";
    public const string ExtraResults = "live_results";   // string[] con los nombres de resultado numéricos
    public const string ExtraEcuIndex = "live_ecu_index";   // índice de la ECU en el catálogo del chasis (-1 = buscar por título)
    public const string ExtraChassis = "live_chassis";   // id del chasis: habilita elegir valores de sus páginas de status

    // Uno por color de la paleta de LiveChartView: con más, dos series compartirían color.
    const int MaxChannels = 6;
    // Por encima, la grabación no se pasa por la hoja de compartir (se copia entera a cache/exports): solo se indica la ruta.
    const long MaxShareBytes = 1024 * 1024;

    static readonly int[] IntervalsMs = [250, 500, 1000, 2000];
    static readonly int[] Windows = [60, 120, 300];

    string _sgbd = "", _job = "", _args = "", _ecu = "";
    string[] _names = [];
    IReadOnlyList<ChassisPage> _pages = [];
    List<ChassisValue> _picked = [];   // valores elegidos en el selector; vacío = el job con el que se abrió
    IReadOnlyList<LiveJob> _plan = [];
    readonly LiveSeriesData _data = new();
    LiveChartView _chart = null!;
    TextView _status = null!;
    Button _pauseBtn = null!, _intervalBtn = null!, _windowBtn = null!, _recordBtn = null!;
    LinearLayout _chips = null!;
    readonly List<TextView> _chipViews = [];
    readonly PollRunner _poll = new();
    DiagService? _inFlight;   // servicio con un job de este monitor en curso
    bool _userPaused;         // pausado con el botón: al volver a primer plano no se reanuda solo
    int _intervalIdx = 1, _windowIdx = 1;
    bool _recording;          // grabación activada; el fichero se abre con la primera muestra (ahí se conocen las columnas)
    LiveLogFile? _log;

    static string Num(double v) => double.IsNaN(v) ? "--" : v.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>Intent listo para lanzar con StartActivity.</summary>
    public static Intent CreateIntent(Context ctx, string ecu, string sgbd, string job, string args, string[] results,
        string chassisId = "", int ecuIndex = -1)
    {
        var i = new Intent(ctx, typeof(LiveMonitorActivity));
        i.PutExtra(ExtraEcu, ecu);
        i.PutExtra(ExtraSgbd, sgbd);
        i.PutExtra(ExtraJob, job);
        i.PutExtra(ExtraArgs, args);
        i.PutExtra(ExtraResults, results);
        i.PutExtra(ExtraChassis, chassisId);
        i.PutExtra(ExtraEcuIndex, ecuIndex);
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
        _plan = [DefaultJob()];
        string chassis = Intent?.GetStringExtra(ExtraChassis) ?? "";
        if (chassis.Length > 0)
        {
            // Recreada tras matar el proceso: el catálogo debe incluir el override de la carpeta ECU, como en ChassisEcuActivity.
            if (savedInstanceState != null)
                DiagHolder.LoadCatalogs(this, DiagHolder.LoadSettings(this).EcuPath);
            var ecus = ChassisCatalog.Get(chassis).Ecus;
            int idx = Intent?.GetIntExtra(ExtraEcuIndex, -1) ?? -1;
            var ecu = idx >= 0 && idx < ecus.Count ? ecus[idx] : ecus.FirstOrDefault(e => e.Title == _ecu);
            _pages = ecu?.StatusPages ?? [];
        }
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

        var channels = BigButton(GetString(Resource.String.lv_channels));
        channels.Click += (_, _) => PickChannels();
        channels.Visibility = _pages.Count > 0 ? ViewStates.Visible : ViewStates.Gone;
        _recordBtn = BigButton(GetString(Resource.String.lv_record_start));
        _recordBtn.Click += (_, _) =>
        {
            if (_recording)
                StopRecording();
            else
            {
                _recording = true;
                _recordBtn.Text = GetString(Resource.String.lv_record_stop);
            }
        };
        root.AddView(Row(channels, _recordBtn), new LinearLayout.LayoutParams(-1, -2));

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
        _log?.Flush();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        Stop();
        CloseLog();
        base.OnDestroy();
    }

    void Start()
    {
        if (_poll.Running || _sgbd.Length == 0 || _plan[0].Job.Length == 0)
            return;
        _pauseBtn.Text = GetString(Resource.String.live_pause);
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        var plan = _plan;
        // Se espera cada respuesta antes de encolar el siguiente job, así la cola nunca se acumula. Cada job se pide
        // una vez por ciclo con solo sus resultados elegidos; DiagService añade JOB_STATUS por su cuenta.
        _poll.Start(async ct =>
        {
            try
            {
                var diag = DiagHolder.Get(this);
                _inFlight = diag;
                var sample = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                string? error = null;
                foreach (var job in plan)
                {
                    var r = await diag.RunJobAsync(_sgbd, job.Job, job.Args, string.Join(";", job.Results));
                    if (ct.IsCancellationRequested)
                        return false;
                    error ??= Collect(job, r, sample);
                }
                Show(sample, error);
                return true;
            }
            finally
            {
                // Tras Stop/Start rápido otro bucle puede tener ya su propio job en curso: no se le pisa.
                if (_poll.IsCurrent(ct))
                    _inFlight = null;
            }
        }, ex => _status.Text = UiUtil.Str(this, Resource.String.live_error, UiUtil.Describe(ex)), IntervalsMs[_intervalIdx], 50);
    }

    void Stop()
    {
        if (!_poll.Stop())
            return;
        _pauseBtn.Text = GetString(Resource.String.live_resume);
        Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
        // Interrumpe el job en curso para liberar el adaptador cuanto antes. Solo si es nuestro: Abort cancela
        // también lo que otras pantallas tengan en cola, y DiagHolder.Get crearía un servicio solo para abortar.
        _inFlight?.Abort();
        _inFlight = null;
    }

    LiveJob DefaultJob() => new(_job, _args, _names, _names);

    // Añade a la muestra los valores numéricos pedidos de un job; devuelve el error del job si ha fallado.
    string? Collect(LiveJob job, JobResult r, Dictionary<string, double> sample)
    {
        if (DiagLogic.StatusError(r, GetString(Resource.String.g_rp_job_failed)!) is string error)
            return error;
        foreach (var set in r.Sets)
        {
            foreach (var (key, value) in set)
            {
                if (key.Equals("JOB_STATUS", StringComparison.OrdinalIgnoreCase) || job.SeriesOf(key) is not string series)
                    continue;
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && double.IsFinite(d))
                    sample[series] = d;
            }
        }
        return null;
    }

    // Con varios jobs, uno que falle no impide graficar los demás: se dibuja lo recibido y se muestra el error.
    void Show(Dictionary<string, double> sample, string? error)
    {
        string? errorText = error == null ? null : UiUtil.Str(this, Resource.String.live_error, UiUtil.Friendly(this, error));
        if (sample.Count == 0)
        {
            _status.Text = errorText ?? GetString(Resource.String.live_no_numeric);
            return;
        }
        var now = DateTime.Now;
        _data.AddSample(now, sample);
        Record(now, sample);
        if (_chipViews.Count != _data.Series.Count)
            RefreshChips();
        else
            UpdateChipTexts();
        _chart.Invalidate();
        _status.Text = errorText ?? UiUtil.Str(this, Resource.String.live_status, _sgbd,
            string.Join(", ", _plan.Select(j => j.Job)), _data.Count.ToString(CultureInfo.CurrentCulture));
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
        _intervalBtn.Text = UiUtil.Str(this, Resource.String.live_interval, (IntervalsMs[_intervalIdx] / 1000.0).ToString("0.##", CultureInfo.CurrentCulture));
        _windowBtn.Text = UiUtil.Str(this, Resource.String.live_window, Windows[_windowIdx].ToString(CultureInfo.CurrentCulture));
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
            chip.SetAccessibilityDelegate(new ButtonDelegate(() => s.Visible));
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
        var color = LiveChartView.ColorOf(this, s.ColorIndex);
        var bg = new Android.Graphics.Drawables.GradientDrawable();
        bg.SetCornerRadius(UiUtil.Dp(this, 8));
        bg.SetColor(s.Visible ? color : UiUtil.Res(this, Resource.Color.m_surface));
        bg.SetStroke(UiUtil.Dp(this, 2), color);
        chip.Background = bg;
        // Texto por contraste sobre el relleno; oculta: texto normal sobre la superficie (el color queda en el borde).
        chip.SetTextColor(s.Visible ? LiveChartView.TextOn(color) : UiUtil.Res(this, Resource.Color.m_text));
    }

    void UpdateChipTexts()
    {
        for (int i = 0; i < _chipViews.Count && i < _data.Series.Count; i++)
        {
            var s = _data.Series[i];
            _chipViews[i].Text = UiUtil.Str(this, Resource.String.live_series_stats, s.Name, Num(s.Last), Num(s.Min), Num(s.Max));
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
        ResultExporter.ShareCsv(this, _data.ToCsv(ResultExporter.Cell),
            GetString(Resource.String.live_export_title)!);
    }

    // Selector de valores agrupado por página de status: solo jobs de lectura, hasta MaxChannels a la vez.
    void PickChannels()
    {
        var picked = new HashSet<ChassisValue>(_picked);
        var shown = new HashSet<ChassisValue>();
        int p = UiUtil.Dp(this, 16);
        var list = new LinearLayout(this) { Orientation = Orientation.Vertical };
        list.SetPadding(p, p / 2, p, 0);
        foreach (var page in _pages)
        {
            var values = page.Values.Where(v => JobPolicy.IsReadOnly(v.Job) && shown.Add(v)).ToList();
            if (values.Count == 0)
                continue;
            var header = new TextView(this) { Text = page.Title };
            header.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
            header.SetPadding(0, p / 2, 0, 0);
            list.AddView(header);
            foreach (var v in values)
            {
                var box = new CheckBox(this) { Text = v.Unit.Length > 0 ? $"{v.Label} ({v.Unit})" : v.Label, Checked = picked.Contains(v) };
                box.SetMinimumHeight(UiUtil.Dp(this, UiUtil.TouchDp));
                box.CheckedChange += (_, e) =>
                {
                    if (!e.IsChecked)
                        picked.Remove(v);
                    else if (picked.Count < MaxChannels)
                        picked.Add(v);
                    else
                    {
                        box.Checked = false;
                        UiUtil.Toast(this, UiUtil.Str(this, Resource.String.lv_channels_max, MaxChannels));
                    }
                };
                list.AddView(box);
            }
        }
        var scroll = new ScrollView(this);
        scroll.AddView(list);
        new AlertDialog.Builder(this)
            .SetTitle(UiUtil.Str(this, Resource.String.lv_channels_title, MaxChannels))!
            .SetView(scroll)!
            // En el orden del catálogo, no en el de los toques.
            .SetPositiveButton(Resource.String.lv_channels_apply, (_, _) => ApplyChannels(_pages.SelectMany(pg => pg.Values).Where(picked.Contains).Distinct().ToList()))!
            .SetNeutralButton(Resource.String.lv_channels_default, (_, _) => ApplyChannels([]))!
            .SetNegativeButton(Resource.String.lv_cancel, (_, _) => { })!
            .Show();
    }

    void ApplyChannels(List<ChassisValue> picked)
    {
        _picked = picked;
        _plan = picked.Count > 0 ? LiveJob.Group(picked) : [DefaultJob()];
        bool running = _poll.Running;
        Stop();
        _data.Clear();
        RefreshChips();
        _chart.Invalidate();
        // Las columnas cambian: si se está grabando, la siguiente muestra abre otro fichero.
        CloseLog();
        if (running)
            Start();
    }

    void Record(DateTime time, Dictionary<string, double> sample)
    {
        if (!_recording)
            return;
        if (_log?.Error != null)
        {
            StopRecording();   // avisa del error al cerrar el fichero
            return;
        }
        // Una serie nueva no cabe en la cabecera ya escrita (monitor sin resultados fijos): se sigue en otro fichero.
        if (_log != null && LiveLog.NeedsNewFile(_log.Columns, sample.Keys))
            CloseLog();
        if (_log == null)
        {
            try
            {
                _log = LiveLogFile.Create(this, LiveLog.Columns(_plan.SelectMany(j => j.Series).ToList(), sample.Keys));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                UiUtil.Toast(this, UiUtil.Str(this, Resource.String.lv_record_failed, UiUtil.Describe(ex)));
                StopRecording();
                return;
            }
            UiUtil.Toast(this, UiUtil.Str(this, Resource.String.lv_record_started, _log.Name));
        }
        _log.Append(time, sample);
    }

    void CloseLog()
    {
        _log?.CloseAsync();
        _log = null;
    }

    async void StopRecording()
    {
        _recording = false;
        _recordBtn.Text = GetString(Resource.String.lv_record_start);
        var log = _log;
        _log = null;
        if (log == null)
            return;
        await log.CloseAsync();
        if (IsFinishing || IsDestroyed)
            return;
        if (log.Error is Exception error)
        {
            UiUtil.Toast(this, UiUtil.Str(this, Resource.String.lv_record_failed, UiUtil.Describe(error)));
            return;
        }
        bool small = new Java.IO.File(log.FilePath).Length() <= MaxShareBytes;
        var dialog = new AlertDialog.Builder(this)
            .SetTitle(Resource.String.lv_record_saved)!
            .SetMessage(UiUtil.Str(this, small ? Resource.String.lv_record_saved_at : Resource.String.lv_record_saved_big, log.FilePath))!
            .SetNegativeButton(Resource.String.lv_record_close, (_, _) => { })!;
        if (small)
            dialog.SetPositiveButton(Resource.String.lv_record_share, (_, _) => ShareLog(log.FilePath));
        dialog.Show();
    }

    // ExportProvider solo sirve cache/exports: ShareCsv copia allí el contenido (por eso el tope de tamaño).
    async void ShareLog(string path)
    {
        string csv;
        try
        {
            csv = await Task.Run(() => File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UiUtil.Toast(this, UiUtil.Str(this, Resource.String.live_export_failed, UiUtil.Describe(ex)));
            return;
        }
        if (!IsFinishing && !IsDestroyed)
            ResultExporter.ShareCsv(this, csv, GetString(Resource.String.lv_record_share_title)!);
    }
}
