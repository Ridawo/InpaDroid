# Contributing to InpaDroid

> **[English]** | [Español](#cómo-contribuir)

---

## Reporting a bug

Open an issue and include:

- What you were doing when it happened and which screen/ECU you were on.
- The exact error text shown by the app (copy it or screenshot it).
- Whether the Battery and Ignition indicators were green.
- Android version and adapter type (USB K+DCAN, ENET, ELM327).

If the problem is a crash, the Android logcat output (`adb logcat -d`) is the fastest way to diagnose it.

## Adding support for a new chassis

Chassis are data-driven: one JSON file plus one menu entry. The generic `ChassisMenuActivity` and `ChassisEcuActivity` (in `src/InpaDroid/Ui/Chassis/`) do the rest.

1. Create `src/InpaDroid/Ui/Chassis/<id>_catalog.json` (for example `e61_catalog.json`). It is embedded automatically by `InpaDroid.csproj`. Use `e46_catalog.json` as a template: an object with `id`, `name`, `subtitle`, `infoTitle`, `info` (may use `{iface}` and `{ecupath}`), `verified` and `ecus` (each with `title`, `sgbd`, `statusPages`, `actions`). Set `"verified": false` (chassis and/or ECU) for anything you have not checked, as in `e60_catalog.json` and `e90_catalog.json`. The app only shows the chassis-level flag ("sin verificar" in the menu); the per-ECU one is a note for reviewers.
2. Add the menu entry in `MainActivity.cs`: one `SetKey(n, "E61", () => ChassisMenuActivity.Start(this, "e61"))` line and its text in `BuildMenu`.
3. Optional: users can override any chassis without recompiling by putting `<id>_catalog.json` in their ECU folder (see [Catalog format](#catalog-format) for the field details).

Before adding a chassis, check that you have tested the ECU `.prg`/`.grp` files and confirmed which jobs and result names actually work for that car. The catalog should only contain values that have been verified against a real vehicle or a known-good EDIABAS trace.

## Catalog format

Chassis catalogs live in `src/InpaDroid/Ui/Chassis/<id>_catalog.json`. At runtime the app also tries to load `<id>_catalog.json` from the ECU folder (only for chassis that already have an embedded catalog); if found and valid, it replaces the embedded catalog. This means you can add or change ECUs without recompiling.

The reference JSON is `src/InpaDroid/Ui/Chassis/e39_catalog.json`. Its structure:

```json
{
  "id": "e39",
  "name": "BMW E39",
  "subtitle": "Shown under the title of the chassis menu",
  "infoTitle": "Title of the F1 Info dialog",
  "info": "F1 Info text; {iface} and {ecupath} are replaced with the current adapter and ECU folder",
  "verified": true,
  "ecus": [
    {
      "title": "Display name shown in the ECU list",
      "sgbd": "D_0012",
      "identJob": "IDENT",
      "fsReadJob": "FS_LESEN",
      "fsClearJob": "FS_LOESCHEN",
      "statusPages": [
        {
          "title": "Page title",
          "values": [
            {
              "job": "STATUS_MOTORDREHZAHL",
              "result": "STAT_MOTORDREHZAHL_WERT",
              "label": "Engine speed",
              "unit": "rpm",
              "args": ""
            }
          ]
        }
      ],
      "actions": [
        {
          "title": "Action label",
          "job": "STEUERN_E_LUEFTER",
          "args": "50",
          "warning": "Safety warning shown before the user confirms."
        }
      ]
    }
  ]
}
```

A file in the ECU folder may also be just the `ecus` array (`[ { "title": ..., "sgbd": ... } ]`): then only the ECU list changes and the rest of the embedded chassis data is kept. Comments and trailing commas are accepted.

`unit` and `args` can be omitted when empty. `identJob`, `fsReadJob`, and `fsClearJob` default to `IDENT`, `FS_LESEN`, and `FS_LOESCHEN` respectively and can be omitted if those defaults apply.

**Before submitting a PR with catalog changes**, verify each value and job name against the actual `.prg` using F7 (all jobs) or the `PrgProbe` tool in `tools/PrgProbe/`.

## Code conventions

- Language: C# 14 / .NET 10. Target framework `net10.0-android36.1`.
- All calls to `EdiabasNet` must go through `DiagService`. EdiabasNet is not thread-safe; `DiagService` serializes all calls through its internal worker queue. Do not call it from any other thread.
- Do not add comments that explain what the code does — use clear names instead. Only add a comment when the *why* is non-obvious: a hidden constraint, a workaround, or an invariant that would surprise a reader.
- Do not add error handling or fallbacks for scenarios that cannot happen. Validate at system boundaries (user input, file I/O, external APIs); trust internal code.
- Any new dependency must be GPL-compatible. ediabaslib is GPLv3 and that license propagates.

## Running the tests and the catalog check

- `dotnet test tests/InpaDroid.Tests` runs the unit tests (no Android device needed). They link the pure-logic sources (`DiagModels.cs`, `ChassisModel.cs`, `ChassisCatalog.cs`) and the embedded catalogs, so a broken catalog JSON fails here.
- `python3 tools/catalog_check.py` needs the PRG dumps from the proprietary BMW files (`datos_bmw/`), so it only runs locally; CI just checks that it compiles.
- CI (`.github/workflows/ci.yml`) runs the unit tests, the JSON check of `src/**/*.json` and an Android build on every push and pull request. Releases are described in [docs/RELEASING.md](docs/RELEASING.md).

## Submitting a pull request

1. Fork the repository and create a branch from `main`.
2. Keep PRs focused: one logical change per PR.
3. Test against a real car or, for UI changes, on a device/emulator with the actual APK installed.
4. The PR description should explain *why* the change is needed, not just what it does.

---

---

## Cómo contribuir

> [English](#contributing-to-inpadroid) | **[Español]**

---

## Reportar un error

Abre una incidencia (*issue*) e incluye:

- Qué estabas haciendo cuando ocurrió y en qué pantalla o centralita estabas.
- El texto exacto del error que muestra la app (cópialo o haz una captura).
- Si los indicadores Batería y Encendido estaban en verde.
- Versión de Android y tipo de adaptador (USB K+DCAN, ENET, ELM327).

Si el problema es un cierre inesperado de la app, el log de Android (`adb logcat -d`) es lo más útil para diagnosticarlo.

## Añadir soporte para un chasis nuevo

Los chasis se definen con datos: un archivo JSON y una entrada de menú. Las activities genéricas `ChassisMenuActivity` y `ChassisEcuActivity` (en `src/InpaDroid/Ui/Chassis/`) hacen el resto.

1. Crear `src/InpaDroid/Ui/Chassis/<id>_catalog.json` (por ejemplo `e61_catalog.json`). `InpaDroid.csproj` lo incrusta solo. Usa `e46_catalog.json` como plantilla: un objeto con `id`, `name`, `subtitle`, `infoTitle`, `info` (admite `{iface}` y `{ecupath}`), `verified` y `ecus` (cada una con `title`, `sgbd`, `statusPages`, `actions`). Pon `"verified": false` (en el chasis o en la centralita) en lo que no hayas comprobado, como en `e60_catalog.json` y `e90_catalog.json`. La app solo muestra la marca del chasis ("sin verificar" en el menú); la de cada centralita es una nota para quien revise.
2. Añadir la entrada en `MainActivity.cs`: una línea `SetKey(n, "E61", () => ChassisMenuActivity.Start(this, "e61"))` y su texto en `BuildMenu`.
3. Opcional: cualquier usuario puede sustituir un chasis sin recompilar copiando `<id>_catalog.json` a su carpeta ECU (los campos se explican en [Formato del catálogo](#formato-del-catálogo)).

Antes de añadir un chasis, comprueba que los archivos `.prg`/`.grp` funcionan y verifica qué jobs y nombres de resultado existen de verdad en ese coche. El catálogo solo debe incluir valores comprobados contra un vehículo real o una traza EDIABAS conocida.

## Formato del catálogo

Los catálogos de chasis están en `src/InpaDroid/Ui/Chassis/<id>_catalog.json`. En tiempo de ejecución la app también intenta cargar `<id>_catalog.json` desde la carpeta ECU (solo para chasis que ya tienen catálogo incrustado); si lo encuentra y es válido, reemplaza el incrustado. Así puedes añadir o cambiar centralitas sin recompilar.

El JSON de referencia con el catálogo completo está en `src/InpaDroid/Ui/Chassis/e39_catalog.json`. Su estructura:

```json
{
  "id": "e39",
  "name": "BMW E39",
  "subtitle": "Texto bajo el título del menú del chasis",
  "infoTitle": "Título del diálogo F1 Info",
  "info": "Texto de F1 Info; {iface} y {ecupath} se sustituyen por la interfaz y la carpeta ECU actuales",
  "verified": true,
  "ecus": [
    {
      "title": "Nombre que aparece en la lista de centralitas",
      "sgbd": "D_0012",
      "identJob": "IDENT",
      "fsReadJob": "FS_LESEN",
      "fsClearJob": "FS_LOESCHEN",
      "statusPages": [
        {
          "title": "Título de la página",
          "values": [
            {
              "job": "STATUS_MOTORDREHZAHL",
              "result": "STAT_MOTORDREHZAHL_WERT",
              "label": "Régimen motor",
              "unit": "1/min",
              "args": ""
            }
          ]
        }
      ],
      "actions": [
        {
          "title": "Nombre de la acción",
          "job": "STEUERN_E_LUEFTER",
          "args": "50",
          "warning": "Aviso de seguridad que aparece antes de que el usuario confirme."
        }
      ]
    }
  ]
}
```

Un archivo en la carpeta ECU también puede ser solo el array de `ecus` (`[ { "title": ..., "sgbd": ... } ]`): entonces solo cambia la lista de centralitas y el resto de datos del chasis incrustado se mantiene. Se aceptan comentarios y comas finales.

`unit` y `args` se pueden omitir cuando están vacíos. `identJob`, `fsReadJob` y `fsClearJob` tienen por defecto `IDENT`, `FS_LESEN` y `FS_LOESCHEN`, y se pueden omitir si esos valores aplican.

**Antes de enviar un PR con cambios en el catálogo**, verifica cada valor y nombre de job contra el `.prg` real usando F7 (todos los jobs) o la herramienta `PrgProbe` en `tools/PrgProbe/`.

## Convenciones de código

- Lenguaje: C# 14 / .NET 10. Target framework `net10.0-android36.1`.
- Todas las llamadas a `EdiabasNet` van a través de `DiagService`. EdiabasNet no es thread-safe; `DiagService` serializa todas las llamadas a través de su cola de trabajo interna. No lo llames desde ningún otro hilo.
- No añadas comentarios que expliquen qué hace el código: usa nombres claros. Añade un comentario solo cuando el *porqué* no sea obvio: una restricción oculta, un parche, o una invariante que sorprendería al lector.
- No añadas manejo de errores ni alternativas para situaciones que no pueden ocurrir. Valida en los límites del sistema (entrada del usuario, E/S de archivos, APIs externas); confía en el código interno.
- Cualquier dependencia nueva debe ser compatible con la GPL. ediabaslib tiene licencia GPLv3 y esa licencia se propaga.

## Pruebas y comprobación del catálogo

- `dotnet test tests/InpaDroid.Tests` pasa las pruebas unitarias (no hace falta un dispositivo Android). Enlazan el código sin dependencias de Android (`DiagModels.cs`, `ChassisModel.cs`, `ChassisCatalog.cs`) y los catálogos incrustados, así que un JSON de catálogo roto falla aquí.
- `python3 tools/catalog_check.py` necesita los volcados de los PRG sacados de los archivos propietarios de BMW (`datos_bmw/`), por eso solo se ejecuta en local; la CI solo comprueba que compila.
- La CI (`.github/workflows/ci.yml`) pasa las pruebas unitarias, valida los `src/**/*.json` y compila la app en cada push y pull request. Las releases se explican en [docs/RELEASING.md](docs/RELEASING.md).

## Enviar un pull request

1. Haz un fork del repositorio y crea una rama desde `main`.
2. Mantén los PRs centrados: un cambio lógico por PR.
3. Prueba contra un coche real o, para cambios de UI, en un dispositivo o emulador con la APK instalada.
4. La descripción del PR debe explicar *por qué* es necesario el cambio, no solo qué hace.
