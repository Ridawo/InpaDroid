using System.IO.Compression;
using Android.App;
using Android.Content;
using Android.Provider;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

/// <summary>
/// Importa archivos ECU (.prg/.grp, sueltos o dentro de un ZIP) elegidos con el selector de documentos del
/// sistema a la carpeta ECU. No necesita permiso de almacenamiento. Las reglas están en <see cref="EcuImport"/>.
/// </summary>
public static class EcuImporter
{
    /// <summary>true mientras hay una importación en curso (solo se escribe desde el hilo de la UI).</summary>
    public static volatile bool Running;

    /// <summary>Abre el selector de documentos (varios archivos permitidos).</summary>
    public static void Start(Activity activity, int requestCode)
    {
        if (Running)
        {
            UiUtil.Toast(activity, UiUtil.Str(activity, Resource.String.im_busy, ""));
            return;
        }
        try
        {
            var intent = new Intent(Intent.ActionOpenDocument)
                .AddCategory(Intent.CategoryOpenable)
                .SetType("*/*")
                .PutExtra(Intent.ExtraAllowMultiple, true);
            activity.StartActivityForResult(intent, requestCode);
        }
        catch (Exception ex)
        {
            UiUtil.Toast(activity, UiUtil.Str(activity, Resource.String.im_picker_error, UiUtil.Describe(ex)));
        }
    }

    /// <summary>
    /// Procesa el resultado del selector. <paramref name="showBusy"/> recibe el texto de progreso y la acción
    /// de cancelar; <paramref name="done"/> recibe el resumen y si hubo fallos. Todo en el hilo de la UI.
    /// </summary>
    public static async Task HandleResult(Activity activity, Result resultCode, Intent? data, string ecuPath,
        Action<string, Action> showBusy, Action hideBusy, Action<string, bool> done)
    {
        var uris = PickedUris(resultCode, data);
        if (uris.Count == 0)
            return;
        if (Running)
        {
            UiUtil.Toast(activity, UiUtil.Str(activity, Resource.String.im_busy, ""));
            return;
        }
        Running = true;
        using var cts = new CancellationTokenSource();
        var resolver = activity.ContentResolver!;
        string cacheDir = activity.CacheDir!.AbsolutePath;
        void Progress(string name) => activity.RunOnUiThread(() =>
            showBusy(UiUtil.Str(activity, Resource.String.im_busy, name), cts.Cancel));
        var sum = new ImportSummary();
        try
        {
            Progress("");
            await Task.Run(() => Import(resolver, uris, ecuPath, cacheDir, sum, Progress, cts.Token));
        }
        catch (OperationCanceledException)
        {
            sum.Cancelled = true;
        }
        catch (Exception ex)
        {
            sum.Fail("", UiUtil.Describe(ex));
        }
        finally
        {
            Running = false;
        }
        hideBusy();
        string text = UiUtil.Str(activity, Resource.String.im_result, sum.Imported, sum.Skipped, sum.Failed);
        if (sum.Cancelled)
            text += "\n" + activity.GetString(Resource.String.im_cancelled);
        if (sum.FirstError != null)
            text += "\n" + UiUtil.Str(activity, Resource.String.im_first_error, sum.FirstError);
        done(text, sum.Failed > 0);
    }

    static List<Android.Net.Uri> PickedUris(Result resultCode, Intent? data)
    {
        var list = new List<Android.Net.Uri>();
        if (resultCode != Result.Ok || data == null)
            return list;
        var clip = data.ClipData;
        if (clip != null)
        {
            for (int i = 0; i < clip.ItemCount; i++)
                if (clip.GetItemAt(i)?.Uri is { } u)
                    list.Add(u);
        }
        else if (data.Data != null)
            list.Add(data.Data);
        return list;
    }

    sealed class State
    {
        public HashSet<string> Names = new(StringComparer.Ordinal);
        public long Total;
    }

