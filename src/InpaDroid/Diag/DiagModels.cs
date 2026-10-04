namespace InpaDroid.Diag;

public sealed class EcuFile                    // un .prg o .grp de la carpeta
{
    public string Name { get; init; } = "";    // sin extensión, p.ej. "D_MOTOR"
    public string FullPath { get; init; } = "";
    public bool IsGroup { get; init; }         // true para .grp
}

public sealed class JobInfo
{
    public string Name { get; init; } = "";
    public string Comment { get; init; } = "";
    public IReadOnlyList<string> Arguments { get; init; } = [];   // nombres de argumentos
    public IReadOnlyList<string> Results { get; init; } = [];
}

public sealed class JobResult
{
    public bool Ok { get; init; }
    public string Error { get; init; } = "";
    // Un diccionario por set de resultados (sin el set 0 de sistema). Valor ya formateado como texto.
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Sets { get; init; } = [];
    public string JobStatus { get; init; } = "";   // JOB_STATUS del último set si existe
}
