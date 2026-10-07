# Verified E39 ECUs

ECUs in `src/InpaDroid/Ui/Chassis/e39_catalog.json` and whether each one has been checked against a real car. A row is only marked once someone has actually tested it; until then it stays `pending`.

| ECU | SGBD | Useful jobs | Verified in real car | Contributor |
|---|---|---|---|---|
| Motor diésel (DDE) | D_0012 | IDENT, FS_LESEN, FS_LOESCHEN, STATUS_MOTORDREHZAHL, STATUS_LADEDRUCK, STEUERN_E_LUEFTER | pending | |
| Cambio automático (EGS) | D_0032 | IDENT, FS_LESEN, FS_LOESCHEN, STATUS_IO_LESEN | pending | |
| ABS / ASC / DSC | D_0056 | IDENT, FS_LESEN, FS_LOESCHEN, STATUS_IO_LESEN, STATUS_IO_LESEN_GESCHW | pending | |
| Airbag (MRS) | D_00A4 | IDENT, FS_LESEN, FS_LOESCHEN, STATUS_LESEN | pending | |
| Cuadro de instrumentos (IKE / KOMBI) | D_0080 | IDENT, FS_LESEN, FS_LOESCHEN, STATUS_ANALOG_LESEN, STATUS_TANKINHALT_LESEN, STEUERN_SELBSTTEST | pending | |
| Módulo de luces (LCM) | D_00D0 | IDENT, FS_LESEN, FS_LOESCHEN, STATUS_LESEN, STATUS_VORGEBEN | pending | |
| Módulo general (GM / cierre) | D_ZKE_GM | IDENT, FS_LESEN, FS_LOESCHEN, STATUS_DIGITAL_GM3_INT, STATUS_ANALOG_GM3, STEUERN_DIGITAL_GM3 | pending | |
| Inmovilizador (EWS) | D_0044 | IDENT, FS_LESEN, FS_LOESCHEN, STATUS_LESEN | pending | |
| Climatización (IHKA) | D_005B | IDENT, FS_LESEN, FS_LOESCHEN, STATUS_REGLERGROESSEN, STATUS_ANALOGEINGAENGE, STEUERN_GEBLAESE | pending | |

## How to add a verification

1. Connect the app to your E39 and open the ECU from the E39 menu.
2. Check that identification (IDENT) answers and that the fault memory can be read. Then check the status pages and, if you dare, the actions (STEUERN_*), noting which ones work and which fail or show wrong values.
3. Open a pull request editing this file: replace `pending` with a short result (for example `IDENT, FS_LESEN, STATUS OK; STEUERN_GEBLAESE not tested`), and add the car (model, year, engine) and the exact SGBD variant reported by IDENT. Put your GitHub handle in the Contributor column.
4. If something did not work, open an issue with the job name and the error, and write `partial` in the table.

Only report what you saw on a real car. Do not attach BMW proprietary files (`.prg`, `.grp`, INPA or EDIABAS data).
