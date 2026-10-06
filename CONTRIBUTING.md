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

The cleanest way to add a chassis is the same pattern used for the E39:

1. Create a menu activity that extends `InpaActivity` (see `E39MenuActivity.cs` as a model).
2. Create an ECU activity with its own status pages and Steuern actions (see `E39EcuActivity.cs`).
3. Define the catalog — either hardcoded in C# or in a JSON file (see [Extending the E39 catalog](#extending-the-e39-catalog) below).
4. Hook the new menu into `MainActivity` with a function key.

Before adding a chassis, check that you have tested the ECU `.prg`/`.grp` files and confirmed which jobs and result names actually work for that car. The catalog should only contain values that have been verified against a real vehicle or a known-good EDIABAS trace.

## Extending the E39 catalog

The E39 catalog lives in `src/InpaDroid/Ui/E39/E39Catalog.cs`. At runtime the app also tries to load `e39_catalog.json` from the ECU folder; if found, it replaces the built-in catalog. This means you can add or change ECUs without recompiling.

The reference JSON with the full built-in catalog is at `src/InpaDroid/Ui/E39/e39_catalog.json`. Its structure:

```json
[
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
```

`unit` and `args` can be omitted when empty. `identJob`, `fsReadJob`, and `fsClearJob` default to `IDENT`, `FS_LESEN`, and `FS_LOESCHEN` respectively and can be omitted if those defaults apply.

**Before submitting a PR with catalog changes**, verify each value and job name against the actual `.prg` using F7 (all jobs) or the `PrgProbe` tool in `tools/PrgProbe/`.

## Code conventions

- Language: C# 13 / .NET 10. Target framework `net10.0-android36.1`.
- All calls to `EdiabasNet` must go through `DiagService`. EdiabasNet is not thread-safe; `DiagService` serializes all calls through its internal worker queue. Do not call it from any other thread.
- Do not add comments that explain what the code does — use clear names instead. Only add a comment when the *why* is non-obvious: a hidden constraint, a workaround, or an invariant that would surprise a reader.
- Do not add error handling or fallbacks for scenarios that cannot happen. Validate at system boundaries (user input, file I/O, external APIs); trust internal code.
- Any new dependency must be GPL-compatible. ediabaslib is GPLv3 and that license propagates.

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

El camino más limpio es seguir el mismo patrón que el E39:

1. Crear una activity de menú que extienda `InpaActivity` (ver `E39MenuActivity.cs` como modelo).
2. Crear una activity de centralita con sus páginas de status y acciones Steuern (ver `E39EcuActivity.cs`).
3. Definir el catálogo, en C# o en JSON (ver [Ampliar el catálogo E39](#ampliar-el-catálogo-e39) más abajo).
4. Conectar el nuevo menú en `MainActivity` con una tecla F.

Antes de añadir un chasis, comprueba que los archivos `.prg`/`.grp` funcionan y verifica qué jobs y nombres de resultado existen de verdad en ese coche. El catálogo solo debe incluir valores comprobados contra un vehículo real o una traza EDIABAS conocida.

## Ampliar el catálogo E39

El catálogo E39 está en `src/InpaDroid/Ui/E39/E39Catalog.cs`. En tiempo de ejecución la app también intenta cargar `e39_catalog.json` desde la carpeta ECU; si lo encuentra, reemplaza el catálogo incorporado. Así puedes añadir o cambiar centralitas sin recompilar.

El JSON de referencia con el catálogo completo está en `src/InpaDroid/Ui/E39/e39_catalog.json`. Su estructura:

```json
[
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
```

`unit` y `args` se pueden omitir cuando están vacíos. `identJob`, `fsReadJob` y `fsClearJob` tienen por defecto `IDENT`, `FS_LESEN` y `FS_LOESCHEN`, y se pueden omitir si esos valores aplican.

**Antes de enviar un PR con cambios en el catálogo**, verifica cada valor y nombre de job contra el `.prg` real usando F7 (todos los jobs) o la herramienta `PrgProbe` en `tools/PrgProbe/`.

## Convenciones de código

- Lenguaje: C# 13 / .NET 10. Target framework `net10.0-android36.1`.
- Todas las llamadas a `EdiabasNet` van a través de `DiagService`. EdiabasNet no es thread-safe; `DiagService` serializa todas las llamadas a través de su cola de trabajo interna. No lo llames desde ningún otro hilo.
- No añadas comentarios que expliquen qué hace el código: usa nombres claros. Añade un comentario solo cuando el *porqué* no sea obvio: una restricción oculta, un parche, o una invariante que sorprendería al lector.
- No añadas manejo de errores ni alternativas para situaciones que no pueden ocurrir. Valida en los límites del sistema (entrada del usuario, E/S de archivos, APIs externas); confía en el código interno.
- Cualquier dependencia nueva debe ser compatible con la GPL. ediabaslib tiene licencia GPLv3 y esa licencia se propaga.

## Enviar un pull request

1. Haz un fork del repositorio y crea una rama desde `main`.
2. Mantén los PRs centrados: un cambio lógico por PR.
3. Prueba contra un coche real o, para cambios de UI, en un dispositivo o emulador con la APK instalada.
4. La descripción del PR debe explicar *por qué* es necesario el cambio, no solo qué hace.
