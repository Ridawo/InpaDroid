using Android.App;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Text.Style;
using Android.Util;
using Android.Views;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

public abstract class InpaActivity : Activity
{
    const int HeaderPollMs = 2000;
    const int HeaderPollAfterErrorMs = 6000;

    sealed class KeyDef
    {
        public string Label = "";
        public Action? Action;
    }

    // 0..9 = F1..F10, 10..19 = Shift+F1..F10
    readonly KeyDef?[] _keys = new KeyDef?[20];
    readonly TextView?[] _keyViews = new TextView?[10];
    TextView? _shiftView;
    bool _shift;

    LinearLayout? _keyBar;
    TextView? _title, _subtitle, _battText, _ignText;
    View? _battLed, _ignLed;
    View? _busy;
    TextView? _busyText;
    Button? _busyButton;
    Action? _busyAction;
    CancellationTokenSource? _headerCts;

    /// <summary>
    /// true mientras la pantalla tiene un job o un status en marcha: la cabecera deja de leer UTILITY para no
    /// cambiar de SGBD (en línea K cada cambio recarga el .prg y reinicia la comunicación) ni retrasar el job.
    /// </summary>
    protected virtual bool PauseHeader => false;

    /// <summary>Servicio de diagnóstico de la app. Puede lanzar si no se puede crear: usar dentro de try.</summary>
    protected DiagService Diag => DiagHolder.Get(this);

    protected void InitInpa(int layoutId, string title)
    {
        SetContentView(layoutId);
        _title = FindViewById<TextView>(Resource.Id.inpa_title);
        _subtitle = FindViewById<TextView>(Resource.Id.inpa_subtitle);
        _battLed = FindViewById(Resource.Id.inpa_batt_led);
        _battText = FindViewById<TextView>(Resource.Id.inpa_batt_text);
        _ignLed = FindViewById(Resource.Id.inpa_ign_led);
        _ignText = FindViewById<TextView>(Resource.Id.inpa_ign_text);
        _keyBar = FindViewById<LinearLayout>(Resource.Id.inpa_fkeys);
        _busy = FindViewById(Resource.Id.inpa_busy);
        _busyText = FindViewById<TextView>(Resource.Id.inpa_busy_text);
        _busyButton = FindViewById<Button>(Resource.Id.inpa_busy_abort);
        if (_busyButton != null)
            _busyButton.Click += (_, _) => _busyAction?.Invoke();
        SetTitleText(title);
        BuildKeyBar();
    }

    protected void SetTitleText(string title)
    {
        if (_title != null) _title.Text = title;
    }

    protected void SetSubtitle(string text)
    {
        if (_subtitle != null) _subtitle.Text = text;
    }

    protected void SetKey(int f, string label, Action action) =>
        _keys[f - 1] = new KeyDef { Label = label, Action = action };

    protected void SetShiftKey(int f, string label, Action action) =>
        _keys[f + 9] = new KeyDef { Label = label, Action = action };

    /// <summary>Teclas definidas, en orden: primero F1..F10, luego Shift+F1..F10.</summary>
    protected IEnumerable<(int F, bool Shift, string Label)> DefinedKeys()
    {
        for (int i = 0; i < 20; i++)
        {
            var k = _keys[i];
            if (k?.Action != null)
                yield return (i % 10 + 1, i >= 10, k.Label);
        }
    }

    /// <summary>Texto estilo INPA: "&lt; F1 &gt;  Info" o "&lt;Shift&gt; + &lt; F4 &gt;  Borrar".</summary>
    protected static string KeyCaption(int f, bool shift, string label)
    {
        var key = f == 10 ? "< F10>" : $"< F{f} >";
        return shift ? $"<Shift> + {key}  {label}" : $"{key}  {label}";
    }

    /// <summary>Se llama antes de ejecutar cualquier tecla F. Devolver false para ignorarla.</summary>
    protected virtual bool BeforeKey(int f, bool shift) => true;

