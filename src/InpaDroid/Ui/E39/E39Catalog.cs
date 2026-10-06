using System.Text.Json;

namespace InpaDroid.Ui.E39;

// Catálogo de centralitas del E39 diésel (520d M47, 525d/530d M57), solo datos.
//
// Origen de los nombres: volcado offline de los .prg/.grp de "daten E39 v70" con tools/PrgProbe
// (jobs internos _JOBS/_ARGUMENTS/_RESULTS/_TABLE de EdiabasLib). Unidades: tablas BETRIEBSWTAB de las DDE
// y literales *_EINH de cada job (tools/prg_units). Detalle en datos_bmw/e39/analisis/RESUMEN.md.
// Los .ipo de esa carpeta son scripts de codificación (CABI) y de programación, no pantallas de status de
// INPA, así que las páginas siguen la agrupación de los propios jobs STATUS_* de cada SGBD.
//
// Sgbd es siempre el grupo (.grp): la app resuelve la variante con IDENTIFIKATION. Cada valor se ha
// comprobado contra las variantes E39 de su grupo (tools/catalog_check.py); cuando una página solo vale para
// algunas variantes, el título lo dice.
public static class E39Catalog
{
    private const string AvisoMotorParado =
        "Prueba de actuador. Coche parado, freno de mano puesto, cambio en P/punto muerto y contacto puesto " +
        "(motor apagado salvo que se indique). Aléjate de correas y ventilador. Al acabar pulsa " +
        "\"Terminar activaciones\" o quita el contacto para devolver el control a la centralita.";

