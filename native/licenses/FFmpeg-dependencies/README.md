# Corrected FFmpeg runtime dependency notices

These notice files preserve upstream bytes and component/member paths from the
fixed sources used for the corrected runtime. The inventory records archive and
file hashes, exact recipe/image identity and any collection issues. Full source
archives and build scripts are separate corresponding-source release assets.

Chromaprint, FFTW3, Rubber Band and VapourSynth are excluded from the corrected
runtime. The LittleCMS core library is MIT; its GPL fast_float/threaded plugin
libraries are removed before linking and are not represented as shipped plugins.
xxHash notices cover the BSD library, not its unrelated command-line program.
Final runtime configuration and linker-map checks determine actual linkage.

Source input caches are broader than the linked runtime. Included notice files
from an active dependency may describe optional source portions; their presence
does not assert those portions are linked. Cargo notices cover the supplied
resolved dependency sources including build/platform-specific inputs. Known
disabled runtime components, tool/test/example paths and LCMS plugin paths are
not collected. License alternatives and runtime exceptions remain in the full
upstream text. This index is provenance, not a legal certification.

The FFmpeg dependency stack uses the FreeType Project (https://freetype.org/)
under the FreeType License (FTL). Its full FTL text and expressly referenced
BDF/PCF/gzip/HarfBuzz copyright files accompany the root FreeType license notice.
The actual selected NVIDIA codec header notices are included (SDK alternatives
in the source cache are not used). GCC16.2 runtime COPYING3 and the GCC Runtime
Library Exception3.1 are included under gcc-runtime-16.2.0. OpenSSL's unused
integration-test/provider and installer submodule notices are omitted.
