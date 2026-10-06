#!/usr/bin/env bash
# Builds the Release APK (signed with the debug key) and copies it to out/InpaDroid.apk.
set -euo pipefail

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
export JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64
export ANDROID_HOME="$HOME/Android/Sdk"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJ="$ROOT/src/InpaDroid/InpaDroid.csproj"
TFM=net10.0-android36.1

# EnableAndroidTargets / TargetFrameworks for EdiabasLib come from src/InpaDroid/Directory.Build.rsp.
dotnet publish "$PROJ" -c Release -f "$TFM" \
  -p:AndroidSdkDirectory="$ANDROID_HOME" \
  -p:JavaSdkDirectory="$JAVA_HOME" \
  "$@"

APK="$ROOT/src/InpaDroid/bin/Release/$TFM/publish/com.inpadroid.app-Signed.apk"
mkdir -p "$ROOT/out"
cp "$APK" "$ROOT/out/InpaDroid.apk"
echo "APK: $ROOT/out/InpaDroid.apk"