    // ---------------------------------------------------------------- Motor diésel (DDE), grupo D_0012
    // Variantes E39 diésel del grupo D_0012:
    //   DDE30DS0 = DDE 3.0 para M47 (520d, bomba rotativa VP44)
    //   DDE40KW0 = DDE 4.0 para M57 (525d/530d, common rail)
    //   D40M57A1 = DDE 4.0 para M57 E39, SGBD nueva (índice de diagnosis 5370); comparte las páginas
    //              "M47 y M57" pero no las de rail (allí se llaman STATUS_RAILDRUCK_IST/_SOLL, etc.).
    // (DDE22DS0 es el M51 del 525tds, no aplica.)
    private static readonly E39Ecu Dde = new(
        "Motor diésel (DDE)", "D_0012",
        [
            new E39Page("Valores básicos (M47 y M57)",
            [
                new E39Value("STATUS_MOTORDREHZAHL", "STAT_MOTORDREHZAHL_WERT", "Régimen motor", "1/min"),
                new E39Value("STATUS_MOTORTEMPERATUR", "STAT_MOTORTEMPERATUR_WERT", "Temperatura refrigerante", "°C"),
                new E39Value("STATUS_UBATT", "STAT_UBATT_WERT", "Tensión batería (DDE)", "V"),
                new E39Value("STATUS_LMM_MASSE", "STAT_LMM_MASSE_WERT", "Caudalímetro (HFM)", "%"),
                new E39Value("STATUS_PWG_POTI_SPANNUNG", "STAT_PWG_POTI_SPANNUNG_WERT", "Pedal acelerador, tensión potenciómetro 1", "V"),
            ]),
            new E39Page("Turbo y pedal (DDE 3.0 y DDE 4.0)",
            [
                // STATUS_LADEDRUCK y STATUS_PWG_FAHRERWUNSCH existen en DDE30DS0 y DDE40KW0 (no en D40M57A1).
                new E39Value("STATUS_LADEDRUCK", "STAT_LADEDRUCK_WERT", "Presión de turbo (absoluta)", "mbar"),
                new E39Value("STATUS_PWG_FAHRERWUNSCH", "STAT_PWG_FAHRERWUNSCH_WERT", "Pedal acelerador", "%"),
                new E39Value("STATUS_LMM_MASSE", "STAT_LMM_MASSE_WERT", "Caudalímetro (HFM)", "%"),
                new E39Value("STATUS_MOTORDREHZAHL", "STAT_MOTORDREHZAHL_WERT", "Régimen motor", "1/min"),
            ]),
            new E39Page("Entradas digitales (M47 y M57)",
            [
                new E39Value("STATUS_DIGITAL", "STAT_BLS_EIN", "Interruptor luz de freno", "1=pisado"),
                new E39Value("STATUS_DIGITAL", "STAT_KUP_EIN", "Interruptor embrague", "1=pisado"),
                new E39Value("STATUS_DIGITAL", "STAT_AC_EIN", "Petición A/C", "1=sí"),
                new E39Value("STATUS_DIGITAL", "STAT_KO_EIN", "Petición compresor A/C", "1=sí"),
            ]),
            new E39Page("M57 525d/530d (DDE 4.0): rail e inyección",
            [
                new E39Value("STATUS_RAILDRUCK", "STAT_RAILDRUCK_WERT", "Presión rail real", "bar"),
                new E39Value("STATUS_ZUMPQSOLL", "STAT_ZUMPQSOLL_WERT", "Presión rail consigna", "bar"),
                new E39Value("STATUS_VORFOERDERDRUCK", "STAT_VORFOERDERDRUCK_WERT", "Presión de prealimentación", "bar"),
                new E39Value("STATUS_MRMM_EAKT", "STAT_MRMM_EAKT_WERT", "Cantidad inyectada", "mm³/carrera"),
                new E39Value("STATUS_MOTORDREHZAHL", "STAT_MOTORDREHZAHL_WERT", "Régimen motor", "1/min"),
            ]),
            new E39Page("M57 525d/530d (DDE 4.0): turbo, aire y EGR",
            [
                new E39Value("STATUS_LADEDRUCK", "STAT_LADEDRUCK_WERT", "Presión de turbo real", "mbar"),
                new E39Value("STATUS_LDMP_LSOLL", "STAT_LDMP_LSOLL_WERT", "Presión de turbo consigna", "mbar"),
                new E39Value("STATUS_ANMADF", "STAT_ANMADF_WERT", "Presión atmosférica", "mbar"),
                new E39Value("STATUS_ARMM_LIST", "STAT_ARMM_LIST_WERT", "Masa de aire real", "mg/carrera"),
                new E39Value("STATUS_ARMM_LSOLL", "STAT_ARMM_LSOLL_WERT", "Masa de aire consigna (EGR)", "mg/carrera"),
                new E39Value("STATUS_AN_LUFTTEMPERATUR", "STAT_AN_LUFTTEMPERATUR_WERT", "Temperatura aire admisión", "°C"),
            ]),
            new E39Page("M57 525d/530d (DDE 4.0): corrección de caudal por cilindro",
            [
                // Al ralentí y motor caliente. Valores grandes en un cilindro apuntan a ese inyector.
                new E39Value("STATUS_LAUFUNRUHE_LLR_MENGE", "STAT_LAUFUNRUHE_LLR_MENGE_ZYL1_WERT", "Cilindro 1", "mm³"),
                new E39Value("STATUS_LAUFUNRUHE_LLR_MENGE", "STAT_LAUFUNRUHE_LLR_MENGE_ZYL2_WERT", "Cilindro 2", "mm³"),
                new E39Value("STATUS_LAUFUNRUHE_LLR_MENGE", "STAT_LAUFUNRUHE_LLR_MENGE_ZYL3_WERT", "Cilindro 3", "mm³"),
                new E39Value("STATUS_LAUFUNRUHE_LLR_MENGE", "STAT_LAUFUNRUHE_LLR_MENGE_ZYL4_WERT", "Cilindro 4", "mm³"),
                new E39Value("STATUS_LAUFUNRUHE_LLR_MENGE", "STAT_LAUFUNRUHE_LLR_MENGE_ZYL5_WERT", "Cilindro 5", "mm³"),
                new E39Value("STATUS_LAUFUNRUHE_LLR_MENGE", "STAT_LAUFUNRUHE_LLR_MENGE_ZYL6_WERT", "Cilindro 6", "mm³"),
            ]),
            new E39Page("M57 525d/530d (DDE 4.0): régimen por cilindro",
            [
                new E39Value("STATUS_LAUFUNRUHE_DREHZAHL", "STAT_LAUFUNRUHE_DREHZAHL_ZYL1_WERT", "Cilindro 1", "1/min"),
                new E39Value("STATUS_LAUFUNRUHE_DREHZAHL", "STAT_LAUFUNRUHE_DREHZAHL_ZYL2_WERT", "Cilindro 2", "1/min"),
                new E39Value("STATUS_LAUFUNRUHE_DREHZAHL", "STAT_LAUFUNRUHE_DREHZAHL_ZYL3_WERT", "Cilindro 3", "1/min"),
                new E39Value("STATUS_LAUFUNRUHE_DREHZAHL", "STAT_LAUFUNRUHE_DREHZAHL_ZYL4_WERT", "Cilindro 4", "1/min"),
                new E39Value("STATUS_LAUFUNRUHE_DREHZAHL", "STAT_LAUFUNRUHE_DREHZAHL_ZYL5_WERT", "Cilindro 5", "1/min"),
                new E39Value("STATUS_LAUFUNRUHE_DREHZAHL", "STAT_LAUFUNRUHE_DREHZAHL_ZYL6_WERT", "Cilindro 6", "1/min"),
            ]),
            new E39Page("M47 520d (DDE 3.0): bomba VP44 e inyección",
            [
                new E39Value("STATUS_EINSPRITZMENGE", "STAT_EINSPRITZMENGE_WERT", "Cantidad inyectada", "mg/carrera"),
                new E39Value("STATUS_SB_SOLLWERT", "STAT_SB_SOLLWERT_WERT", "Inicio de inyección consigna", "°KW"),
                new E39Value("STATUS_SB_ISTWERT", "STAT_SB_ISTWERT_WERT", "Inicio de inyección real", "°KW"),
                new E39Value("STATUS_TEMPERATUR_PUMPE", "STAT_TEMPERATUR_PUMPE_WERT", "Temperatura bomba", "°C"),
                new E39Value("STATUS_MOTORDREHZAHL_SOLL", "STAT_MOTORDREHZAHL_SOLL_WERT", "Ralentí consigna", "1/min"),
                new E39Value("STATUS_MOTORDREHZAHL", "STAT_MOTORDREHZAHL_WERT", "Régimen motor", "1/min"),
            ]),
            new E39Page("M47 520d (DDE 3.0): turbo, aire y EGR",
            [
                new E39Value("STATUS_LADEDRUCK", "STAT_LADEDRUCK_WERT", "Presión de turbo real", "hPa"),
                new E39Value("STATUS_LADEDRUCK_SOLLWERT", "STAT_LADEDRUCK_SOLLWERT_WERT", "Presión de turbo consigna", "hPa"),
                new E39Value("STATUS_ATMOSPHAERENDRUCK", "STAT_ATMOSPHAERENDRUCK_WERT", "Presión atmosférica", "hPa"),
                new E39Value("STATUS_LAST", "STAT_LAST_WERT", "Masa de aire real", "mg/carrera"),
                new E39Value("STATUS_ARF_SOLLWERT", "STAT_ARF_SOLLWERT_WERT", "Masa de aire consigna (EGR)", "mg/carrera"),
                new E39Value("STATUS_GESCHWINDIGKEIT", "STAT_GESCHWINDIGKEIT_WERT", "Velocidad", "km/h"),
            ]),
        ],
        [
            // Mismo job y mismo argumento TASTVERHAELTNIS en DDE30DS0 y DDE40KW0 (D40M57A1 usa STEUERN_SELECTIV).
            new E39Action("Electroventilador al 50 %", "STEUERN_E_LUEFTER", "50",
                "El ventilador eléctrico del radiador girará a media potencia. Mantén manos, ropa y cables lejos. " + AvisoMotorParado),
            new E39Action("Relé de precalentamiento (calentadores)", "STEUERN_GLUEHRELAIS", "100",
                "Activa los calentadores. Úsalo solo unos segundos y no lo repitas seguido: se calientan mucho. " + AvisoMotorParado),
            new E39Action("Válvula EGR (AGR) al 95 %", "STEUERN_AGR_STELLER", "95",
                "Mueve la válvula de recirculación de gases; necesita vacío (motor al ralentí para oírla). " + AvisoMotorParado),
            new E39Action("Actuador presión de turbo al 95 %", "STEUERN_LADEDRUCKSTELLER", "95",
                "Mueve la electroválvula de presión de turbo; necesita vacío (motor al ralentí). No aceleres durante la prueba. " + AvisoMotorParado),
            new E39Action("Terminar activaciones", "DIAGNOSE_ENDE", "",
                "Cierra la sesión de diagnosis de la DDE y devuelve el control de todos los actuadores a la centralita."),
        ]);

