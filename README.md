# InpaDroid

InpaDroid es una app para Android que imita a BMW INPA. Por debajo usa [ediabaslib](https://github.com/uholeschak/ediabaslib), una implementación libre de EDIABAS, así que habla con las centralitas igual que INPA o Tool32: carga un `.prg`, ejecuta sus jobs y enseña los resultados.

La app no trae ningún archivo de BMW. Los `.prg` y `.grp` los pones tú, copiándolos a una carpeta del móvil.

## Qué hace esta primera versión

Esta versión es genérica: no lee los scripts `.ipo` de INPA, sino que monta las pantallas a partir de los jobs que hay dentro de cada `.prg`. Por eso sirve para cualquier centralita que tengas, aunque las pantallas no tengan el diseño específico de cada script de INPA.

Tiene cuatro pantallas genéricas, más un menú propio para el BMW E39 (ver [BMW E39](#bmw-e39)):

- Inicio: la pantalla de arranque, parecida a la de INPA. Desde aquí se abre la lista de centralitas, se identifica el vehículo y se entra en Ajustes.
- Selección de centralita: lista de todos los `.prg` y `.grp` de la carpeta, con buscador. Los grupos (`.grp`) salen arriba y marcados.
- Pantalla de centralita: lo que harías en un script INPA estándar. Info, Ident, leer y borrar la memoria de errores, Status, Steuern y una lista de todos los jobs al estilo Tool32.
- Ajustes: tipo de adaptador, datos de conexión y carpeta de archivos.

Como en INPA, abajo hay una barra con las teclas F1 a F10 y un botón Shift que cambia las etiquetas a Shift+F1…F10. Arriba, en todas las pantallas, hay dos indicadores: Batería y Encendido.

## Qué necesitas

- Un móvil o tablet Android.
- Un cable o adaptador de diagnóstico. Valen estos:
  - Cable K+DCAN USB (el típico de INPA para la serie E). Tiene que llevar chip FTDI y necesitas un adaptador OTG para enchufarlo al móvil. Los clones con otros chips (CH340, por ejemplo) no van a funcionar.
  - Cable ENET (serie F y G). Se conecta por WiFi, si tienes un adaptador ENET WiFi, o con un adaptador USB-Ethernet en el móvil.
  - Adaptador ELM327 por Bluetooth o WiFi. Es la opción más limitada: con centralitas D-CAN puede servir, pero con los BMW antiguos que usan DS2 o línea K casi nada funciona. Si tu coche es de esa época, usa un cable K+DCAN.
- Tus archivos `.prg` y `.grp` de EDIABAS, los mismos que tienes en `C:\EDIABAS\Ecu` en el PC.

## Compilar la APK

En este ordenador ya está todo instalado:

- .NET 10 en `~/.dotnet`, con el workload de Android
- JDK 21 en `/usr/lib/jvm/java-21-openjdk-amd64`
- Android SDK en `~/Android/Sdk`

Desde la carpeta `InpaDroid` ejecuta:

```sh
./build.sh
```

La APK queda en `out/InpaDroid.apk`.

Para instalarla en el móvil tienes dos opciones:

1. Copiar `InpaDroid.apk` al móvil (por cable, Telegram, correo o como te venga bien), abrirla desde el gestor de archivos y aceptar la instalación. Android te pedirá permitir la instalación de apps de origen desconocido para esa aplicación; dale permiso y vuelve atrás.
2. Con depuración USB activada en el móvil (Opciones de desarrollador) y el móvil conectado al PC:

   ```sh
   adb install -r out/InpaDroid.apk
   ```

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
                    ├── D_EGS.grp
                    ├── ...
                    ├── MS450DS0.prg
                    ├── UTILITY.prg
                    └── ...
```

La ruta completa suele ser `/storage/emulated/0/Android/data/com.inpadroid.app/files/Ecu/`. La carpeta `com.inpadroid.app` aparece después de abrir la app por primera vez. Si `Ecu` no existe, créala.

Para copiarlos:

1. Abre InpaDroid una vez y ciérrala.
2. Conecta el móvil al PC por USB y elige "Transferencia de archivos" en la notificación del móvil.
3. En el PC, entra en el móvil y ve a `Android/data/com.inpadroid.app/files/`.
4. Copia ahí dentro el contenido de tu `C:\EDIABAS\Ecu` (los `.prg` y `.grp` sueltos dentro de `Ecu`, sin subcarpetas).

En Android 11 y posteriores muchos gestores de archivos del propio móvil no dejan entrar en `Android/data`. Desde el PC por cable normalmente sí se puede. Si no te deja, usa otra carpeta y cámbiala en Ajustes.

Para usar otra carpeta, entra en Ajustes y cambia la carpeta ECU: puedes escribir la ruta a mano o usar el botón para elegirla.

Para comprobar que los ha encontrado, mira en Ajustes: debajo de la carpeta aparece cuántos `.prg` y `.grp` ha detectado. Si sale cero, la ruta está mal o los archivos están en una subcarpeta.

Qué archivos importan:

- `.prg` y `.grp`: sí, son los que usa la app. Copia todos los que tengas.
- `UTILITY.prg`: conviene que esté, porque los indicadores de batería y encendido salen de él.
- `.ipo` y `.ips` (los scripts de INPA, en `INPA\SGDAT`): esta versión no los usa. No pasa nada si los copias, pero los ignora.
- `.exe`, `.dll` y el resto de archivos de Windows: no sirven en Android, no hace falta copiarlos.

## Configurar el adaptador

Todo se hace en Ajustes. Elige el tipo de adaptador y rellena lo que corresponda.

### Cable K+DCAN USB

No hay nada más que configurar. Conecta el cable al móvil con el adaptador OTG y al coche. La primera vez Android pregunta si InpaDroid puede usar el dispositivo USB: acepta (y marca que lo recuerde, si te lo ofrece).

### ENET

En host ENET deja `auto` para que la app busque el coche por la red. Si no lo encuentra, escribe la IP del coche o del adaptador ENET WiFi. Con WiFi, conecta antes el móvil a la red del adaptador; con USB-Ethernet, enchufa el adaptador al móvil y el cable al coche.

### ELM327 Bluetooth

Empareja primero el ELM327 desde los ajustes de Bluetooth de Android (el PIN suele ser 1234 o 0000). Después, en Ajustes de InpaDroid, elige el adaptador de la lista de dispositivos emparejados. En Android 12 o posterior la app pide permiso de "Dispositivos cercanos" para usar Bluetooth; hay que dárselo.

### ELM327 WiFi

Conecta el móvil a la red WiFi del ELM327 y escribe su dirección en host ELM WiFi. Por defecto es `192.168.0.10:35000`, que es la de la mayoría de estos adaptadores. Si el tuyo usa otra, la verás en sus instrucciones.

## Uso paso a paso

1. Conecta el adaptador al coche y pon el contacto.
2. Abre InpaDroid. En la cabecera, Batería se pone en verde cuando la app lee tensión en el conector, y Encendido cuando detecta el contacto puesto. Se refrescan cada dos segundos más o menos mientras hay conexión. Si se quedan en gris, revisa el cable, el contacto y que tengas `UTILITY.prg`.
3. En la pantalla de inicio tienes:

   | Tecla | Función |
   |---|---|
   | F1 | Info |
   | F2 | Selección de centralita |
   | F3 | Identificar vehículo (si tienes los archivos necesarios, como `FA` o `UTILITY`) |
   | F9 | Ajustes |
   | F10 | Salir |

4. Pulsa F2 y busca la centralita. Puedes escribir parte del nombre en el buscador (por ejemplo `MOTOR` o `EGS`). Si eliges un `.grp`, la app averigua qué `.prg` concreto lleva tu coche, igual que hace INPA.
5. Dentro de la centralita, las teclas son las de un script INPA estándar:

   | Tecla | Función | Job que ejecuta |
   |---|---|---|
   | F1 | Info | `INFO` |
   | F2 | Ident | `IDENT` |
   | F4 | Leer memoria de errores | `FS_LESEN` |
   | Shift+F4 | Borrar memoria de errores (pide confirmación) | `FS_LOESCHEN` |
   | F5 | Status | lista de jobs `STATUS_*` |
   | F6 | Steuern | lista de jobs `STEUERN_*` |
   | F7 | Jobs | todos los jobs del `.prg` |
   | F10 | Volver | |

   Para Shift+F4, pulsa primero Shift y luego F4.

Los resultados salen en una lista, un bloque por cada set de resultados, con líneas del tipo `NOMBRE: valor`. En la memoria de errores se resaltan el código (`F_ORT_NR`) y el texto (`F_ORT_TEXT`) de cada fallo.

En F5 Status eliges un job de la lista y la app lo repite más o menos cada segundo, así ves los valores en directo. Deja de leer cuando sales de esa pantalla.

En F6 Steuern eliges un job, escribes los argumentos si los pide, y la app enseña un aviso y pide confirmación antes de mandarlo.

F7 Jobs funciona como Tool32: eliges cualquier job, escribes los argumentos separados por `;` y lo ejecutas. La lista de jobs, con sus argumentos y resultados, se lee directamente del `.prg`, así que funciona sin coche conectado.

## BMW E39

Esta parte es para el E39 diésel: 520d con motor M47, o 525d y 530d con M57.

### Cable

En el E39 solo sirve el cable K+DCAN USB con chip FTDI. El coche va por línea K, y con eso ni el ELM327 ni el ENET funcionan. El cable lleva un interruptor (o un puente) entre los pines 7 y 8 del conector OBD: tiene que estar en la posición "old", con los dos pines unidos. Si lo dejas en la posición de los coches nuevos, no conecta.

Al móvil se enchufa con un adaptador OTG.

Los E39 fabricados antes de septiembre de 2000 pueden no tener el conector OBD de 16 pines conectado a todas las centralitas. En esos coches a veces hace falta el adaptador al conector redondo de 20 pines que hay bajo el capó.

Mientras diagnosticas, el contacto tiene que estar puesto (llave en posición 2) con el motor parado. Si vas a estar mucho rato, conecta un cargador de batería: con el contacto puesto la batería baja rápido, y una tensión baja da errores raros en las centralitas.

### Archivos

Del paquete `daten E39 v70` solo hace falta la carpeta `ecu`. Copia todo su contenido (todos los `.prg` y `.grp`, sueltos, sin subcarpetas) en la carpeta `Ecu` de la app, como se explica en [Copiar los archivos de BMW al móvil](#copiar-los-archivos-de-bmw-al-móvil). Son unos 700 `.prg` y 241 `.grp`.

El resto de carpetas (`sgdat` con los `.ipo`, `daten`, `data`, `work`, `cfgdat` y demás) no hace falta en el móvil.

En el proyecto, los datos del E39 están en `datos_bmw/e39/`. Esa carpeta no entra en git ni en la APK, igual que el resto de `datos_bmw/`.

### Menú Vehículo E39

En la pantalla de inicio hay una entrada nueva, Vehículo BMW E39, que se abre con F4. Al abrirla sale la lista de centralitas del E39, con la del motor diésel (DDE) la primera.

Dentro de cada centralita las teclas siguen la misma idea que la pantalla genérica:

- Ident, para leer la identificación de la centralita.
- Leer la memoria de errores, y borrarla (pide confirmación).
- Páginas de status, con valores que se refrescan en directo.
- Tests de actuadores (Steuern). Antes de lanzar cada uno sale un aviso y hay que confirmar.
- F7 para ver todos los jobs del `.prg` y lanzar cualquiera, como en Tool32.

Las etiquetas exactas de cada tecla se ven en la barra de abajo de la pantalla.

### Primera prueba en el coche

1. Instala la APK en el móvil.
2. Copia el contenido de `ecu` a la carpeta `Ecu` de la app y comprueba en Ajustes que detecta los `.prg` y `.grp`.
3. Pon el interruptor del cable en "old" y conéctalo al coche y al móvil con el OTG.
4. En Ajustes, elige como adaptador "Cable USB K+DCAN (FTDI)" y pulsa Detectar cable. Cuando Android pregunte si InpaDroid puede usar el dispositivo USB, acepta.
5. Pon el contacto (posición 2).
6. Vuelve a la pantalla de inicio y espera a que Batería y Encendido se pongan en verde.
7. Pulsa F4 (Vehículo BMW E39), entra en DDE y pulsa Ident.

Si Ident devuelve datos de la centralita, la conexión funciona y puedes seguir con la memoria de errores y las páginas de status.

Si algo falla, la app escribe el error en rojo. Para pedir ayuda o abrir una incidencia, copia ese texto tal cual o haz una captura de pantalla, y di en qué paso te has quedado, qué centralita estabas abriendo y si Batería y Encendido estaban en verde.

## Problemas frecuentes

### No conecta

Comprueba que el contacto está puesto y que el tipo de adaptador de Ajustes es el que tienes enchufado. Con el cable USB, mira que sea FTDI, que el adaptador OTG funcione (prueba con un pendrive) y que aceptaste el permiso USB. Con ENET, que el móvil esté en la red correcta; si `auto` no lo encuentra, pon la IP a mano. Con ELM327, ten en cuenta que en coches con DS2 o línea K no va a funcionar bien. El error sale escrito en pantalla; si no lo entiendes, apúntalo tal cual.

### No aparecen las centralitas

Ve a Ajustes y mira cuántos `.prg`/`.grp` ha encontrado. Si son cero, los archivos no están en la carpeta configurada o están dentro de una subcarpeta. Tienen que estar sueltos dentro de `Ecu`.

### El job no existe

No todas las centralitas tienen `INFO`, `IDENT`, `FS_LESEN` y demás, y a veces se llaman distinto. Entra en F7 Jobs para ver qué jobs tiene de verdad ese `.prg` y lánzalo desde ahí. Si estás usando un `.grp` y falla, prueba a abrir directamente el `.prg` de tu centralita.

### Permisos de Bluetooth o USB

Si denegaste un permiso, la app no puede volver a pedirlo sola. Ve a Ajustes de Android, Aplicaciones, InpaDroid, Permisos, y activa "Dispositivos cercanos" (Bluetooth). Para USB, desenchufa y vuelve a enchufar el cable: Android vuelve a preguntar.

## Avisos

Las funciones Steuern activan componentes de verdad (actuadores, válvulas, relés). Un argumento mal puesto o un job lanzado en mal momento puede dejar una centralita inservible o causar daños. Úsalas solo si sabes lo que hace ese job, con el coche parado y la batería en buen estado. Lo mismo vale para los jobs que lances desde F7.

ediabaslib tiene licencia GPLv3. Si repartes la APK a otras personas, tienes que publicar también el código fuente de la app.

Los archivos de BMW (`.prg`, `.grp`, `.ipo`, INPA, EDIABAS) son propiedad de BMW. No se incluyen en la app ni en este repositorio, y no se deben redistribuir. La carpeta `datos_bmw/` del proyecto está fuera del control de versiones por ese motivo.

## Próximos pasos

Lo siguiente es probar el menú E39 en el coche y ajustar las pantallas con lo que salga. Los datos del E39 traen los `.ipo` (los scripts compilados de INPA) pero no los `.ips` (el código fuente), así que las pantallas se rehacen con los nombres y textos que se pueden leer de los `.ipo` y con los jobs de cada `.prg`. El plan detallado está en [PROXIMA_SESION.md](PROXIMA_SESION.md).
