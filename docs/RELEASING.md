# Releasing

Releases are built by `.github/workflows/release.yml` when a tag starting with `v` is pushed.

```sh
git tag v0.1.0
git push origin v0.1.0
```

The workflow builds the Release APK, signs it and attaches `InpaDroid-<tag>.apk` to a GitHub Release with auto-generated notes.

## Signing secrets

Set these under Settings > Secrets and variables > Actions:

| Secret | Content |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | The release keystore file, base64-encoded |
| `ANDROID_KEYSTORE_PASSWORD` | Keystore password |
| `ANDROID_KEY_ALIAS` | Alias of the signing key |
| `ANDROID_KEY_PASSWORD` | Password of that key |

Create a keystore and encode it:

```sh
keytool -genkeypair -v -keystore release.keystore -alias inpadroid \
  -keyalg RSA -keysize 2048 -validity 10000
base64 -w0 release.keystore > release.keystore.b64   # paste the content into the secret
```

Keep `release.keystore` somewhere safe and out of the repo (`*.keystore` is git-ignored). If you lose it you cannot publish updates that install over existing ones.

## Missing secrets

If any of the four secrets is empty, the workflow prints a warning, signs with a throwaway debug key and marks the release as a pre-release. Such an APK is fine for testing but should not be distributed as an official build, and it cannot be upgraded over one signed with the real key.
