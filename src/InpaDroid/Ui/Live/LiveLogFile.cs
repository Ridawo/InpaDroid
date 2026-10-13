using System.Text;
using Android.Content;
using InpaDroid.Diag;

namespace InpaDroid.Ui.Live;

/// <summary>
/// Grabación del monitor en vivo a un CSV en files/logs de la app (almacenamiento externo si lo hay, si no interno).
/// Las escrituras se encadenan en segundo plano para no bloquear el hilo de UI; el formato lo pone <see cref="LiveLog"/>.
/// Se usa desde el hilo de UI.
/// </summary>
sealed class LiveLogFile
{
    readonly StreamWriter _writer;
    Task _tail = Task.CompletedTask;
    volatile Exception? _error;

    public string FilePath { get; }
    public string Name => System.IO.Path.GetFileName(FilePath);
    public IReadOnlyList<string> Columns { get; }
    /// <summary>Primer fallo de escritura (disco lleno, almacenamiento retirado); a partir de ahí no se escribe más.</summary>
    public Exception? Error => _error;

    LiveLogFile(string path, IReadOnlyList<string> columns)
    {
        FilePath = path;
        Columns = columns;
        // BOM como en ResultExporter, para que Excel lea bien los acentos.
        _writer = new StreamWriter(path, false, new UTF8Encoding(true));
        string header = LiveLog.Header(columns, ResultExporter.Cell);
        Run(w => w.Write(header));
    }

    /// <summary>Crea un fichero nuevo con su cabecera; lanza IOException/UnauthorizedAccessException si no se puede.</summary>
    public static LiveLogFile Create(Context ctx, IReadOnlyList<string> columns)
    {
        var dir = ctx.GetExternalFilesDir("logs") ?? new Java.IO.File(ctx.FilesDir, "logs");
        dir.Mkdirs();
        string name = LiveLog.FileName(DateTime.Now, n => File.Exists(System.IO.Path.Combine(dir.AbsolutePath, n)));
        return new LiveLogFile(System.IO.Path.Combine(dir.AbsolutePath, name), columns);
    }

    public void Append(DateTime time, IReadOnlyDictionary<string, double> sample)
    {
        string row = LiveLog.Row(time, Columns, sample);
        Run(w => w.Write(row));
    }

    public void Flush() => Run(w => w.Flush());

    /// <summary>Cierra el fichero tras las escrituras pendientes; la tarea acaba cuando está en disco y nunca falla.</summary>
    public Task CloseAsync() => _tail = _tail.ContinueWith(_ =>
    {
        try
        {
            _writer.Dispose();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _error ??= ex;
        }
    }, TaskScheduler.Default);

    void Run(Action<StreamWriter> write) => _tail = _tail.ContinueWith(_ =>
    {
        if (_error != null)
            return;
        try
        {
            write(_writer);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _error = ex;
        }
    }, TaskScheduler.Default);
}
