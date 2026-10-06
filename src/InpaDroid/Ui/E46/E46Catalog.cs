using InpaDroid.Ui.E39;

namespace InpaDroid.Ui.E46;

// Catálogo de centralitas del E46 (Serie 3, 1998-2006). Protocolo: línea K (DS2/KWP2000).
// Los E46 de 2006 con módulo D-CAN montado de fábrica quedan fuera del alcance inicial.
//
// Sgbd es siempre el grupo (.grp): la app resuelve la variante con IDENTIFIKATION.
// Los nombres de job se han verificado contra daten E46 v70 (mismos grupos que E39 salvo EGS6).
public static class E46Catalog
{
    // ---------------------------------------------------------------- Motor diésel (DDE), grupo D_0012
    // Comparte grupo con E39: las variantes M47 y M57TU son distintas pero los jobs STATUS_* coinciden.
    private static readonly E39Ecu Dde = new(
        "Motor diésel (DDE)", "D_0012",
        [
            new E39Page("Valores básicos",
            [
                new E39Value("STATUS_MOTORDREHZAHL",   "STAT_MOTORDREHZAHL_WERT",   "Régimen motor",           "1/min"),
                new E39Value("STATUS_MOTORTEMPERATUR", "STAT_MOTORTEMPERATUR_WERT", "Temperatura refrigerante","°C"),
                new E39Value("STATUS_UBATT",           "STAT_UBATT_WERT",           "Tensión batería (DDE)",   "V"),
                new E39Value("STATUS_LMM_MASSE",       "STAT_LMM_MASSE_WERT",       "Caudalímetro (HFM)",      "%"),
                new E39Value("STATUS_LADEDRUCK",       "STAT_LADEDRUCK_WERT",       "Presión turbo (absoluta)","mbar"),
            ]),
        ],
        []);

    // ---------------------------------------------------------------- Motor gasolina (MS43/MSS54), grupo D_MOTOR
    // MS43 = 318i/320i/325i/330i (2001+). MSS54 = M3 (330 kW).
    // Ambos usan jobs STATUS_* idénticos para los parámetros básicos.
    private static readonly E39Ecu Motor = new(
        "Motor gasolina (MS43/MSS54)", "D_MOTOR",
        [
            new E39Page("Valores básicos",
            [
                new E39Value("STATUS_MOTORDREHZAHL",   "STAT_MOTORDREHZAHL_WERT",   "Régimen motor",           "1/min"),
                new E39Value("STATUS_MOTORTEMPERATUR", "STAT_MOTORTEMPERATUR_WERT", "Temperatura refrigerante","°C"),
                new E39Value("STATUS_UBATT",           "STAT_UBATT_WERT",           "Tensión batería (DME)",   "V"),
                new E39Value("STATUS_LAMBDAREGELUNG",  "STAT_LAMBDAREGELUNG_WERT",  "Control lambda",          ""),
                new E39Value("STATUS_LADEDRUCK",       "STAT_LADEDRUCK_WERT",       "Presión turbo (abs.)",    "mbar"),
            ]),
        ],
        []);

    // ---------------------------------------------------------------- Caja de cambios automática (EGS), grupo D_EGS
    // EGS5 en el E46 (misma familia que E39). EGS6 (SMG II del M3) usa D_EGS6 — no incluido aquí.
    private static readonly E39Ecu Egs = new(
        "Cambio automático (EGS)", "D_EGS",
        [
            new E39Page("Valores básicos",
            [
                new E39Value("STATUS_GETRIEBE",   "STAT_GETRIEBE_STUFE_WERT",    "Marcha actual",            ""),
                new E39Value("STATUS_GETRIEBE",   "STAT_FAHRSTUFE_WERT",         "Selector de marcha",       ""),
                new E39Value("STATUS_TEMPERATUR", "STAT_GETRIEBETEMPERATUR_WERT","Temperatura aceite caja",  "°C"),
            ]),
        ],
        []);

    // ---------------------------------------------------------------- ABS/DSC III, grupo D_ABSI
    private static readonly E39Ecu Abs = new(
        "ABS / DSC III", "D_ABSI",
        [
            new E39Page("Velocidades de rueda",
            [
                new E39Value("STATUS_RADDREHZAHL", "STAT_RADDREHZAHL_VL_WERT", "Rueda delantera izq.", "km/h"),
                new E39Value("STATUS_RADDREHZAHL", "STAT_RADDREHZAHL_VR_WERT", "Rueda delantera der.", "km/h"),
                new E39Value("STATUS_RADDREHZAHL", "STAT_RADDREHZAHL_HL_WERT", "Rueda trasera izq.",   "km/h"),
                new E39Value("STATUS_RADDREHZAHL", "STAT_RADDREHZAHL_HR_WERT", "Rueda trasera der.",   "km/h"),
            ]),
        ],
        []);

    // ---------------------------------------------------------------- Airbag (MRS), grupo D_MRS
    private static readonly E39Ecu Airbag = new(
        "Airbag (MRS)", "D_MRS",
        [],
        []);

    // ---------------------------------------------------------------- Cuadro de instrumentos (IKE), grupo D_KOMBI
    private static readonly E39Ecu Ike = new(
        "Cuadro de instrumentos (IKE)", "D_KOMBI",
        [
            new E39Page("Valores básicos",
            [
                new E39Value("STATUS_UBATT",    "STAT_UBATT_WERT",    "Tensión de red",     "V"),
                new E39Value("STATUS_TANKSTAND","STAT_TANKSTAND_WERT","Nivel depósito",     "l"),
                new E39Value("STATUS_KMH",      "STAT_KMH_WERT",      "Velocidad (IKE)",    "km/h"),
            ]),
        ],
        []);

    // ---------------------------------------------------------------- Control carrocería (GM/ZKE), grupo D_ZKE5
    private static readonly E39Ecu Gm = new(
        "Carrocería (GM/ZKE)", "D_ZKE5",
        [],
        []);

    private static IReadOnlyList<E39Ecu> _ecus = [Dde, Motor, Egs, Abs, Airbag, Ike, Gm];
    public static IReadOnlyList<E39Ecu> Ecus => _ecus;

    // Permite cargar un catálogo JSON externo (mismo esquema que e39_catalog.json).
    // Si el archivo no existe o falla la carga, mantiene el catálogo compilado.
    public static void TryLoadFromFolder(string ecuPath)
    {
        // TODO Fase 3: implementar deserialización de e46_catalog.json cuando el catálogo sea estable.
        _ = ecuPath;
    }
}
