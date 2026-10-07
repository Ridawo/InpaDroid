#!/usr/bin/env bash
# Builds the Release APK (signed with the debug key) and copies it to out/InpaDroid.apk.
set -euo pipefail

# Defaults for this machine; already-set variables win.
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export JAVA_HOME="${JAVA_HOME:-/usr/lib/jvm/java-21-openjdk-amd64}"
export ANDROID_HOME="${ANDROID_HOME:-$HOME/Android/Sdk}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJ="$ROOT/src/InpaDroid/InpaDroid.csproj"
TFM=net10.0-android36.1

# EnableAndroidTargets / TargetFrameworks for EdiabasLib come from src/InpaDroid/Directory.Build.rsp.
# Signing uses the Android debug key (~/.android/debug.keystore). Replace with a release key for distribution.
DEBUG_KEYSTORE="$HOME/.android/debug.keystore"
if [ ! -f "$DEBUG_KEYSTORE" ]; then
  mkdir -p "$(dirname "$DEBUG_KEYSTORE")"
  "$JAVA_HOME/bin/keytool" -genkeypair -keystore "$DEBUG_KEYSTORE" -storepass android -keypass android \
    -alias androiddebugkey -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Android Debug,O=Android,C=US"
fi
dotnet publish "$PROJ" -c Release -f "$TFM" \
  -p:AndroidSdkDirectory="$ANDROID_HOME" \
  -p:JavaSdkDirectory="$JAVA_HOME" \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore="$DEBUG_KEYSTORE" \
  -p:AndroidSigningKeyAlias=androiddebugkey \
  -p:AndroidSigningKeyPass=android \
  -p:AndroidSigningStorePass=android \
  "$@"

APK="$ROOT/src/InpaDroid/bin/Release/$TFM/publish/com.inpadroid.app-Signed.apk"
mkdir -p "$ROOT/out"
cp "$APK" "$ROOT/out/InpaDroid.apk"
echo "APK: $ROOT/out/InpaDroid.apk"
