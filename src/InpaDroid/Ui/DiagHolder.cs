using Android.Content;
using InpaDroid.Diag;
using InpaDroid.Ui.E39;
using InpaDroid.Ui.E46;

namespace InpaDroid.Ui;

/// <summary>
/// Un único DiagService para toda la app. Se recrea al guardar los ajustes y se libera al salir.
/// </summary>
public static class DiagHolder
{
    static readonly object Sync = new();
    static DiagService? _service;
    // Cierre del servicio anterior en curso: el nuevo no debe abrir el cable mientras el viejo aún lo tiene,
    // porque la interfaz de ediabaslib es estática y el Dispose tardío del viejo cerraría el puerto del nuevo.
    static Task _closing = Task.CompletedTask;

    /// <summary>Carpeta ECU por defecto: &lt;almacenamiento externo de la app&gt;/Ecu.</summary>
    public static string DefaultEcuPath(Context ctx)
    {
        var baseDir = ctx.GetExternalFilesDir(null)?.AbsolutePath ?? ctx.FilesDir?.AbsolutePath ?? "";
        return Path.Combine(baseDir, "Ecu");
    }

    /// <summary>Ajustes guardados, con la carpeta ECU por defecto si no hay ninguna.</summary>
    public static AdapterSettings LoadSettings(Context ctx)
    {
        var app = ctx.ApplicationContext ?? ctx;
        var settings = AdapterSettings.Load(app);
        if (string.IsNullOrWhiteSpace(settings.EcuPath))
            settings.EcuPath = DefaultEcuPath(app);
        return settings;
    }

    /// <summary>Devuelve el servicio de la app (lo crea si hace falta). Puede lanzar excepción.</summary>
    public static DiagService Get(Context ctx)
    {
        lock (Sync)
        {
            if (_service == null)
            {
                _closing.Wait(TimeSpan.FromSeconds(4));
                var app = ctx.ApplicationContext ?? ctx;
                var settings = LoadSettings(app);
                TryCreateDirectory(settings.EcuPath);
                E39Catalog.TryLoadFromFolder(settings.EcuPath);
                E46Catalog.TryLoadFromFolder(settings.EcuPath);
                _service = new DiagService(app, settings);
            }
            return _service;
        }
    }

    /// <summary>Libera el servicio actual (en segundo plano); el siguiente Get crea uno nuevo.</summary>
    public static Task Reset()
    {
        DiagService? old;
        lock (Sync)
        {
            old = _service;
            _service = null;
        }
        if (old == null)
            return Task.CompletedTask;
        var closing = Task.Run(() =>
        {
            try { old.Abort(); } catch { }
            try { old.Dispose(); } catch { }
        });
        lock (Sync)
            _closing = closing;
        return closing;
    }

    public static void TryCreateDirectory(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path))
                Directory.CreateDirectory(path);
        }
        catch
        {
            // Sin permiso o ruta inválida: la pantalla de ajustes/selección lo mostrará al no encontrar archivos.
        }
    }
}
