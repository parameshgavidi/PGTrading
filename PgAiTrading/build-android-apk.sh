#!/usr/bin/env bash
# Build a sideloadable Android APK (arm64) for PG AI Trading.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

: "${ANDROID_HOME:=${ANDROID_SDK_ROOT:-$HOME/Android/Sdk}}"
export ANDROID_HOME
export ANDROID_SDK_ROOT="$ANDROID_HOME"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK not found. Install .NET 10 SDK first." >&2
  exit 1
fi

if [[ ! -d "$ANDROID_HOME/platforms" ]]; then
  echo "Android SDK not found at ANDROID_HOME=$ANDROID_HOME" >&2
  exit 1
fi

KEYSTORE="${ANDROID_SIGNING_KEYSTORE:-$ROOT/android-signing.keystore}"
ALIAS="${ANDROID_SIGNING_KEY_ALIAS:-pgaitrading}"
STORE_PASS="${ANDROID_SIGNING_STORE_PASS:-pgaitrading}"
KEY_PASS="${ANDROID_SIGNING_KEY_PASS:-pgaitrading}"

if [[ ! -f "$KEYSTORE" ]]; then
  echo "Creating local signing keystore at $KEYSTORE"
  keytool -genkeypair -v \
    -keystore "$KEYSTORE" \
    -alias "$ALIAS" \
    -keyalg RSA -keysize 2048 -validity 10000 \
    -storepass "$STORE_PASS" -keypass "$KEY_PASS" \
    -dname "CN=PG AI Trading, OU=Dev, O=PG, L=Unknown, ST=Unknown, C=IN"
fi

dotnet workload install maui-android --skip-manifest-update >/dev/null

dotnet publish -f net10.0-android -c Release -r android-arm64 \
  -p:TargetFrameworks=net10.0-android \
  -p:AndroidPackageFormat=apk \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore="$KEYSTORE" \
  -p:AndroidSigningKeyAlias="$ALIAS" \
  -p:AndroidSigningKeyPass="$KEY_PASS" \
  -p:AndroidSigningStorePass="$STORE_PASS"

OUT="$ROOT/bin/Release/net10.0-android/android-arm64/publish/com.pgaitrading.trading-Signed.apk"
echo "APK: $OUT"
ls -lah "$OUT"
