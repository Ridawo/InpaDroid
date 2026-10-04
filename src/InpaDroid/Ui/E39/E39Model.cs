namespace InpaDroid.Ui.E39;

// Un valor de una página de status: job a ejecutar y resultado a mostrar.
public sealed record E39Value(string Job, string Result, string Label, string Unit = "", string Args = "");

// Página de status (como una pantalla STATUS de INPA): se refresca en bucle.
public sealed record E39Page(string Title, IReadOnlyList<E39Value> Values);

// Activación (Steuern). Warning es el texto de aviso que se muestra antes de confirmar.
public sealed record E39Action(string Title, string Job, string Args, string Warning);

// Una centralita del menú E39. Sgbd = nombre de .grp o .prg sin extensión (p.ej. "D_0012").
public sealed record E39Ecu(
    string Title, string Sgbd,
    IReadOnlyList<E39Page> StatusPages, IReadOnlyList<E39Action> Actions,
    string IdentJob = "IDENT", string FsReadJob = "FS_LESEN", string FsClearJob = "FS_LOESCHEN");