    // ---------------------------------------------------------------- Cambio automático (EGS), grupo D_0032
    // Variantes E39 de D_0032: GS20, GS8.32, GS8.36, GS8.51, GS8.55, GS8.60.x (GS8600..GS8604).
    // Todas leen el estado con el mismo job STATUS_IO_LESEN; solo se usan resultados presentes en todas.
    private static readonly E39Ecu Egs = new(
        "Cambio automático (EGS)", "D_0032",
        [
            new E39Page("Valores del cambio",
            [
                new E39Value("STATUS_IO_LESEN", "STAT_WAEHLHEBEL_POSITION", "Posición palanca"),
                new E39Value("STATUS_IO_LESEN", "STAT_GANG", "Marcha engranada"),
                new E39Value("STATUS_IO_LESEN", "STAT_PROG_TASTER", "Programa (A / S-M)"),
                new E39Value("STATUS_IO_LESEN", "STAT_GETRIEBETEMPERATUR_WERT", "Temperatura aceite cambio", "°C"),
                new E39Value("STATUS_IO_LESEN", "STAT_MOTORDREHZAHL_WERT", "Régimen motor", "1/min"),
                new E39Value("STATUS_IO_LESEN", "STAT_ABTRIEBSDREHZAHL_WERT", "Régimen de salida", "1/min"),
                new E39Value("STATUS_IO_LESEN", "STAT_MOTORTEMPERATUR_WERT", "Temperatura motor", "°C"),
                new E39Value("STATUS_IO_LESEN", "STAT_DKG_WERT", "Pedal / carga (DKG)", "%"),
                new E39Value("STATUS_IO_LESEN", "STAT_UBAT_WERT", "Tensión batería", "V"),
            ]),
            new E39Page("Electroválvulas y entradas",
            [
                new E39Value("STATUS_IO_LESEN", "STAT_MV1_EIN", "Electroválvula 1", "1=activa"),
                new E39Value("STATUS_IO_LESEN", "STAT_MV2_EIN", "Electroválvula 2", "1=activa"),
                new E39Value("STATUS_IO_LESEN", "STAT_MV3_EIN", "Electroválvula 3", "1=activa"),
                new E39Value("STATUS_IO_LESEN", "STAT_MVSL_EIN", "Bloqueo palanca (shift-lock)", "1=activo"),
                new E39Value("STATUS_IO_LESEN", "STAT_EDS1_WERT", "Regulador de presión EDS1", "mA"),
                new E39Value("STATUS_IO_LESEN", "STAT_BREMSSIGNAL_EIN", "Señal de freno", "1=pisado"),
                new E39Value("STATUS_IO_LESEN", "STAT_KICK_DOWN_EIN", "Kick-down", "1=sí"),
                new E39Value("STATUS_IO_LESEN", "STAT_NOTPROGRAMM_EIN", "Programa de emergencia", "1=activo"),
                new E39Value("STATUS_IO_LESEN", "STAT_SCHALTUNGSART_AKTUELL", "Cambio de marcha actual"),
            ]),
        ],
        []);   // Sin activaciones: probar electroválvulas del cambio no es una prueba segura en casa.

