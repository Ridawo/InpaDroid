using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

/// <summary>
/// Base de las pantallas que trabajan con una centralita (EcuActivity, ChassisEcuActivity, CodingActivity):
/// operación exclusiva, resolución de variante (.grp -> .prg), lista de jobs y ejecución de un job descartando
/// resultados de operaciones ya sustituidas. Cada pantalla pone su SGBD y sus textos.
/// </summary>
public abstract class EcuScreenActivity : InpaActivity
{
    private protected ResultPanel _results = null!;
    protected TextView _heading = null!;
    protected string? _sgbd;                // variante real (.prg) una vez resuelta
    protected bool _busyJob;                // operación en curso (bloquea otras teclas salvo F10)
    bool _abortRequested;                   // el usuario ha abortado la operación en curso (Exclusive lo limpia)
    protected int _generation;              // descarta resultados de operaciones ya sustituidas
    IReadOnlyList<JobInfo>? _jobs;

    /// <summary>SGBD pedida (.grp o .prg), antes de resolver la variante.</summary>
    protected abstract string EcuSgbd { get; }

    /// <summary>Texto de la barra de trabajo mientras se identifica la variante.</summary>
    protected abstract string VariantBusyText { get; }

    /// <summary>No se pudo identificar la variante. notify: aviso emergente además del panel.</summary>
    protected abstract void OnVariantFailed(bool notify);

    protected virtual void OnVariantFound() { }

    /// <summary>Se llama antes de mostrar un job nuevo (p. ej. para limpiar otra vista de resultados).</summary>
    protected virtual void BeforeJob() { }

    /// <summary>Se llama tras mostrar la respuesta de un job.</summary>
    protected virtual void OnJobResult(JobResult r) { }

    /// <summary>true si la barra de trabajo debe quedarse al acabar la operación (p. ej. el Status que ha arrancado).</summary>
    protected virtual bool KeepBusyBar => false;

    /// <summary>Hay algo en curso que abortar al cerrar la pantalla.</summary>
    protected virtual bool InFlight => _busyJob;

    protected override bool PauseHeader => _busyJob;

    // Operación abortada o pantalla cerrándose: no se avisa ni se encadena nada más.
    protected bool Stopped => _abortRequested || IsFinishing || IsDestroyed;

    protected override bool BeforeKey(int f, bool shift)
    {
        if (_busyJob && !(f == 10 && !shift))
        {
            UiUtil.Toast(this, GetString(Resource.String.ecu_busy_job)!);
            return false;
        }
        // F10 (volver) con algo en curso: se aborta antes de cerrar.
        if (f == 10 && !shift && InFlight)
            AbortFlow();
        return true;
    }

    // Abortar de la barra de trabajo: además de cancelar el job, corta la operación (p. ej. el borrado tras la copia).
    protected void AbortFlow()
    {
        _abortRequested = true;
        AbortJob();
    }

    // Atrás del sistema o cierre sin pasar por F10: el job en curso no debe seguir ni encadenar otro.
    protected override void OnDestroy()
    {
        if (InFlight)
            AbortFlow();
        base.OnDestroy();
    }

    protected async void Exclusive(Func<Task> work)
    {
        if (_busyJob)
            return;
        _busyJob = true;
        _abortRequested = false;
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            _results.ShowError(_sgbd ?? EcuSgbd, "ERROR: " + UiUtil.Describe(ex));
        }
        finally
        {
            _busyJob = false;
            if (!KeepBusyBar)
                HideBusy();
        }
    }

    // null si falla; se reintenta con la siguiente tecla.
    // notify: aviso emergente además del panel; sin él, una tecla que falla con el mismo error en pantalla parece no hacer nada.
    protected async Task<string?> EnsureSgbdAsync(bool notify = true)
    {
        if (_sgbd != null)
            return _sgbd;
        ShowBusy(VariantBusyText, null, AbortFlow);
        var variant = await Diag.ResolveVariantAsync(EcuSgbd);
        HideBusy();
        // Abortado o pantalla cerrada: la variante vacía no es un fallo que haya que avisar.
        if (Stopped)
            return null;
        if (string.IsNullOrWhiteSpace(variant))
        {
            OnVariantFailed(notify);
            return null;
        }
        _sgbd = variant;
        OnVariantFound();
        return _sgbd;
    }

    protected async Task<IReadOnlyList<JobInfo>?> EnsureJobsAsync(string sgbd)
    {
        if (_jobs is { Count: > 0 })
            return _jobs;
        ShowBusy(UiUtil.Str(this, Resource.String.g_jobs_reading, sgbd));
        var jobs = await Diag.GetJobsAsync(sgbd);
        HideBusy();
        if (jobs.Count == 0)
        {
            _results.ShowError(sgbd, UiUtil.Str(this, Resource.String.g_jobs_failed, sgbd));
            return null;
        }
        _jobs = jobs;
        return jobs;
    }

    protected async Task<JobResult?> RunJobCore(string sgbd, string job, string heading, string args, string results = "")
    {
        int gen = ++_generation;
        BeforeJob();
        _heading.Text = heading;
        ShowBusy($"{sgbd} : {job}…", null, AbortFlow);
        try
        {
            var r = await Diag.RunJobAsync(sgbd, job, args, results);
            if (gen != _generation)
                return null;
            _results.ShowResult(sgbd, job, r);
            OnJobResult(r);
            return r;
        }
        catch (Exception ex)
        {
            if (gen == _generation)
                _results.ShowException(sgbd, job, ex);
            return null;
        }
    }
}
