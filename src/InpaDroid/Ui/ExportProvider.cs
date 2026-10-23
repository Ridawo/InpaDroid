using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;

namespace InpaDroid.Ui;

/// <summary>
/// Sirve de solo lectura los CSV e informes HTML de cache/exports a la app que elija el usuario en la hoja de compartir.
/// No exportado: el acceso lo da el permiso temporal del Intent (FLAG_GRANT_READ_URI_PERMISSION).
/// </summary>
[ContentProvider([Authority], Exported = false, GrantUriPermissions = true)]
public class ExportProvider : ContentProvider
{
    public const string Authority = "com.inpadroid.app.exports";
    const string Folder = "exports";

    public static Java.IO.File Dir(Context ctx)
    {
        var dir = new Java.IO.File(ctx.CacheDir, Folder);
        dir.Mkdirs();
        return dir;
    }

    public static Android.Net.Uri UriFor(string fileName) =>
        Android.Net.Uri.Parse($"content://{Authority}/{fileName}")!;

    public override bool OnCreate() => true;

    // Solo nombres planos "xxx.csv" o "inpadroid-report-*.html": sin separadores de ruta ni ".." (el URI viene de otra app).
    static bool IsValidName(string? name) =>
        !string.IsNullOrEmpty(name)
        && (name.EndsWith(".csv", StringComparison.Ordinal)
            || name.StartsWith("inpadroid-report-", StringComparison.Ordinal) && name.EndsWith(".html", StringComparison.Ordinal))
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_') && !name.Contains("..");

    Java.IO.File? Resolve(Android.Net.Uri uri)
    {
        var name = uri.LastPathSegment;
        if (!IsValidName(name) || Context == null)
            return null;
        var file = new Java.IO.File(Dir(Context), name!);
        return file.Exists() ? file : null;
    }

    public override ParcelFileDescriptor? OpenFile(Android.Net.Uri uri, string mode)
    {
        if (mode != "r")
            throw new Java.Lang.SecurityException("Solo lectura");
        var file = Resolve(uri) ?? throw new Java.IO.FileNotFoundException(uri.ToString());
        return ParcelFileDescriptor.Open(file, ParcelFileMode.ReadOnly);
    }

    public override ICursor? Query(Android.Net.Uri uri, string[]? projection, string? selection, string[]? selectionArgs,
        string? sortOrder)
    {
        var file = Resolve(uri);
        if (file == null)
            return null;
        var cursor = new MatrixCursor([IOpenableColumns.DisplayName, IOpenableColumns.Size]);
        cursor.AddRow([new Java.Lang.String(file.Name ?? ""), file.Length()]);
        return cursor;
    }

    public override string? GetType(Android.Net.Uri uri) =>
        uri.LastPathSegment?.EndsWith(".html", StringComparison.Ordinal) == true ? "text/html" : "text/csv";

    public override Android.Net.Uri? Insert(Android.Net.Uri uri, ContentValues? values) =>
        throw new Java.Lang.UnsupportedOperationException();

    public override int Delete(Android.Net.Uri uri, string? selection, string[]? selectionArgs) =>
        throw new Java.Lang.UnsupportedOperationException();

    public override int Update(Android.Net.Uri uri, ContentValues? values, string? selection, string[]? selectionArgs) =>
        throw new Java.Lang.UnsupportedOperationException();
}
