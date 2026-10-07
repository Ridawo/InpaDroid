namespace InpaDroid.Ui;

/// <summary>
/// Bucle de sondeo: una iteración cada intervalo, sin solaparse (se espera cada una antes de la siguiente).
/// Start/Stop son idempotentes. El runner no toca ediabaslib: el trabajo lo hace la iteración vía DiagService.
/// </summary>
public sealed class PollRunner
{
    CancellationTokenSource? _cts;

    public bool Running => _cts != null;

    /// <summary>True si el token pertenece al sondeo en marcha (no a uno ya detenido y reiniciado).</summary>
    public bool IsCurrent(CancellationToken ct) => _cts?.Token == ct;

    /// <summary>
    /// Arranca el sondeo; no hace nada si ya está en marcha. <paramref name="poll"/> devuelve false para terminar el bucle.
    /// Las excepciones de la iteración van a <paramref name="onError"/> salvo que ya se haya detenido.
    /// </summary>
    public bool Start(Func<CancellationToken, Task<bool>> poll, Action<Exception> onError, int intervalMs, int minDelayMs)
    {
        if (_cts != null)
            return false;
        var cts = new CancellationTokenSource();
        _cts = cts;
        Loop(cts, poll, onError, intervalMs, minDelayMs);
        return true;
    }

    /// <summary>Detiene el sondeo; devuelve false si no estaba en marcha.</summary>
    public bool Stop()
    {
        if (_cts == null)
            return false;
        _cts.Cancel();
        _cts = null;
        return true;
    }

    static async void Loop(CancellationTokenSource cts, Func<CancellationToken, Task<bool>> poll,
        Action<Exception> onError, int intervalMs, int minDelayMs)
    {
        var ct = cts.Token;
        while (!ct.IsCancellationRequested)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (!await poll(ct))
                    break;
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested)
                    break;
                onError(ex);
            }
            try
            {
                await Task.Delay((int)Math.Max(minDelayMs, intervalMs - sw.ElapsedMilliseconds), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        // Tras Stop nadie más usa este token; si salió por la iteración, Stop aún debe poder cancelarlo.
        if (ct.IsCancellationRequested)
            cts.Dispose();
    }
}
