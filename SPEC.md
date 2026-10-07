# InpaDroid — especificación común

App Android (C#, .NET 10, `net10.0-android`) que imita a BMW INPA usando **ediabaslib** (GPLv3,
https://github.com/uholeschak/ediabaslib) como motor EDIABAS. No incluye ningún archivo de BMW:
el usuario copia sus `.prg`/`.grp` a una carpeta del móvil.

## Rutas
- Proyecto: `/home/ridao/Desktop/Pruevas antibraviti/InpaDroid/`
- App: `InpaDroid/src/InpaDroid/InpaDroid.csproj` (namespace raíz `InpaDroid`)
- ediabaslib: `InpaDroid/external/ediabaslib/` (ya copiado, sin `.git`)
- Datos BMW de referencia (NO van dentro de la APK, están en `.gitignore`):
  `InpaDroid/datos_bmw/st212/` = lo extraído de `St212.exe` (Standard Tools 2.12): `Ecu/` (32 `.prg` base),
  `Bin/` (`EDIABAS.ini`), `INPA/CFGDAT` y `INPA/SGDAT` (cabeceras y script de inicio `startus.ips`).
- Entorno: `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`, JDK en
  `/usr/lib/jvm/java-21-openjdk-amd64`, Android SDK en `$HOME/Android/Sdk`.

## Propiedad de archivos (cada agente toca SOLO lo suyo)
| Dueño | Archivos |
|---|---|
| Agente entorno | `InpaDroid.csproj`, `AndroidManifest.xml`, `Resources/` (salvo `Resources/layout/*` y `Resources/values/styles_inpa.xml`), `external/`, `.gitignore`, `build.sh` |
| Agente diagnóstico | `src/InpaDroid/Diag/*.cs` |
| Agente UI | `src/InpaDroid/Ui/*.cs`, `Resources/layout/*`, `Resources/values/styles_inpa.xml` |
| Agente README | `README.md` |

## Contrato de la capa de diagnóstico (`InpaDroid.Diag`)
```csharp
namespace InpaDroid.Diag;

public enum AdapterType { UsbKDcan, Enet, Elm327Bluetooth, Elm327Wifi }

public sealed class AdapterSettings
{
    public AdapterType Type { get; set; } = AdapterType.UsbKDcan;
    public string BluetoothAddress { get; set; } = "";        // MAC del ELM327
    public string EnetHost { get; set; } = "auto";            // IP o "auto"
    public string ElmWifiHost { get; set; } = "192.168.0.10:35000";
    public string EcuPath { get; set; } = "";                 // carpeta con .prg/.grp
    public static AdapterSettings Load(Android.Content.Context ctx);   // SharedPreferences
    public void Save(Android.Content.Context ctx);
}

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

public sealed class DiagService : IDisposable
{
    public DiagService(Android.Content.Context context, AdapterSettings settings);
    public IReadOnlyList<EcuFile> ScanEcuFolder();            // ordena .grp primero, luego .prg, alfabético
    // Las tareas se ejecutan en un hilo de fondo y se serializan (EdiabasNet no es thread-safe).
    public Task<JobResult> RunJobAsync(string sgbd, string job, string args = "", string results = "");
    public Task<IReadOnlyList<JobInfo>> GetJobsAsync(string sgbd);   // offline, desde el .prg (_JOBS etc.)
    public Task<string> ResolveVariantAsync(string sgbd);           // .grp -> nombre del .prg real
    public Task<(double? Ubatt, bool? Ignition)> ReadBatteryIgnitionAsync(); // UTILITY STATUS_UBATT / STATUS_ZUENDUNG
    public void Abort();
    public void Dispose();
}
```
Notas: el adaptador USB es FTDI (puerto `"FTDI0"`, `EdFtdiInterface.ConnectParameterType(UsbManager)`),
ENET usa `EdInterfaceEnet`, ELM327 BT usa puerto `"BLUETOOTH:<MAC>;ELM327"`, ELM327 WiFi usa `"ELM327WIFI"`.
Comprobar los detalles exactos en el código de ediabaslib/BmwDeepObd.

## UI estilo INPA (`InpaDroid.Ui`)
Aspecto: fondo gris claro, barra de título azul BMW (#1C69D4 o azul INPA), texto monoespaciado
para valores, **barra inferior de teclas F** (F1–F10 en dos filas o desplazable) más botón **Shift**
que cambia las etiquetas a Shift+F1…F10, igual que INPA. Cabecera en todas las pantallas con
indicadores **Batería** y **Encendido** (verde/gris) refrescados cada ~2 s mientras hay conexión.

Pantallas:
1. **MainActivity** (pantalla de inicio INPA): título "InpaDroid", F1 Info, F2 Selección de centralita,
   F3 Identificar vehículo (lee `FA`/`UTILITY` si existen), F9 Ajustes, F10 Salir.
2. **EcuSelectActivity**: lista de `EcuFile` con buscador; grupos (`.grp`) arriba con distintivo.
   Al tocar uno abre EcuActivity.
3. **EcuActivity** (pantalla de centralita, teclas como un script INPA estándar):
   F1 Info (job `INFO`), F2 Ident (`IDENT`), F4 Memoria de errores (`FS_LESEN`),
   Shift+F4 Borrar errores (`FS_LOESCHEN`, con confirmación), F5 Status (lista de jobs `STATUS_*`;
   al elegir uno se repite cada ~1 s hasta salir), F6 Steuern (jobs `STEUERN_*`, pide argumentos,
   aviso de seguridad y confirmación), F7 Jobs (todos los jobs estilo Tool32: elegir job, argumentos
   separados por `;`, ejecutar), F10 Volver. Resultados en lista: un bloque por set, `NOMBRE: valor`.
   En errores destacar `F_ORT_NR` y `F_ORT_TEXT`.
4. **SettingsActivity**: tipo de adaptador, MAC Bluetooth (lista de emparejados), host ENET,
   host ELM WiFi, carpeta ECU (texto + botón para elegir; por defecto
   `<almacenamiento externo de la app>/Ecu`).

Permisos/runtime: pedir BLUETOOTH_CONNECT/SCAN en Android 12+, USB host, INTERNET,
ACCESS_WIFI_STATE/NETWORK_STATE. Gestionar todo error mostrando el texto en pantalla, nunca cerrar la app.

---
# Fase 2: versión E39 (coche del usuario: E39 diésel, M47 520d o M57 525d/530d)

Datos del usuario: `InpaDroid/daten E39 v70/` (`ecu/` 700 `.prg` + 241 `.grp`, `sgdat/` 198 `.ipo`, sin `.ips`).
Se copian `ecu/`, `sgdat/` y `cfgdat/` a `datos_bmw/e39/` (en .gitignore; nunca dentro de la APK).
El E39 va por línea K (DS2/KWP2000): solo sirve el cable K+DCAN USB (FTDI) con el puente de los pines 7/8.

> Nota: esta sección y el contrato de abajo son históricos (fase 2). Hoy los catálogos son JSON genéricos en
> `src/InpaDroid/Ui/Chassis/<id>_catalog.json` y los tipos viven en `Ui/Chassis/ChassisModel.cs`; ver CONTRIBUTING.md.

## Propiedad de archivos (fase 2)
| Dueño | Archivos |
|---|---|
| Agente datos E39 | `src/InpaDroid/Ui/E39/E39Model.cs`, `src/InpaDroid/Ui/E39/E39Catalog.cs`, `tools/` (herramientas de escritorio), `datos_bmw/e39/` |
| Agente pantallas E39 | `src/InpaDroid/Ui/E39/E39MenuActivity.cs`, `src/InpaDroid/Ui/E39/E39EcuActivity.cs`, `Resources/layout/e39_*.xml`, y en `Ui/MainActivity.cs` SOLO añadir una tecla para abrir el menú E39 |
| Agente ajustes | `src/InpaDroid/Ui/SettingsActivity.cs` |
| Agente README | `README.md`, `PROXIMA_SESION.md` |

## Contrato del catálogo E39 (`InpaDroid.Ui.E39`, archivo `E39Model.cs`)
```csharp
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
```
`E39Catalog.cs`: `public static class E39Catalog { public static IReadOnlyList<E39Ecu> Ecus { get; } }`
Orden: motor diésel (DDE) primero, luego caja, ABS/ASC/DSC, airbag, IKE, LCM, GM/ZKE, EWS, climatización, etc.
Las pantallas usan DiagService.RunJobAsync; para cada página agrupan las llamadas por job (un job por llamada,
pidiendo los resultados necesarios) para no repetir jobs.
