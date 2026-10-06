# InpaDroid

> [English](README.md) | **[Español]**

InpaDroid es una app para Android que imita a BMW INPA. Por debajo usa [ediabaslib](https://github.com/uholeschak/ediabaslib), una implementación libre de EDIABAS, así que habla con las centralitas igual que INPA o Tool32: carga un `.prg`, ejecuta sus jobs y enseña los resultados.

La app no trae ningún archivo de BMW. Los `.prg` y `.grp` los pones tú, copiándolos a una carpeta del móvil.

> **Aviso:** INPA y EDIABAS son marcas registradas de BMW AG. Este proyecto no está afiliado ni respaldado por BMW AG de ninguna manera.

---

## Qué hace esta primera versión

Esta versión es genérica: no lee los scripts `.ipo` de INPA, sino que monta las pantallas a partir de los jobs que hay dentro de cada `.prg`. Por eso sirve para cualquier centralita que tengas, aunque las pantallas no tengan el diseño específico de cada script de INPA.

Tiene cuatro pantallas genéricas, más un menú propio para el BMW E39 (ver [BMW E39](#bmw-e39)):

- **Inicio** — la pantalla de arranque, parecida a la de INPA. Desde aquí se abre la lista de centralitas, se identifica el vehículo y se entra en Ajustes.
- **Selección de centralita** — lista de todos los `.prg` y `.grp` de la carpeta, con buscador. Los grupos (`.grp`) salen arriba y marcados.
- **Pantalla de centralita** — Info, Ident, leer y borrar la memoria de errores, Status, Steuern y una lista de todos los jobs al estilo Tool32.
- **Ajustes** — tipo de adaptador, datos de conexión y carpeta de archivos.

Como en INPA, abajo hay una barra con las teclas F1 a F10 y un botón Shift que cambia las etiquetas a Shift+F1…F10. Arriba, en todas las pantallas, hay dos indicadores: Batería y Encendido.

---

## Qué necesitas

**Hardware:**
- Un móvil o tablet Android.
- Un cable o adaptador de diagnóstico. Valen estos:
  - **Cable K+DCAN USB** (serie E). Tiene que llevar chip FTDI y necesitas un adaptador OTG para enchufarlo al móvil. Los clones con otros chips (CH340, por ejemplo) no funcionan.
  - **Cable ENET** (serie F/G). Se conecta por WiFi si tienes un adaptador ENET WiFi, o con un adaptador USB-Ethernet en el móvil.
  - **Adaptador ELM327** por Bluetooth o WiFi. Es la opción más limitada: con centralitas D-CAN puede servir, pero con coches BMW que usan DS2 o línea K casi nada funciona. Si tu coche es de la serie E, usa el cable K+DCAN.

**Software:**
- Tus archivos `.prg` y `.grp` de EDIABAS, los mismos que tienes en `C:\EDIABAS\Ecu` en el PC.

---

## Compilar la APK

El entorno de compilación requiere:
- .NET 10 con el workload de Android (`~/.dotnet`)
- JDK 21 (`/usr/lib/jvm/java-21-openjdk-amd64`)
- Android SDK (`~/Android/Sdk`)

Desde la carpeta `InpaDroid` ejecuta:

```sh
./build.sh
```

La APK queda en `out/InpaDroid.apk`.

Para instalarla en el móvil tienes dos opciones:

1. Copia `InpaDroid.apk` al móvil, ábrela desde el gestor de archivos y acepta la instalación. Android pedirá permitir apps de origen desconocido para esa aplicación; dale permiso y vuelve atrás.
2. Con depuración USB activada en el móvil:

   ```sh
   adb install -r out/InpaDroid.apk
   ```

---

## Copiar los archivos de BMW al móvil

Por defecto la app busca los archivos en su propia carpeta del almacenamiento interno:

```
Almacenamiento interno/
└── Android/
    └── data/
        └── com.inpadroid.app/
            └── files/
                └── Ecu/
                    ├── D_MOTOR.grp
                    ├── MS450DS0.prg
                    ├── UTILITY.prg
                    └── ...
```

La ruta completa suele ser `/storage/emulated/0/Android/data/com.inpadroid.app/files/Ecu/`. La carpeta `com.inpadroid.app` aparece después de abrir la app por primera vez.

**Pasos:**
1. Abre InpaDroid una vez y ciérrala.
2. Conecta el móvil al PC por USB y elige "Transferencia de archivos".
3. En el PC, entra en el móvil y ve a `Android/data/com.inpadroid.app/files/`.
4. Copia ahí dentro el contenido de tu `C:\EDIABAS\Ecu` (los `.prg` y `.grp` sueltos dentro de `Ecu`, sin subcarpetas).

En Android 11 y posteriores muchos gestores de archivos del propio móvil no dejan entrar en `Android/data`. Desde el PC por cable normalmente sí se puede. Si no te deja, usa otra carpeta y cámbiala en Ajustes.

**Qué archivos importan:**
- `.prg` y `.grp` — cópialos todos.
- `UTILITY.prg` — conviene que esté; los indicadores de batería y encendido salen de él.
- `.ipo` y `.ips` (los scripts de INPA, en `INPA\SGDAT`) — esta versión no los usa. No pasa nada si los copias, pero los ignora.
- `.exe`, `.dll` y demás archivos de Windows — no sirven en Android, no hace falta copiarlos.

---

## Configurar el adaptador

Todo se hace en Ajustes. Elige el tipo de adaptador y rellena lo que corresponda.

### Cable K+DCAN USB

No hay nada más que configurar. Conecta el cable al móvil con el adaptador OTG y al coche. La primera vez Android pregunta si InpaDroid puede usar el dispositivo USB; acepta (y marca que lo recuerde, si te lo ofrece).

### ENET

En host ENET deja `auto` para que la app busque el coche en la red. Si no lo encuentra, escribe la IP del coche o del adaptador ENET WiFi. Con WiFi, conecta antes el móvil a la red del adaptador; con USB-Ethernet, enchufa el adaptador al móvil y el cable al coche.

### ELM327 Bluetooth

Empareja primero el ELM327 desde los ajustes de Bluetooth de Android (el PIN suele ser 1234 o 0000). Después, en Ajustes de InpaDroid, elige el adaptador de la lista de dispositivos emparejados. En Android 12 o posterior la app pide permiso de "Dispositivos cercanos" para usar Bluetooth; hay que dárselo.

### ELM327 WiFi

Conecta el móvil a la red WiFi del ELM327 y escribe su dirección en host ELM WiFi. Por defecto es `192.168.0.10:35000`, que es la de la mayoría de estos adaptadores. Si el tuyo usa otra, la verás en sus instrucciones.

---

## Uso paso a paso

1. Conecta el adaptador al coche y pon el contacto (posición 2, motor apagado).
2. Abre InpaDroid. En la cabecera, **Batería** se pone en verde cuando la app lee tensión en el conector, y **Encendido** cuando detecta el contacto puesto. Se refrescan cada dos segundos. Si se quedan en gris, revisa el cable, el contacto y que tengas `UTILITY.prg` en la carpeta.
3. En la pantalla de inicio tienes:

   | Tecla | Función |
   |-------|---------|
   | F1 | Info |
   | F2 | Selección de centralita |
   | F3 | Identificar vehículo (necesita `FA` o `UTILITY`) |
   | F4 | Menú BMW E39 |
   | F9 | Ajustes |
   | F10 | Salir |

4. Pulsa **F2** y busca la centralita. Puedes escribir parte del nombre en el buscador (por ejemplo `MOTOR` o `EGS`). Si eliges un `.grp`, la app averigua qué `.prg` concreto lleva tu coche, igual que hace INPA.
5. Dentro de la centralita, las teclas son las de un script INPA estándar:

   | Tecla | Función | Job que ejecuta |
   |-------|---------|-----------------|
   | F1 | Info | `INFO` |
   | F2 | Ident | `IDENT` |
   | F4 | Leer memoria de errores | `FS_LESEN` |
   | Shift+F4 | Borrar memoria de errores (pide confirmación) | `FS_LOESCHEN` |
   | F5 | Status (lectura continua) | jobs `STATUS_*` |
   | F6 | Steuern (activar componentes) | jobs `STEUERN_*` |
   | F7 | Todos los jobs (Tool32) | — |
   | F10 | Volver | — |

Status repite el job elegido cada segundo. Para en cuanto pulsas cualquier tecla.

Steuern muestra un aviso y pide confirmación antes de enviar el job.

F7 funciona como Tool32: eliges cualquier job, escribes los argumentos separados por `;` y lo ejecutas. La lista de jobs se lee directamente del `.prg`, así que funciona sin coche conectado.

---

## BMW E39

Esta parte es para el E39 diésel: 520d con motor M47, o 525d y 530d con M57.

### Cable

En el E39 solo sirve el cable K+DCAN USB con chip FTDI. El coche va por línea K (DS2/KWP2000); con eso ni el ELM327 ni el ENET funcionan. El cable lleva un interruptor (o un puente) entre los pines 7 y 8 del conector OBD: tiene que estar en la posición **old** (pines unidos). Si lo dejas en la posición de los coches nuevos, no conecta.

Al móvil se enchufa con un adaptador OTG.

Los E39 fabricados antes de septiembre de 2000 pueden no tener el conector OBD de 16 pines conectado a todas las centralitas. En esos coches a veces hace falta el adaptador al conector redondo de 20 pines que hay bajo el capó.

Mientras diagnosticas, el contacto tiene que estar puesto (posición 2) con el motor parado. Si vas a estar mucho rato, conecta un cargador de batería: con el contacto puesto la batería baja rápido, y una tensión baja da errores raros en las centralitas.

### Archivos

Del paquete `daten E39 v70` solo hace falta la carpeta `ecu`. Copia todo su contenido (todos los `.prg` y `.grp`, sueltos, sin subcarpetas) en la carpeta `Ecu` de la app. Son unos 700 `.prg` y 241 `.grp`.

El resto de carpetas (`sgdat`, `daten`, `data`, `work`, `cfgdat`) no hace falta en el móvil.

### Menú Vehículo E39

En la pantalla de inicio, **F4** abre el menú BMW E39. Sale la lista de centralitas del E39, con la del motor diésel (DDE) la primera.

Dentro de cada centralita las teclas siguen la misma idea que la pantalla genérica:

- Ident, para leer la identificación de la centralita.
- Leer la memoria de errores, y borrarla (pide confirmación).
- Páginas de status, con valores que se refrescan en directo.
- Tests de actuadores (Steuern). Antes de lanzar cada uno sale un aviso y hay que confirmar.
- F7 para ver todos los jobs del `.prg` y lanzar cualquiera, como en Tool32.

### Primera prueba en el coche

1. Instala la APK en el móvil.
2. Copia el contenido de `ecu` a la carpeta `Ecu` de la app y comprueba en Ajustes que detecta los `.prg` y `.grp`.
3. Pon el interruptor del cable en **old** y conéctalo al coche y al móvil con el OTG.
4. En Ajustes, elige "Cable USB K+DCAN (FTDI)" y pulsa **Detectar cable**. Cuando Android pregunte, acepta el permiso USB.
5. Pon el contacto (posición 2).
6. Vuelve a la pantalla de inicio y espera a que Batería y Encendido se pongan en verde.
7. Pulsa **F4** (Vehículo BMW E39), entra en DDE y pulsa Ident.

Si Ident devuelve datos de la centralita, la conexión funciona y puedes seguir con la memoria de errores y las páginas de status.

Si algo falla, la app escribe el error en rojo. Para pedir ayuda, copia ese texto tal cual o haz una captura de pantalla, y di en qué paso te quedaste, qué centralita estabas abriendo y si Batería y Encendido estaban en verde.

---

## Problemas frecuentes

**No conecta**
Comprueba que el contacto está puesto y que el tipo de adaptador de Ajustes es el que tienes enchufado. Con el cable USB, mira que sea FTDI, que el adaptador OTG funcione y que aceptaste el permiso USB. Con ENET, que el móvil esté en la red correcta; si `auto` no lo encuentra, pon la IP a mano. Con ELM327, recuerda que en coches con DS2 o línea K no funciona bien. El error sale escrito en pantalla; si no lo entiendes, apúntalo tal cual.

**No aparecen las centralitas**
Ve a Ajustes y mira cuántos `.prg`/`.grp` ha encontrado. Si son cero, los archivos no están en la carpeta configurada o están dentro de una subcarpeta. Tienen que estar sueltos dentro de `Ecu`.

**El job no existe**
No todas las centralitas tienen `INFO`, `IDENT`, `FS_LESEN` y demás, y a veces se llaman distinto. Entra en F7 Jobs para ver qué jobs tiene de verdad ese `.prg`. Si estás usando un `.grp` y falla, prueba a abrir directamente el `.prg` de tu centralita.

**Permisos de Bluetooth o USB denegados**
Si denegaste un permiso, la app no puede volver a pedirlo sola. Ve a Ajustes de Android → Aplicaciones → InpaDroid → Permisos y activa "Dispositivos cercanos" para Bluetooth. Para USB, desenchufa y vuelve a enchufar el cable: Android vuelve a preguntar.

---

## Ampliar el catálogo E39

La app incluye un catálogo de nueve centralitas E39. Puedes reemplazarlo o ampliarlo dejando un archivo `e39_catalog.json` en tu carpeta ECU. La app lo carga automáticamente al arrancar; si falta o tiene errores, usa el catálogo incorporado.

El esquema JSON está documentado en [`src/InpaDroid/Ui/E39/e39_catalog.json`](src/InpaDroid/Ui/E39/e39_catalog.json), que también sirve como ejemplo completo.

---

## Cómo contribuir

Ver [CONTRIBUTING.md](CONTRIBUTING.md).

---

## Licencia

GPLv3 — ver [LICENSE](LICENSE). Cualquier dependencia que añadas debe ser compatible con la GPL.

---

## Créditos

- [ediabaslib](https://github.com/uholeschak/ediabaslib) de Ulrich Holeschak — el motor EDIABAS que hace posible todo esto.
- Partes de este proyecto se desarrollaron con la ayuda de [Claude Code](https://claude.com/claude-code), la herramienta de programación IA de Anthropic, utilizada de forma responsable como asistente de desarrollo — generación de código, revisión y refactorización bajo supervisión humana.