    // ---------------------------------------------------------------- ABS / ASC / DSC, grupo D_0056
    // Variantes E39: ABS5, ASC5, ASC5D (M51), ASC57 (Bosch 5.7), ASC57R75, DSC5, DSC57 (DSC III), DSC3.
    // ABS/ASC 5.x leen todo con STATUS_IO_LESEN; DSC57/DSC3 lo reparten en STATUS_IO_LESEN_* (argumento E = una vez).
    private static readonly E39Ecu Abs = new(
        "ABS / ASC / DSC", "D_0056",
        [
            new E39Page("Ruedas (ABS / ASC 5 y 5.7)",
            [
                new E39Value("STATUS_IO_LESEN", "STAT_RAD_GESCHW_VL_WERT", "Rueda delantera izquierda", "km/h", "E"),
                new E39Value("STATUS_IO_LESEN", "STAT_RAD_GESCHW_VR_WERT", "Rueda delantera derecha", "km/h", "E"),
                new E39Value("STATUS_IO_LESEN", "STAT_RAD_GESCHW_HL_WERT", "Rueda trasera izquierda", "km/h", "E"),
                new E39Value("STATUS_IO_LESEN", "STAT_RAD_GESCHW_HR_WERT", "Rueda trasera derecha", "km/h", "E"),
                new E39Value("STATUS_IO_LESEN", "STAT_BREMSLICHT_SCHALTER_EIN", "Interruptor luz de freno", "1=pisado", "E"),
                new E39Value("STATUS_IO_LESEN", "STAT_PUMPENMOTOR_EIN", "Motor bomba hidráulica", "1=activo", "E"),
                new E39Value("STATUS_IO_LESEN", "STAT_VENTILRELAIS_EIN", "Relé de electroválvulas", "1=activo", "E"),
            ]),
            new E39Page("Ruedas (DSC III 5.7)",
            [
                new E39Value("STATUS_IO_LESEN_GESCHW", "STAT_RAD_GESCHW_VL_WERT", "Rueda delantera izquierda", "km/h", "E"),
                new E39Value("STATUS_IO_LESEN_GESCHW", "STAT_RAD_GESCHW_VR_WERT", "Rueda delantera derecha", "km/h", "E"),
                new E39Value("STATUS_IO_LESEN_GESCHW", "STAT_RAD_GESCHW_HL_WERT", "Rueda trasera izquierda", "km/h", "E"),
                new E39Value("STATUS_IO_LESEN_GESCHW", "STAT_RAD_GESCHW_HR_WERT", "Rueda trasera derecha", "km/h", "E"),
            ]),
            new E39Page("Sensores (DSC III 5.7)",
            [
                new E39Value("STATUS_IO_LESEN_ANALOG", "STAT_LENKWINKEL_WERT", "Ángulo de volante", "°", "E"),
                new E39Value("STATUS_IO_LESEN_ANALOG", "STAT_DREHRATE_WERT", "Velocidad de giro (yaw)", "°/s", "E"),
                new E39Value("STATUS_IO_LESEN_ANALOG", "STAT_QUERBESCHLEUNIGUNG_WERT", "Aceleración lateral", "m/s²", "E"),
                new E39Value("STATUS_IO_LESEN_ANALOG", "STAT_DRUCK_WERT", "Presión de freno", "bar", "E"),
                new E39Value("STATUS_IO_LESEN_ANALOG", "STAT_ZUENDUNG_WERT", "Tensión contacto (borne 15)", "V", "E"),
            ]),
            new E39Page("Entradas y salidas (DSC III 5.7)",
            [
                new E39Value("STATUS_IO_LESEN_DIGITAL", "STAT_BREMSLICHT_SCHALTER_EIN", "Interruptor luz de freno", "1=pisado", "E"),
                new E39Value("STATUS_IO_LESEN_DIGITAL", "STAT_HANDBREMSSCHALTER_EIN", "Interruptor freno de mano", "1=puesto", "E"),
                new E39Value("STATUS_IO_LESEN_DIGITAL", "STAT_PUMPENMOTOR_EIN", "Motor bomba hidráulica", "1=activo", "E"),
                new E39Value("STATUS_IO_LESEN_DIGITAL", "STAT_VORLADEPUMPE_EIN", "Bomba de precarga", "1=activa", "E"),
                new E39Value("STATUS_IO_LESEN_DIGITAL", "STAT_VENTILRELAIS_EIN", "Relé de electroválvulas", "1=activo", "E"),
            ]),
        ],
        []);   // Sin activaciones: tocar válvulas o bomba del freno solo en taller.

    // ---------------------------------------------------------------- Airbag (MRS), grupo D_00A4
    // Variantes E39: MRS3 y MRS4 (E39 Temic) comparten STATUS_LESEN con STAT_ZKn (1 = circuito correcto).
    // Asignación de circuitos según AUSSTATTUNG_LESEN de MRS3; en MRS4 puede variar a partir de ZK4.
    private static readonly E39Ecu Airbag = new(
        "Airbag (MRS)", "D_00A4",
        [
            new E39Page("Circuitos de disparo (MRS3 / MRS4)",
            [
                new E39Value("STATUS_LESEN", "STAT_ZK0", "ZK0 airbag conductor", "1=OK"),
                new E39Value("STATUS_LESEN", "STAT_ZK1", "ZK1 pretensor conductor", "1=OK"),
                new E39Value("STATUS_LESEN", "STAT_ZK2", "ZK2 pretensor acompañante", "1=OK"),
                new E39Value("STATUS_LESEN", "STAT_ZK3", "ZK3 airbag acompañante", "1=OK"),
                new E39Value("STATUS_LESEN", "STAT_ZK4", "ZK4 lateral delantero izquierdo", "1=OK"),
                new E39Value("STATUS_LESEN", "STAT_ZK5", "ZK5 lateral delantero derecho", "1=OK"),
                new E39Value("STATUS_LESEN", "STAT_ZK6", "ZK6 lateral trasero izquierdo", "1=OK"),
                new E39Value("STATUS_LESEN", "STAT_ZK7", "ZK7 lateral trasero derecho", "1=OK"),
                new E39Value("STATUS_LESEN", "STAT_ZK8", "ZK8 cabeza (ITS) izquierdo", "1=OK"),
                new E39Value("STATUS_LESEN", "STAT_ZK9", "ZK9 cabeza (ITS) derecho", "1=OK"),
                new E39Value("STATUS_LESEN", "STAT_ZK10", "ZK10 desconexión batería", "1=OK"),
                new E39Value("STATUS_LESEN", "STAT_ZK11", "ZK11 airbag acompañante 2.ª etapa", "1=OK"),
            ]),
        ],
        []);   // Nunca activaciones en el airbag.

