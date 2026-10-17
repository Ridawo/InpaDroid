using InpaDroid.Diag;

namespace InpaDroid.Ui;

/// <summary>
/// Pantalla de centralita con memoria de errores (EcuActivity, ChassisEcuActivity): leer y guardar en el historial,
/// compartir CSV o informe, y borrar con copia de seguridad previa. Cada pantalla pone sus jobs y sus textos.
/// </summary>
public abstract class FaultScreenActivity : EcuScreenActivity
{
    /// <summary>Chasis y título de la centralita para el historial y el informe.</summary>
    protected abstract string ChassisName { get; }
    protected abstract string EcuTitle { get; }

    protected abstract string FaultReadJob { get; }
    protected abstract string FaultClearJob { get; }

    /// <summary>Encabezado de la lectura y del aviso tras el borrado.</summary>
    protected abstract string FaultsTitle { get; }

    protected abstract string ClearTitle { get; }
    protected abstract string ClearMessage(string name);
    protected abstract string ClearYes { get; }

    /// <summary>Se llama con una lectura de errores correcta, antes de guardarla.</summary>
    protected virtual void OnFaultsRead(JobResult r) { }

    protected void ConfirmClearFaults()
    {
        var name = _sgbd ?? EcuSgbd;
        JobDialogs.ConfirmJob(this, ClearTitle,
            ClearMessage(name) + "\n\n" + GetString(Resource.String.fb_clear_hint),
            name, FaultClearJob, "",
            ClearYes,
            onYes: () => Exclusive(async () =>
            {
                var sgbd = await EnsureSgbdAsync();
                if (sgbd == null)
                    return;
                // Copia de seguridad automática: la memoria de errores se lee y se guarda en el historial antes de borrarla.
                var backup = await ReadFaultsCore(sgbd, HistoryStore.KindBackup);
                // Abortado o pantalla cerrada (atrás del sistema, Abortar justo al acabar la lectura): no se borra nada.
                if (Stopped)
                    return;
                if (backup != null)
                    await ClearFaultsCore(sgbd, GetString(Resource.String.fb_backup_saved)!);
                else
                    JobDialogs.Confirm(this, GetString(Resource.String.fb_backup_failed_title)!, GetString(Resource.String.fb_backup_failed_msg)!,
                        GetString(Resource.String.fb_clear_anyway)!,
                        () => Exclusive(() => ClearFaultsCore(sgbd, GetString(Resource.String.fb_backup_none)!)));
            }),
            extra: (GetString(Resource.String.sf_save_faults)!, SaveFaults));
    }

    async Task ClearFaultsCore(string sgbd, string backupStatus)
    {
        await RunJobCore(sgbd, FaultClearJob, ClearTitle, "");
        _results.ShowInfo(FaultsTitle, backupStatus + "\n" + GetString(Resource.String.ecu_reread_faults), append: true);
    }

    // Lee la memoria de errores por la cola y, si la respuesta es correcta, la guarda en el historial; null si falla.
    async Task<JobResult?> ReadFaultsCore(string sgbd, string kind)
    {
        var r = await RunJobCore(sgbd, FaultReadJob, FaultsTitle, "");
        if (r == null || DiagLogic.StatusError(r, "") != null)
            return null;
        OnFaultsRead(r);
        HistoryStore.Save(this, ChassisName, EcuTitle, sgbd, kind, r);
        return r;
    }

    // F4, «Guardar errores» e Informe: lee la memoria de errores y, si se ha leído bien, pasa la respuesta a then.
    protected void ReadFaults(Action<string, JobResult>? then = null) =>
        Exclusive(async () =>
        {
            var sgbd = await EnsureSgbdAsync();
            if (sgbd != null && await ReadFaultsCore(sgbd, HistoryStore.KindRead) is { } r)
                then?.Invoke(sgbd, r);
        });

    // "Guardar errores" del aviso de borrado: lee la memoria de errores y la comparte en CSV.
    void SaveFaults() => ReadFaults((_, _) => _results.Share());

    protected void ShareReport() => ReadFaults((sgbd, r) => ReportExporter.Share(this, ChassisName, EcuTitle, sgbd, r));
}
