# Releasing

Releases are built by `.github/workflows/release.yml` when a tag starting with `v` is pushed.

```sh
git tag v0.1.0
git push origin v0.1.0
```

The workflow builds the Release APK, signs it and attaches `InpaDroid-<tag>.apk` and its checksum `InpaDroid-<tag>.apk.sha256` to a GitHub Release with auto-generated notes. Verify a download with `sha256sum -c InpaDroid-<tag>.apk.sha256`.

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

## SBOM (TODO)

The release does not publish a software bill of materials yet. A CycloneDX or SPDX step would have to use an action pinned by full commit SHA (like the rest of the workflow) or `dotnet CycloneDX` from a pinned tool version. It is not added until a verified SHA is available. Meanwhile, the dependencies are listed in [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md).
