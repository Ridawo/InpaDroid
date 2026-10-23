using Android.Content;
using InpaDroid.Diag;
using InpaDroid.Ui.Chassis;

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

    /// <summary>Carga los catálogos de chasis en el idioma de los recursos de la app, con los override de la carpeta ECU.</summary>
    public static void LoadCatalogs(Context ctx, string ecuPath)
    {
        ChassisCatalog.Language = ctx.GetString(Resource.String.catalog_lang)!;
        ChassisCatalog.TryLoadFromFolder(ecuPath);
    }

    /// <summary>Ajustes guardados, con la carpeta ECU por defecto si no hay ninguna.</summary>
    public static AdapterSettings LoadSettings(Context ctx) => AdapterSettings.Load(ctx.ApplicationContext ?? ctx);

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
                LoadCatalogs(app, settings.EcuPath);
                _service = new DiagService(app, settings);
            }
            return _service;
        }
    }

    /// <summary>Libera el servicio actual (en segundo plano); el siguiente Get crea uno nuevo.</summary>
    public static Task Reset()
    {
        // _closing se publica en el mismo lock que vacía _service: un Get entre medias abriría el cable sin esperar al cierre.
        lock (Sync)
        {
            var old = _service;
            _service = null;
            // Sin servicio puede seguir en curso un cierre anterior: quien espera a Reset espera también a ese.
            if (old == null)
                return _closing;
            return _closing = Task.Run(() =>
            {
                try { old.Abort(); } catch { }
                try { old.Dispose(); } catch { }
            });
        }
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