    protected void PressKey(int f, bool shift)
    {
        if (_shift)
        {
            // Shift en pantalla funciona como en INPA con una sola pulsación: se suelta tras usarlo.
            _shift = false;
            UpdateKeyLabels();
        }
        var key = _keys[(shift ? 10 : 0) + f - 1];
        if (key?.Action == null)
            return;
        if (!BeforeKey(f, shift))
            return;
        try
        {
            key.Action();
        }
        catch (Exception ex)
        {
            UiUtil.Toast(this, "Error: " + UiUtil.Describe(ex));
        }
    }

    void BuildKeyBar()
    {
        if (_keyBar == null)
            return;
        _keyBar.RemoveAllViews();

        _shiftView = MakeKeyView();
        _shiftView.Click += (_, _) =>
        {
            _shift = !_shift;
            UpdateKeyLabels();
        };
        for (int i = 0; i < 10; i++)
        {
            int f = i + 1;
            var v = MakeKeyView();
            v.Click += (_, _) => PressKey(f, _shift);
            _keyViews[i] = v;
        }

        bool wide = (Resources?.Configuration?.ScreenWidthDp ?? 0) >= 600;
        if (wide)
        {
            // Pantalla ancha (horizontal): Shift + F1..F10 en una fila, como en INPA.
            var row = NewRow();
            row.AddView(_shiftView, KeyParams());
            foreach (var v in _keyViews)
                row.AddView(v, KeyParams());
            _keyBar.AddView(row);
        }
        else
        {
            // Vertical: Shift alto a la izquierda + dos filas de 5.
            var outer = NewRow();
            outer.AddView(_shiftView, KeyParams());
            var rows = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
            for (int r = 0; r < 2; r++)
            {
                var row = NewRow();
                for (int c = 0; c < 5; c++)
                    row.AddView(_keyViews[r * 5 + c], KeyParams());
                rows.AddView(row, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent,
                    LinearLayout.LayoutParams.WrapContent));
            }
            outer.AddView(rows, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 5f));
            _keyBar.AddView(outer);
        }
        UpdateKeyLabels();
    }

    LinearLayout NewRow() => new(this) { Orientation = Android.Widget.Orientation.Horizontal };

    LinearLayout.LayoutParams KeyParams()
    {
        var lp = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MatchParent, 1f);
        int m = UiUtil.Dp(this, 1.5f);
        lp.SetMargins(m, m, m, m);
        return lp;
    }

    TextView MakeKeyView()
    {
        var tv = new TextView(this)
        {
            Gravity = GravityFlags.Center,
            Clickable = true,
            Focusable = false,
        };
        tv.SetMinHeight(UiUtil.Dp(this, 44));
        tv.SetMaxLines(3);
        tv.SetTextSize(ComplexUnitType.Sp, 11);
        tv.SetTextColor(Color.Black);
        int p = UiUtil.Dp(this, 2);
        tv.SetPadding(p, p, p, p);
        return tv;
    }

    Drawable KeyBackground(Color color)
    {
        GradientDrawable Shape(Color c)
        {
            var d = new GradientDrawable();
            d.SetColor(c);
            d.SetStroke(UiUtil.Dp(this, 1), UiUtil.KeyBorder);
            d.SetCornerRadius(UiUtil.Dp(this, 3));
            return d;
        }
        var states = new StateListDrawable();
        states.AddState(new[] { Android.Resource.Attribute.StatePressed }, Shape(UiUtil.KeyPressed));
        states.AddState(Array.Empty<int>(), Shape(color));
        return states;
    }

    void UpdateKeyLabels()
    {
        if (_shiftView != null)
        {
            _shiftView.TextFormatted = KeyText("Shift", _shift ? "activo" : "");
            _shiftView.Background = KeyBackground(_shift ? UiUtil.ShiftOn : UiUtil.KeyBg);
        }
        for (int i = 0; i < 10; i++)
        {
            var v = _keyViews[i];
            if (v == null) continue;
            var key = _keys[(_shift ? 10 : 0) + i];
            bool enabled = key?.Action != null;
            v.TextFormatted = KeyText((_shift ? "⇧F" : "F") + (i + 1), enabled ? key!.Label : "");
            v.Enabled = enabled;
            v.Alpha = enabled ? 1f : 0.45f;
            v.Background = KeyBackground(UiUtil.KeyBg);
        }
    }

    static SpannableString KeyText(string key, string label)
    {
        var s = new SpannableString(label.Length > 0 ? key + "\n" + label : key);
        s.SetSpan(new StyleSpan(TypefaceStyle.Bold), 0, key.Length, SpanTypes.ExclusiveExclusive);
        return s;
    }

    /// <summary>Llamar tras cambiar teclas con SetKey para refrescar las etiquetas.</summary>
    protected void RefreshKeys() => UpdateKeyLabels();

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        int f = keyCode switch
        {
            Keycode.F1 => 1, Keycode.F2 => 2, Keycode.F3 => 3, Keycode.F4 => 4, Keycode.F5 => 5,
            Keycode.F6 => 6, Keycode.F7 => 7, Keycode.F8 => 8, Keycode.F9 => 9, Keycode.F10 => 10,
            _ => 0,
        };
        if (keyCode == Keycode.Escape && _keys[9]?.Action != null)
        {
            // Escape es siempre F10 (volver), aunque el Shift de pantalla esté pulsado.
            if ((e?.RepeatCount ?? 0) == 0)
                PressKey(10, false);
            return true;
        }
        if (f > 0)
        {
            if ((e?.RepeatCount ?? 0) == 0)
                PressKey(f, (e?.IsShiftPressed ?? false) || _shift);
            return true;
        }
        return base.OnKeyDown(keyCode, e);
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        BuildKeyBar();
    }

    /// <summary>Muestra la barra de trabajo. Por defecto el botón llama a DiagService.Abort().</summary>
    protected void ShowBusy(string text, string buttonText = "Abortar", Action? onButton = null)
    {
        if (_busy == null) return;
        if (_busyText != null) _busyText.Text = text;
        if (_busyButton != null) _busyButton.Text = buttonText;
        _busyAction = onButton ?? AbortJob;
        _busy.Visibility = ViewStates.Visible;
    }

    protected void HideBusy()
    {
        if (_busy != null)
            _busy.Visibility = ViewStates.Gone;
        _busyAction = null;
    }

    protected void AbortJob()
    {
        try
        {
            Diag.Abort();
            if (_busyText != null) _busyText.Text = "Abortando…";
        }
        catch (Exception ex)
        {
            UiUtil.Toast(this, UiUtil.Describe(ex));
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (DiagHolder.LoadSettings(this).Type == AdapterType.UsbKDcan)
            UsbPermission.RequestIfNeeded(this);
        StartHeaderPolling();
    }

    protected override void OnPause()
    {
        StopHeaderPolling();
        base.OnPause();
    }

    void StopHeaderPolling()
    {
        _headerCts?.Cancel();
        _headerCts = null;
    }

    async void StartHeaderPolling()
    {
        StopHeaderPolling();
        var cts = new CancellationTokenSource();
        _headerCts = cts;
        while (!cts.IsCancellationRequested)
        {
            double? ubatt = null;
            bool? ignition = null;
            bool failed = false;
            if (PauseHeader)
            {
                try { await Task.Delay(HeaderPollMs, cts.Token); }
                catch (OperationCanceledException) { break; }
                continue;
            }
            try
            {
                (ubatt, ignition) = await Diag.ReadBatteryIgnitionAsync();
            }
            catch
            {
                failed = true;
            }
            if (cts.IsCancellationRequested)
                break;
            SetIndicators(ubatt, ignition);
            int delay = failed || (ubatt == null && ignition == null) ? HeaderPollAfterErrorMs : HeaderPollMs;
            try
            {
                await Task.Delay(delay, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    void SetIndicators(double? ubatt, bool? ignition)
    {
        // Como ShowBatteryIgnition de INPA: sin batería, el encendido también se da por apagado.
        bool battOn = ubatt is > 0;
        if (_battLed != null) _battLed.SetBackgroundColor(battOn ? UiUtil.LedOn : UiUtil.LedOff);
        if (_battText != null)
            _battText.Text = ubatt == null ? "--" : battOn ? $"on  {ubatt.Value:0.0} V" : "off";
        bool ignOn = battOn && ignition == true;
        if (_ignLed != null) _ignLed.SetBackgroundColor(ignOn ? UiUtil.LedOn : UiUtil.LedOff);
        if (_ignText != null)
            _ignText.Text = ignition == null ? "--" : ignOn ? "on" : "off";
    }

}
