namespace InpaDroid.Ui.E39;

public sealed record E39Value(string Job, string Result, string Label, string Unit = "", string Args = "");

public sealed record E39Page(string Title, IReadOnlyList<E39Value> Values);

// Warning: texto de aviso mostrado antes de confirmar la activación
public sealed record E39Action(string Title, string Job, string Args, string Warning);

// Sgbd = nombre de .grp o .prg sin extensión (p.ej. "D_0012")
public sealed record E39Ecu(
    string Title, string Sgbd,
    IReadOnlyList<E39Page> StatusPages, IReadOnlyList<E39Action> Actions,
    string IdentJob = "IDENT", string FsReadJob = "FS_LESEN", string FsClearJob = "FS_LOESCHEN");
