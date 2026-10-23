# Third-party notices

InpaDroid is distributed under the GNU GPL v3 (see [LICENSE](LICENSE)). It includes or depends on the following components.

## ediabaslib

EdiabasLib, from the ediabaslib project, vendored under `external/ediabaslib/` (project references to `EdiabasLib` and `UsbSerialNetBinding`). Licensed under the GNU GPL v3. Its license and notices are kept in that directory.

## usb-serial-for-android

USB serial driver for FTDI (K+DCAN) cables, from [mik3y/usb-serial-for-android](https://github.com/mik3y/usb-serial-for-android). It ships in the APK as the prebuilt `usbSerialForAndroid-release.aar` wrapped by `external/ediabaslib/EdiabasLib/AndroidNetBindings/UsbSerialNetBinding`. Licensed under the MIT License (the copyright line must match the upstream LICENSE.txt of the version bundled in the .aar):

```text
MIT License

Copyright (c) the usb-serial-for-android authors (see LICENSE.txt in the upstream repository)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

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
