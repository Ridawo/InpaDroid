#!/usr/bin/env bash
# Arranca el móvil virtual, instala la APK, copia los .prg del E39 y abre InpaDroid. Sin coche: sirve para ver
# las pantallas y leer los jobs de los .prg, pero cualquier job que hable con el coche dará EDIABAS_IFH_0018.
set -e
cd "$(dirname "$0")"
SDK="$HOME/Android/Sdk"
ADB="$SDK/platform-tools/adb"
ECU_MOVIL=/sdcard/Android/data/com.inpadroid.app/files/Ecu

if ! "$ADB" get-state >/dev/null 2>&1; then
    echo "Arrancando el emulador..."
    nohup "$SDK/emulator/emulator" -avd inpatest -no-audio -gpu auto >/dev/null 2>&1 &
    "$ADB" wait-for-device
    until [ "$("$ADB" shell getprop sys.boot_completed | tr -d '\r')" = 1 ]; do sleep 3; done
fi

echo "Instalando la APK..."
"$ADB" install -r out/InpaDroid.apk

if [ "$("$ADB" shell ls "$ECU_MOVIL" 2>/dev/null | wc -l)" -eq 0 ]; then
    echo "Copiando los .prg/.grp del E39..."
    "$ADB" shell mkdir -p "$ECU_MOVIL"
    "$ADB" push datos_bmw/e39/ecu/. "$ECU_MOVIL/"
fi

echo "Abriendo InpaDroid..."
"$ADB" shell monkey -p com.inpadroid.app -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
