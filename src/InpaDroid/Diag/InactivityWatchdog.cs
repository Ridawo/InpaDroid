namespace InpaDroid.Diag;

// Vigila una operación del hilo de trabajo por inactividad: vence cuando pasa `limit` sin ninguna llamada a Kick.
// DiagService llama a Kick desde AbortJobFunc, que EDIABAS consulta antes de cada instrucción del job, así que un
// job largo que sigue avanzando no vence nunca y uno bloqueado esperando al adaptador sí.
// Check/Finish se sincronizan por lock para que una cancelación tardía nunca caiga sobre el job siguiente.
// Sin dependencias de Android ni de EdiabasLib (se prueba en InpaDroid.Tests).
internal sealed class InactivityWatchdog
{
    private readonly object _lock = new();
    private readonly Action _onExpire;
    private readonly long _limitMs;
    private readonly Timer _timer;
    private long _lastActivity;
    private bool _finished;
    private volatile bool _fired;

    public InactivityWatchdog(TimeSpan limit, Action onExpire)
    {
        _onExpire = onExpire;
        _limitMs = (long)limit.TotalMilliseconds;
        _lastActivity = Environment.TickCount64;
        // Se arma después de asignar _timer: Check puede reprogramarlo.
        _timer = new Timer(_ => Check(), null, Timeout.Infinite, Timeout.Infinite);
        _timer.Change(_limitMs, Timeout.Infinite);
    }

    public bool Fired => _fired;

    // Hay actividad: el plazo vuelve a contar desde ahora. Barato, se llama en cada instrucción de EDIABAS.
    public void Kick() => Volatile.Write(ref _lastActivity, Environment.TickCount64);

    private void Check()
    {
        lock (_lock)
        {
            if (_finished)
            {
                return;
            }
            long remaining = _limitMs - (Environment.TickCount64 - Volatile.Read(ref _lastActivity));
            if (remaining > 0)
            {
                _timer.Change(remaining, Timeout.Infinite);
                return;
            }
            _fired = true;
            _onExpire();
        }
    }

    // Detiene la vigilancia. Devuelve true si el plazo llegó a vencer.
    public bool Finish()
    {
        lock (_lock)
        {
            _finished = true;
            _timer.Dispose();
            return _fired;
        }
    }
}
