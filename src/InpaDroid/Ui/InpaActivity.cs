using Android.App;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
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
        var back = FindViewById(Resource.Id.inpa_back);
        if (back != null)
            back.Click += (_, _) =>
            {
                // Atrás = la tecla "volver" (F10) de la pantalla si existe; si no, el atrás del sistema.
                if (_keys[9]?.Action != null) PressKey(10, false);
                else OnBackPressed();
            };
        SetTitleText(title);
        BuildKeyGrid();
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

    /// <summary>Texto estilo INPA para listados de texto: "&lt; F1 &gt;  Info" o "&lt;Shift&gt; + &lt; F4 &gt;  Borrar".</summary>
    protected static string KeyCaption(int f, bool shift, string label)
    {
        var key = f == 10 ? "< F10>" : $"< F{f} >";
        return shift ? $"<Shift> + {key}  {label}" : $"{key}  {label}";
    }

    /// <summary>Se llama antes de ejecutar cualquier tecla F. Devolver false para ignorarla.</summary>
    protected virtual bool BeforeKey(int f, bool shift) => true;

    protected void PressKey(int f, bool shift)
    {
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

    int KeyColumns()
    {
        int w = Resources?.Configuration?.ScreenWidthDp ?? 0;
        int h = Resources?.Configuration?.ScreenHeightDp ?? 0;
        // En apaisado el alto es escaso: más columnas para que las tarjetas no se coman los resultados.
        if (w > h && h > 0)
            return w >= 560 ? 4 : 3;
        return w >= 900 ? 4 : w >= 600 ? 3 : 2;
    }

    /// <summary>Dibuja las teclas definidas como tarjetas en una cuadrícula; las Shift (destructivas) al final.</summary>
    void BuildKeyGrid()
    {
        if (_keyBar == null)
            return;
        _keyBar.RemoveAllViews();
        int cols = KeyColumns();
        LinearLayout? row = null;
        int n = 0;
        for (int i = 0; i < _keys.Length; i++)
        {
            var key = _keys[i];
            // F10 (Volver) ya lo hace la flecha de la cabecera: no se dibuja como tarjeta.
            if (key?.Action == null || i == 9)
                continue;
            if (n++ % cols == 0)
            {
                row = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Horizontal };
                _keyBar.AddView(row, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent));
            }
            row!.AddView(MakeKeyView(key.Label, i % 10 + 1, i >= 10), KeyParams());
        }
        // Relleno de la última fila para que las tarjetas mantengan el ancho.
        // Alto 0 explícito: una View con wrap_content se estira a todo el espacio disponible y aplastaría el resto.
        for (int pad = (cols - n % cols) % cols; pad > 0; pad--)
            row!.AddView(new View(this), new LinearLayout.LayoutParams(0, 0, 1f));
        _keyBar.Visibility = n == 0 ? ViewStates.Gone : ViewStates.Visible;
    }

    LinearLayout.LayoutParams KeyParams()
    {
        var lp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
        int m = UiUtil.Dp(this, 4);
        lp.SetMargins(m, m, m, m);
        return lp;
    }

    TextView MakeKeyView(string label, int f, bool danger)
    {
        var tv = new TextView(this)
        {
            Text = label,
            Gravity = GravityFlags.Center,
            Clickable = true,
            Focusable = false,
        };
        tv.SetMinHeight(UiUtil.Dp(this, 56));
        tv.SetMaxLines(3);
        tv.SetTextSize(ComplexUnitType.Sp, 15);
        tv.SetTypeface(null, TypefaceStyle.Bold);
        tv.SetTextColor(Resources!.GetColor(danger ? Resource.Color.m_error : Resource.Color.m_text, Theme));
        tv.SetBackgroundResource(danger ? Resource.Drawable.bg_card_danger : Resource.Drawable.bg_card);
        int p = UiUtil.Dp(this, 8);
        tv.SetPadding(p, p, p, p);
        tv.Click += (_, _) => PressKey(f, danger);
        return tv;
    }

    /// <summary>Llamar tras cambiar teclas con SetKey para refrescar las tarjetas.</summary>
    protected void RefreshKeys() => BuildKeyGrid();

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
            // Escape es siempre F10 (volver).
            if ((e?.RepeatCount ?? 0) == 0)
                PressKey(10, false);
            return true;
        }
        if (f > 0)
        {
            if ((e?.RepeatCount ?? 0) == 0)
                PressKey(f, e?.IsShiftPressed ?? false);
            return true;
        }
        return base.OnKeyDown(keyCode, e);
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        BuildKeyGrid();
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
        if (_battLed != null) SetLed(_battLed, battOn);
        if (_battText != null)
            _battText.Text = ubatt == null ? "--" : battOn ? $"on  {ubatt.Value:0.0} V" : "off";
        bool ignOn = battOn && ignition == true;
        if (_ignLed != null) SetLed(_ignLed, ignOn);
        if (_ignText != null)
            _ignText.Text = ignition == null ? "--" : ignOn ? "on" : "off";
    }

    static void SetLed(View led, bool on)
    {
        var c = on ? UiUtil.LedOn : UiUtil.LedOff;
        if (led.Background?.Mutate() is GradientDrawable g) g.SetColor(c);
        else led.SetBackgroundColor(c);
    }
}
