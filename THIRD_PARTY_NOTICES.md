# Third-party notices

InpaDroid is distributed under the GNU GPL v3 (see [LICENSE](LICENSE)). It includes or depends on the following components.

## ediabaslib

EdiabasLib, from the ediabaslib project, vendored under `external/ediabaslib/` (project references to `EdiabasLib` and `UsbSerialNetBinding`). Licensed under the GNU GPL v3. Its license and notices are kept in that directory.

## NuGet packages

Versions as declared in `src/InpaDroid/InpaDroid.csproj`. Check each package page on nuget.org for its license text.

| Package | Version |
|---|---|
| BouncyCastle.Cryptography | 2.7.0 |
| InTheHand.AndroidActivity | 1.0.5 |
| InTheHand.Net.Bluetooth | 4.2.5 |
| Newtonsoft.Json | 13.0.4 |
| SharpZipLib | 1.4.2 |

The unit-test project (`tests/InpaDroid.Tests`) additionally uses Microsoft.NET.Test.Sdk, xunit and xunit.runner.visualstudio; none of them ships in the APK.

## Not included

The BMW `.prg`/`.grp` files and the INPA data are proprietary and are not part of this repository or of the APK.