    // ---------------------------------------------------------------- Cuadro de instrumentos, grupo D_0080
    // Variantes E39: IKE (cuadro alto), IKI, KOMBI39 (cuadro básico), KOMBI39C (con CAN).
    // Temperaturas del IKE en valor bruto del convertidor A/D (el propio .prg pone unidad "ADC-WERT").
    private static readonly E39Ecu Ike = new(
        "Cuadro de instrumentos (IKE / KOMBI)", "D_0080",
        [
            new E39Page("Valores del cuadro",
            [
                new E39Value("STATUS_ANALOG_LESEN", "STAT_GESCHWINDIGKEIT_WERT", "Velocidad", "km/h"),
                new E39Value("STATUS_ANALOG_LESEN", "STAT_DREHZAHL_WERT", "Régimen motor", "1/min"),
                new E39Value("STATUS_TANKINHALT_LESEN", "STAT_TANKINHALT_WERT", "Combustible en depósito", "l"),
                new E39Value("STATUS_ANALOG_LESEN", "STAT_KUEHLMITTELTEMP_WERT", "Temperatura refrigerante (bruto A/D)"),
                new E39Value("STATUS_ANALOG_LESEN", "STAT_AUSSENTEMP_WERT", "Temperatura exterior (bruto A/D)"),
                new E39Value("STATUS_ANALOG_LESEN", "STAT_TKVA1_WERT", "Señal de consumo", "ms"),
            ]),
            new E39Page("Entradas del cuadro",
            [
                new E39Value("STATUS_IO_LESEN", "STAT_KLR_EIN", "Borne R (accesorios)", "1=sí"),
                new E39Value("STATUS_IO_LESEN", "STAT_KL15_EIN", "Borne 15 (contacto)", "1=sí"),
                new E39Value("STATUS_IO_LESEN", "STAT_KL50_EIN", "Borne 50 (arranque)", "1=sí"),
                new E39Value("STATUS_IO_LESEN", "STAT_KOMBITASTE_EIN", "Botón del cuadro", "1=pulsado"),
                new E39Value("STATUS_IO_LESEN", "STAT_SIA_RESET_EIN", "Reset indicador de servicio", "1=activo"),
                new E39Value("STATUS_IO_LESEN", "STAT_AGS_EIN", "Señal cambio automático", "1=sí"),
            ]),
        ],
        [
            new E39Action("Autotest del cuadro", "STEUERN_SELBSTTEST", "",
                "Las agujas y los testigos del cuadro hacen un barrido de prueba. Es normal ver testigos encendidos " +
                "durante unos segundos. Hazlo con el coche parado y contacto puesto."),
            new E39Action("Gong de aviso", "STEUERN_GONG3", "",
                "Suena el gong de aviso del cuadro una vez. Coche parado y contacto puesto."),
        ]);

    // ---------------------------------------------------------------- Módulo de luces (LCM), grupo D_00D0
    // Variantes E39: LCM, LCM_A, LCM_II, LCM_III, LCM_IV. STATUS_LESEN y STATUS_VORGEBEN son comunes.
    private const string AvisoLuces =
        "La luz se enciende desde el módulo de luces aunque el mando esté apagado. Coche parado y contacto puesto. " +
        "Si la luz se queda encendida al acabar, quita el contacto.";

