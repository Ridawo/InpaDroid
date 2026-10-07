namespace InpaDroid.Ui.Chassis;

public sealed record E39Value(string Job, string Result, string Label, string Unit = "", string Args = "");

public sealed record E39Page(string Title, IReadOnlyList<E39Value> Values);

// Warning: texto de aviso mostrado antes de confirmar la activación
public sealed record E39Action(string Title, string Job, string Args, string Warning);

// Sgbd = nombre de .grp o .prg sin extensión (p.ej. "D_0012")
public sealed record E39Ecu(
    string Title, string Sgbd,
    IReadOnlyList<E39Page> StatusPages, IReadOnlyList<E39Action> Actions,
    string IdentJob = "IDENT", string FsReadJob = "FS_LESEN", string FsClearJob = "FS_LOESCHEN");

// Un chasis completo: metadatos del menú + centralitas. Se carga de <id>_catalog.json.
// Verified = false marca catálogos esqueleto cuyos SGBD/jobs no se han comprobado contra datos reales.
public sealed record ChassisInfo(
    string Id, string Name, string Subtitle, string InfoTitle, string Info, bool Verified,
    IReadOnlyList<E39Ecu> Ecus);
