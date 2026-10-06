#!/usr/bin/env bash
# Run only inside the digest-pinned BtbN dependency image (see rebuild.ps1).
set -euo pipefail
export LC_ALL=C.UTF-8
readonly source_sha=2a571b606854520cf89804d8030c8b328e621689
readonly source_zip_sha=25c3b714ebbb3d43ffd2a5126d8ce34217dade0d8cd4829c618666e3e19b3858
readonly dependency_recipe=9acad4a9ef1583096af7836cc1e9c8cbcb4d3950
readonly prefix=/opt/ffbuild
readonly output=/out
readonly work=/build-lgpl
mkdir -p "$output" "$work"
exec > >(tee "$output/build.log") 2>&1
printf '%s  %s\n' "$source_zip_sha" /input/ffmpeg.zip | sha256sum --check
[[ "$FFBUILD_PREFIX" == "$prefix" ]] || { echo 'Unexpected dependency prefix'; exit 1; }

# Cached upstream LGPL images include unused GPL dependency archives. Remove them
# before configuring so an accidental transitive reference fails at link time.
find "$prefix" -type f \( -iname '*fftw*' -o -iname '*chromaprint*' -o -iname '*lcms2_fast_float*' -o -iname '*lcms2_threaded*' \) -print -exec sha256sum {} \; >> "$output/removed-gpl-components.txt"
find "$prefix" -type f \( -iname '*fftw*' -o -iname '*chromaprint*' -o -iname '*lcms2_fast_float*' -o -iname '*lcms2_threaded*' \) -delete
find "$prefix/lib/pkgconfig" "$prefix/share/pkgconfig" -type f -name '*.pc' -exec sed -i -E 's/-llcms2_fast_float\b//g; s/-llcms2_threaded\b//g' {} +
if grep -R -n -i -E 'fftw|chromaprint|lcms2_(fast_float|threaded)' "$prefix/lib/pkgconfig" "$prefix/share/pkgconfig"; then
    echo 'A forbidden static dependency remains in pkg-config metadata'; exit 1
fi

if [[ ! -d "$work/FFmpeg-$source_sha" ]]; then
    unzip -q /input/ffmpeg.zip -d "$work"
fi
cd "$work/FFmpeg-$source_sha"
# The official source snapshot has no .git directory; retain its upstream build identity.
printf 'n9.0.2-17-g2a571b6068\n' > VERSION
printf '%s\n' "$FF_CONFIGURE" > "$output/upstream-configure.txt"
printf 'source=%s\nrecipes=%s\n' "$source_sha" "$dependency_recipe" > "$output/provenance.txt"
"$CC" --version >> "$output/provenance.txt"
"$CC" -print-libgcc-file-name >> "$output/provenance.txt"
find "$prefix/lib" -maxdepth 1 -type f -name '*.a' -print0 | sort -z | xargs -0 sha256sum > "$output/dependency-archive-sha256.txt"
pkg-config --list-all | sort > "$output/dependency-pkg-config.txt"

# Keep the upstream feature set, apart from unused fingerprinting and the new
# runtime-loaded VapourSynth integration. GPL/nonfree are explicitly forbidden.
# GNU ld traces every archive used by every shared DLL into the build log.
read -r -a target_flags <<< "$FFBUILD_TARGET_FLAGS"
read -r -a upstream_flags <<< "$FF_CONFIGURE"
configure_flags=()
for flag in "${upstream_flags[@]}"; do
    case "$flag" in
        --enable-chromaprint|--disable-chromaprint|--enable-vapoursynth|--disable-vapoursynth|--enable-gpl|--disable-gpl|--enable-nonfree|--disable-nonfree) ;;
        *) configure_flags+=("$flag") ;;
    esac
done
./configure --prefix="$work/prefix" --pkg-config-flags=--static \
    "${target_flags[@]}" "${configure_flags[@]}" \
    --disable-chromaprint --disable-vapoursynth --disable-gpl --disable-nonfree \
    --extra-cflags="$FF_CFLAGS" --extra-cxxflags="$FF_CXXFLAGS" --extra-libs="$FF_LIBS" \
    --extra-ldflags="$FF_LDFLAGS -Wl,--trace" --extra-ldexeflags="$FF_LDEXEFLAGS" \
    --cc="$CC" --cxx="$CXX" --ar="$AR" --ranlib="$RANLIB" --nm="$NM" \
    --extra-version=20261006-lgpl-no-gpl-deps
cp config.h ffbuild/config.mak ffbuild/config.log "$output/"
for feature in GPL NONFREE CHROMAPRINT VAPOURSYNTH; do
    grep -q "^#define CONFIG_${feature} 0$" config.h || { echo "Forbidden feature: $feature"; exit 1; }
done
# An interrupted compiler may leave an empty object that make treats as current.
# Remove only those generated files before an incremental retry.
find . -type f -name '*.o' -size 0 -print -delete >> "$output/recovered-empty-objects.txt"
# Relink every shared library so this run records its complete archive trace.
find libavutil libswresample libswscale libavcodec libavformat libavfilter libavdevice -maxdepth 1 -type f -name '*.dll' -delete
make -j"${BUILD_JOBS:-8}" V=1
make install install-doc
mkdir -p "$output/bin" "$output/include" "$output/lib" "$output/doc"
cp "$work/prefix/bin/"*.dll "$output/bin/"
cp "$work/prefix/bin/"*.exe "$output/bin/"
cp -a "$work/prefix/include/." "$output/include/"
cp -a "$work/prefix/lib/." "$output/lib/"
cp -a "$work/prefix/share/doc/." "$output/doc/"
cp COPYING.LGPLv2.1 COPYING.LGPLv3 COPYING.GPLv2 COPYING.GPLv3 LICENSE.md "$output/"
sha256sum "$output/bin/"*.dll > "$output/dll-sha256.txt"
for dll in "$output/bin/"*.dll; do
    "$FFBUILD_TOOLCHAIN-objdump" -p "$dll" | grep 'DLL Name:' >> "$output/dll-imports.txt"
    if strings "$dll" | grep -i -E 'fftw_plan_|fftw_execute|fftw_malloc|chromaprint_new|lcms2_fast_float|lcms2_threaded'; then
        echo "Forbidden embedded library marker in $dll"; exit 1
    fi
done
printf 'SUCCESS: rebuilt pinned FFmpeg without Chromaprint, FFTW, or LCMS GPL plugins.\n' > "$output/SUCCESS.txt"