    private static readonly E39Ecu Lcm = new(
        "Módulo de luces (LCM)", "D_00D0",
        [
            new E39Page("Mandos de luces",
            [
                new E39Value("STATUS_LESEN", "STAT_SCHALTER1_SL_EIN", "Mando luces de posición", "1=on"),
                new E39Value("STATUS_LESEN", "STAT_SCHALTER1_AL_EIN", "Mando luces de cruce", "1=on"),
                new E39Value("STATUS_LESEN", "STAT_SCHALTER_FL_EIN", "Palanca luces largas", "1=on"),
                new E39Value("STATUS_LESEN", "STAT_SCHALTER_LICHTHUPE_EIN", "Ráfagas", "1=on"),
                new E39Value("STATUS_LESEN", "STAT_SCHALTER_NSW_EIN", "Mando antiniebla delanteros", "1=on"),
                new E39Value("STATUS_LESEN", "STAT_SCHALTER_NSL_EIN", "Mando antiniebla trasero", "1=on"),
                new E39Value("STATUS_LESEN", "STAT_SCHALTER1_BLK_LINKS_EIN", "Intermitente izquierdo", "1=on"),
                new E39Value("STATUS_LESEN", "STAT_SCHALTER1_BLK_RECHTS_EIN", "Intermitente derecho", "1=on"),
                new E39Value("STATUS_LESEN", "STAT_SCHALTER_WBL_EIN", "Botón warning", "1=on"),
                new E39Value("STATUS_LESEN", "STAT_EINGANG1_BLS_EIN", "Interruptor luz de freno", "1=pisado"),
                new E39Value("STATUS_LESEN", "STAT_U_BATT_WERT", "Tensión batería", "V"),
                new E39Value("STATUS_LESEN", "STAT_EINGANGSSPANNUNG_DIMMER_WERT", "Regulador iluminación cuadro", "V"),
            ]),
            new E39Page("Lámparas fundidas",
            [
                new E39Value("STATUS_LESEN", "STAT_AL_LINKS_DEFEKT", "Cruce izquierda", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_AL_RECHTS_DEFEKT", "Cruce derecha", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_FL_LINKS_DEFEKT", "Largas izquierda", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_FL_RECHTS_DEFEKT", "Largas derecha", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_SL_LINKS_VORN_DEFEKT", "Posición delantera izquierda", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_SL_RECHTS_VORN_DEFEKT", "Posición delantera derecha", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_SL_LINKS_HINTEN_DEFEKT", "Posición trasera izquierda", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_SL_RECHTS_HINTEN_DEFEKT", "Posición trasera derecha", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_BL_LINKS_DEFEKT", "Freno izquierda", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_BL_RECHTS_DEFEKT", "Freno derecha", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_BREMSLICHT_MITTE_DEFEKT", "Tercera luz de freno", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_BLK_LINKS_VORN_DEFEKT", "Intermitente delantero izquierdo", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_BLK_RECHTS_VORN_DEFEKT", "Intermitente delantero derecho", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_BLK_LINKS_HINTEN_DEFEKT", "Intermitente trasero izquierdo", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_BLK_RECHTS_HINTEN_DEFEKT", "Intermitente trasero derecho", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_NSW_LINKS_DEFEKT", "Antiniebla delantero izquierdo", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_NSW_RECHTS_DEFEKT", "Antiniebla delantero derecho", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_NSL_LINKS_DEFEKT", "Antiniebla trasero izquierdo", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_NSL_RECHTS_DEFEKT", "Antiniebla trasero derecho", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_RFS_LINKS_DEFEKT", "Marcha atrás izquierda", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_RFS_RECHTS_DEFEKT", "Marcha atrás derecha", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_KZL_LINKS_DEFEKT", "Matrícula izquierda", "1=fundida"),
                new E39Value("STATUS_LESEN", "STAT_KZL_RECHTS_DEFEKT", "Matrícula derecha", "1=fundida"),
            ]),
        ],
        [
            new E39Action("Encender cruce izquierda", "STATUS_VORGEBEN", "AL_L", AvisoLuces),
            new E39Action("Encender cruce derecha", "STATUS_VORGEBEN", "AL_R", AvisoLuces),
            new E39Action("Encender largas izquierda", "STATUS_VORGEBEN", "FL_L", AvisoLuces),
            new E39Action("Encender largas derecha", "STATUS_VORGEBEN", "FL_R", AvisoLuces),
            new E39Action("Encender luces de freno", "STATUS_VORGEBEN", "BL_L;BL_R;BL_M", AvisoLuces),
            new E39Action("Encender antiniebla delanteros", "STATUS_VORGEBEN", "NSW_L;NSW_R", AvisoLuces),
            new E39Action("Encender marcha atrás", "STATUS_VORGEBEN", "RFS_L;RFS_R", AvisoLuces),
        ]);

    // ---------------------------------------------------------------- Módulo general (GM / ZKE III), grupo D_ZKE_GM
    // D_ZKE_GM (no D_0000) es el grupo que incluye el GM III del E38/E39: variantes ZKE3_GM1 y ZKE3_GM5.
    private const string AvisoGm =
        "El módulo general activa la salida mientras dure la prueba. Coche parado, llave fuera del bombín y " +
        "puertas a la vista. Usa la acción \"soltar\" correspondiente para desactivarla.";

    private static readonly E39Ecu Gm = new(
        "Módulo general (GM / cierre)", "D_ZKE_GM",
        [
            new E39Page("Puertas, capós y bornes",
            [
                new E39Value("STATUS_DIGITAL_GM3_INT", "STAT_IFN_FTOFFEN_AKTIV", "Puerta conductor abierta", "1=sí"),
                new E39Value("STATUS_DIGITAL_GM3_INT", "STAT_IFN_BTOFFEN_AKTIV", "Puerta acompañante abierta", "1=sí"),
                new E39Value("STATUS_DIGITAL_GM3_EA", "STAT_E_TKFH_AKTIV", "Puerta trasera izquierda", "1=abierta"),
                new E39Value("STATUS_DIGITAL_GM3_EA", "STAT_E_TKBH_AKTIV", "Puerta trasera derecha", "1=abierta"),
                new E39Value("STATUS_DIGITAL_GM3_EA", "STAT_E_HKK_AKTIV", "Contacto maletero", "1=activo"),
                new E39Value("STATUS_DIGITAL_GM3_EA", "STAT_E_MHK_AKTIV", "Contacto capó motor", "1=activo"),
                new E39Value("STATUS_DIGITAL_GM3_EA", "STAT_E_HFK_AKTIV", "Contacto guantera", "1=activo"),
                new E39Value("STATUS_DIGITAL_GM3_KP", "STAT_K_KLR_AKTIV", "Borne R (K-Bus)", "1=sí"),
                new E39Value("STATUS_DIGITAL_GM3_KP", "STAT_K_KL15_AKTIV", "Borne 15 (K-Bus)", "1=sí"),
                new E39Value("STATUS_DIGITAL_GM3_KP", "STAT_K_KL50_AKTIV", "Borne 50 (K-Bus)", "1=sí"),
            ]),
            new E39Page("Cierre centralizado y mandos",
            [
                new E39Value("STATUS_DIGITAL_GM3_INT", "STAT_IFN_ZV_VERRIEGELT", "Cierre centralizado cerrado", "1=sí"),
                new E39Value("STATUS_DIGITAL_GM3_INT", "STAT_IFN_ZV_GESICHERT", "Cierre centralizado con seguro", "1=sí"),
                new E39Value("STATUS_DIGITAL_GM3_EA", "STAT_E_TZV_AKTIV", "Botón cierre centralizado", "1=pulsado"),
                new E39Value("STATUS_DIGITAL_GM3_EA", "STAT_E_SIB_AKTIV", "Botón luz interior", "1=pulsado"),
                new E39Value("STATUS_DIGITAL_GM3_EA", "STAT_E_SW1_AKTIV", "Mando limpiaparabrisas 1", "1=on"),
                new E39Value("STATUS_DIGITAL_GM3_EA", "STAT_E_SW2_AKTIV", "Mando limpiaparabrisas 2", "1=on"),
                new E39Value("STATUS_DIGITAL_GM3_EA", "STAT_E_SWP_AKTIV", "Mando lavaparabrisas", "1=on"),
                new E39Value("STATUS_DIGITAL_GM3_EA", "STAT_E_RSK_AKTIV", "Contacto reposo limpiaparabrisas", "1=activo"),
            ]),
            new E39Page("Valores analógicos",
            [
                new E39Value("STATUS_ANALOG_GM3", "STAT_KMH_WERT", "Velocidad (K-Bus)", "km/h"),
                new E39Value("STATUS_ANALOG_GM3", "STAT_TEMP_WERT", "Temperatura exterior (K-Bus)", "°C"),
                new E39Value("STATUS_ANALOG_GM3", "STAT_U30L1_WERT", "Tensión circuito de carga 1", "V"),
                new E39Value("STATUS_ANALOG_GM3", "STAT_U30L2_WERT", "Tensión circuito de carga 2", "V"),
                new E39Value("STATUS_ANALOG_GM3", "STAT_AIB_WERT", "Luz interior (PWM)", "%"),
                new E39Value("STATUS_ANALOG_GM3", "STAT_UWIINT_WERT", "Potenciómetro intervalo limpia", "V"),
            ]),
        ],
        [
            new E39Action("Cierre centralizado: abrir", "STEUERN_DIGITAL_GM3", "MER;1", AvisoGm),
            new E39Action("Cierre centralizado: soltar abrir", "STEUERN_DIGITAL_GM3", "MER;0", "Desactiva el relé de apertura."),
            new E39Action("Cierre centralizado: cerrar", "STEUERN_DIGITAL_GM3", "MVR;1",
                "Cuidado: con las llaves dentro el coche puede quedar cerrado. " + AvisoGm),
            new E39Action("Cierre centralizado: soltar cerrar", "STEUERN_DIGITAL_GM3", "MVR;0", "Desactiva el relé de cierre."),
            new E39Action("LED de alarma: encender", "STEUERN_DIGITAL_GM3", "DWAL;1", AvisoGm),
            new E39Action("LED de alarma: apagar", "STEUERN_DIGITAL_GM3", "DWAL;0", "Apaga el LED de la alarma."),
        ]);

    // ---------------------------------------------------------------- Inmovilizador (EWS), grupo D_0044
    // Variantes: EWS (EWS 2), EWS3, EWS3D. STATUS_LESEN es común.
    private static readonly E39Ecu Ews = new(
        "Inmovilizador (EWS)", "D_0044",
        [
            new E39Page("Estado del inmovilizador",
            [
                new E39Value("STATUS_LESEN", "STAT_AKTUELLE_SCHLUESSELNUMMER", "Llave detectada (número)"),
                new E39Value("STATUS_LESEN", "STAT_AKTUELLE_SCHLUESSEL_ID_TEXT", "Estado de la llave"),
                new E39Value("STATUS_LESEN", "STAT_SCHLUESSEL_GESPERRT", "Llave bloqueada", "1=sí"),
                new E39Value("STATUS_LESEN", "STAT_AKTUELLER_WC_TEXT", "Código variable (DME/DDE)"),
                new E39Value("STATUS_LESEN", "STAT_VORGABE_DME", "Liberación arranque a la DDE", "1=sí"),
                new E39Value("STATUS_LESEN", "STAT_VORGABE_ANLASSERRELAIS", "Liberación relé de arranque", "1=sí"),
                new E39Value("STATUS_LESEN", "STAT_P_N_EINGANG", "Entrada P/N (cambio)", "1=activa"),
                new E39Value("STATUS_LESEN", "STAT_EWS_JUNGFRAEULICH", "EWS virgen (sin emparejar)", "1=sí"),
            ]),
        ],
        []);   // Sin activaciones: el EWS solo se toca para emparejar en taller.

    // ---------------------------------------------------------------- Climatización (IHKA), grupo D_005B
    // Variantes E39: IHKA39, IHKA39_2.._5 (climatizador automático), IHKR39 (aire manual), IHR39 (solo calefacción).
    // IHR39 usa otros resultados (sin A/C ni sondas de temperatura): con esa variante estas páginas darán error.
    private const string AvisoClima =
        "Activación del climatizador. Coche parado y contacto puesto (el compresor solo actúa con el motor en marcha). " +
        "Al acabar pulsa \"Terminar activaciones\" o quita el contacto.";

    private static readonly E39Ecu Clima = new(
        "Climatización (IHKA)", "D_005B",
        [
            new E39Page("Temperaturas",
            [
                new E39Value("STATUS_ANALOGEINGAENGE", "STAT_TINNEN_WERT", "Temperatura interior", "°C"),
                new E39Value("STATUS_ANALOGEINGAENGE", "STAT_TAUSSEN_WERT", "Temperatura exterior", "°C"),
                new E39Value("STATUS_ANALOGEINGAENGE", "STAT_TVERDAMPFER_WERT", "Temperatura evaporador", "°C"),
                new E39Value("STATUS_ANALOGEINGAENGE", "STAT_TKUEHLERWASSER_WERT", "Temperatura refrigerante", "°C"),
                new E39Value("STATUS_ANALOGEINGAENGE", "STAT_KLEMME30_WERT", "Tensión borne 30", "V"),
            ]),
            new E39Page("Mandos y ventilador",
            [
                new E39Value("STATUS_BEDIENTEIL", "STAT_GEBLAESE_WERT", "Ventilador habitáculo", "%"),
                new E39Value("STATUS_BEDIENTEIL", "STAT_FUNKTION_AC_EIN", "Botón A/C", "1=on"),
                new E39Value("STATUS_BEDIENTEIL", "STAT_FUNKTION_UMLUFT_EIN", "Recirculación", "1=on"),
                new E39Value("STATUS_BEDIENTEIL", "STAT_FUNKTION_HHS_EIN", "Luneta térmica", "1=on"),
                new E39Value("STATUS_REGLERGROESSEN", "STAT_DREHZAHL_WERT", "Régimen motor (recibido)", "1/min"),
                new E39Value("STATUS_MOTOR_KLAPPENPOSITION", "STAT_FRISCHLUFT_WERT", "Trampilla aire fresco", "%"),
            ]),
            new E39Page("Salidas del climatizador",
            [
                new E39Value("STATUS_IO", "STAT_ANSTEUERUNG_KOMPRESSOR_EIN", "Compresor A/C", "1=on"),
                new E39Value("STATUS_IO", "STAT_ZUSATZLUEFTER_STUFE_1_EIN", "Ventilador auxiliar etapa 1", "1=on"),
                new E39Value("STATUS_IO", "STAT_ZUSATZWASSERPUMPE_EIN", "Bomba de agua adicional", "1=on"),
                new E39Value("STATUS_IO", "STAT_RELAIS_HECKSCHEIBE_EIN", "Relé luneta térmica", "1=on"),
                new E39Value("STATUS_IO", "STAT_SPRITZDUESENHEIZUNG_EIN", "Calefacción surtidores lavaparabrisas", "1=on"),
                new E39Value("STATUS_IO", "STAT_KLEMME_15_EIN", "Borne 15 (contacto)", "1=sí"),
            ]),
        ],
        [
            new E39Action("Luneta térmica: encender", "STEUERN_RELAIS_HECKSCHEIBE", "EIN", AvisoClima),
            new E39Action("Luneta térmica: apagar", "STEUERN_RELAIS_HECKSCHEIBE", "AUS", "Apaga el relé de la luneta térmica."),
            new E39Action("Ventilador habitáculo al 50 %", "STEUERN_GEBLAESE", "50", AvisoClima),
            new E39Action("Bomba de agua adicional: encender", "STEUERN_ZUSATZWASSERPUMPE", "EIN", AvisoClima),
            new E39Action("Bomba de agua adicional: apagar", "STEUERN_ZUSATZWASSERPUMPE", "AUS", "Apaga la bomba de agua adicional."),
            new E39Action("Terminar activaciones", "DIAGNOSE_ENDE", "",
                "Cierra la sesión de diagnosis del climatizador y le devuelve el control de todas las salidas."),
        ]);

    static IReadOnlyList<E39Ecu> _ecus = BuildDefault();
    public static IReadOnlyList<E39Ecu> Ecus => _ecus;

    private static IReadOnlyList<E39Ecu> BuildDefault() => [Dde, Egs, Abs, Airbag, Ike, Lcm, Gm, Ews, Clima];

    public static void TryLoadFromFolder(string ecuPath)
    {
        if (string.IsNullOrWhiteSpace(ecuPath))
            return;
        string file = Path.Combine(ecuPath, "e39_catalog.json");
        if (!File.Exists(file))
            return;
        try
        {
            using var stream = File.OpenRead(file);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            var dtos = JsonSerializer.Deserialize<EcuJsonDto[]>(stream, options);
            if (dtos != null && dtos.Length > 0)
                _ecus = dtos.Select(ToEcu).ToList();
        }
        catch (Exception)
        {
            // Error de parseo: mantener los datos por defecto sin romper la app.
        }
    }

    private static E39Ecu ToEcu(EcuJsonDto d) => new(
        d.Title, d.Sgbd,
        d.StatusPages.Select(p => new E39Page(p.Title,
            p.Values.Select(v => new E39Value(v.Job, v.Result, v.Label, v.Unit, v.Args)).ToList()
        )).ToList(),
        d.Actions.Select(a => new E39Action(a.Title, a.Job, a.Args, a.Warning)).ToList(),
        d.IdentJob, d.FsReadJob, d.FsClearJob);

    private sealed class EcuJsonDto
    {
        public string Title { get; init; } = "";
        public string Sgbd { get; init; } = "";
        public string IdentJob { get; init; } = "IDENT";
        public string FsReadJob { get; init; } = "FS_LESEN";
        public string FsClearJob { get; init; } = "FS_LOESCHEN";
        public List<PageJsonDto> StatusPages { get; init; } = [];
        public List<ActionJsonDto> Actions { get; init; } = [];
    }

    private sealed class PageJsonDto
    {
        public string Title { get; init; } = "";
        public List<ValueJsonDto> Values { get; init; } = [];
    }

    private sealed class ValueJsonDto
    {
        public string Job { get; init; } = "";
        public string Result { get; init; } = "";
        public string Label { get; init; } = "";
        public string Unit { get; init; } = "";
        public string Args { get; init; } = "";
    }

    private sealed class ActionJsonDto
    {
        public string Title { get; init; } = "";
        public string Job { get; init; } = "";
        public string Args { get; init; } = "";
        public string Warning { get; init; } = "";
    }
}