    static void Import(ContentResolver resolver, List<Android.Net.Uri> uris, string dir, string cacheDir,
        ImportSummary sum, Action<string> progress, CancellationToken ct)
    {
        DiagHolder.TryCreateDirectory(dir);
        // Temporales de una importación anterior interrumpida.
        try
        {
            foreach (var stale in Directory.EnumerateFiles(dir, "*.imp"))
                try { File.Delete(stale); } catch (IOException) { }
        }
        catch (Exception) { }
        var st = new State();
        foreach (var f in Directory.EnumerateFiles(dir))
            st.Names.Add(Path.GetFileName(f));

        foreach (var uri in uris)
        {
            ct.ThrowIfCancellationRequested();
            string display = DisplayName(resolver, uri);
            try
            {
                if (EcuImport.IsZip(display))
                    ImportZip(resolver, uri, dir, cacheDir, st, sum, progress, ct);
                else if (EcuImport.AcceptedName(display) is { } name)
                {
                    using var src = resolver.OpenInputStream(uri) ?? throw new IOException("sin datos");
                    ImportFile(name, src, dir, st, sum, progress, ct);
                }
                else
                    sum.Skipped++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                sum.Fail(display, UiUtil.Describe(ex));
            }
        }
    }

    static string DisplayName(ContentResolver resolver, Android.Net.Uri uri)
    {
        using var c = resolver.Query(uri, [OpenableColumns.DisplayName], null, null, null);
        return c != null && c.MoveToFirst() ? c.GetString(0) ?? "" : "";
    }

    // El ZIP se copia a caché para poder abrirlo con acceso aleatorio sin cargarlo entero en memoria.
    static void ImportZip(ContentResolver resolver, Android.Net.Uri uri, string dir, string cacheDir,
        State st, ImportSummary sum, Action<string> progress, CancellationToken ct)
    {
        string tmpZip = Path.Combine(cacheDir, "import_" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var src = resolver.OpenInputStream(uri) ?? throw new IOException("sin datos"))
            {
                if (!CopyLimited(src, tmpZip, EcuImport.MaxTotalBytes, ct, out _))
                    throw new IOException("ZIP demasiado grande");
            }
            using var zip = new ZipArchive(File.OpenRead(tmpZip), ZipArchiveMode.Read);
            if (zip.Entries.Count > EcuImport.MaxEntries)
                throw new IOException("ZIP con demasiadas entradas");
            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (entry.Name.Length == 0)
                    continue;   // directorio
                // Solo el último segmento del nombre: la ruta de la entrada no se usa nunca para el destino.
                if (EcuImport.AcceptedName(entry.Name) is not { } name)
                {
                    sum.Skipped++;
                    continue;
                }
                if (entry.Length > EcuImport.MaxFileBytes)
                {
                    sum.Fail(name, "demasiado grande");
                    continue;
                }
                try
                {
                    using var src = entry.Open();
                    ImportFile(name, src, dir, st, sum, progress, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    sum.Fail(name, UiUtil.Describe(ex));
                }
            }
        }
        finally
        {
            try { File.Delete(tmpZip); } catch (IOException) { }
        }
    }

    // Copia a un temporal y lo mueve al destino: un fallo a medias no deja un .prg corrupto.
    static void ImportFile(string name, Stream src, string dir, State st, ImportSummary sum,
        Action<string> progress, CancellationToken ct)
    {
        if (EcuImport.CaseCollision(name, st.Names) is { } other)
        {
            sum.Fail(name, "ya existe como " + other);
            return;
        }
        progress(name);
        string tmp = Path.Combine(dir, name + ".imp");
        try
        {
            if (!CopyLimited(src, tmp, EcuImport.AllowedBytes(st.Total), ct, out long written))
            {
                sum.Fail(name, "demasiado grande");
                return;
            }
            File.Move(tmp, Path.Combine(dir, name), true);
            st.Total += written;
            st.Names.Add(name);
            sum.Imported++;
        }
        finally
        {
            try { File.Delete(tmp); } catch (IOException) { }
        }
    }

    // false si el origen supera el límite (el destino queda a medias: el llamador lo borra).
    static bool CopyLimited(Stream src, string dest, long limit, CancellationToken ct, out long written)
    {
        written = 0;
        var buf = new byte[81920];
        using var dst = File.Create(dest);
        int n;
        while ((n = src.Read(buf, 0, buf.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            written += n;
            if (written > limit)
                return false;
            dst.Write(buf, 0, n);
        }
        return true;
    }
}
