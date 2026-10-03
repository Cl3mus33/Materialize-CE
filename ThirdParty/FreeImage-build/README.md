# FreeImage for Materialize CE

`Assets/Plugins/FreeImage.dll` is FreeImage 3.18.0 built with vcpkg:

- vcpkg port `freeimage` 3.18.0#27, which builds FreeImage against current, maintained image libraries
  (libpng, libjpeg-turbo, libtiff, OpenEXR, OpenJPEG, libwebp, LibRaw, zlib) instead of the old copies FreeImage ships;
- plus Debian's security fixes of FreeImage's own code (`overlay-ports/freeimage`):
  CVE-2019-12211/12213 (TIFF), r1830, r1832 and r1836 (BMP), r1848 (PFM), r1877 (DDS).
  The last four are applied to `secure/*.cpp` (their mixed line endings stop the patches from applying as such);
- triplet `x64-windows-fis`: FreeImage as one DLL, the libraries and the C++ runtime linked inside,
  so it depends only on Windows (KERNEL32, WS2_32).

Rebuild:

    vcpkg install freeimage:x64-windows-fis --overlay-triplets=triplets --overlay-ports=overlay-ports

then copy `installed/x64-windows-fis/bin/FreeImage.dll` to `Assets/Plugins/`.
Check with `Assets/Editor/FormatTest.cs` and `DdsReadTest.cs` (-executeMethod FormatTest.Run / DdsReadTest.Run).
Licenses: `Assets/StreamingAssets/Licenses/FreeImage - *.txt`.
