#!/usr/bin/env bash
# Regenerates the EyeBrowse test images. Requires ImageMagick 6 (convert) with
# PNG, JPEG, GIF, TIFF and WebP support, and the DejaVu Sans font.
#
#   ./TestImages/generate.sh
#
# Every image is a "test card": a coloured background with a 10% grid, a red
# border, a centre crosshair, labelled corners and a caption giving its name
# and pixel size, so scaling, panning and cropping are easy to judge by eye.
set -euo pipefail

ROOT=$(cd "$(dirname "$0")" && pwd)
FONT=DejaVu-Sans-Bold

# card <width> <height> <hue 0-360> <caption> <[FORMAT:]output> [extra convert args...]
card() {
    local w=$1 h=$2 hue=$3 caption=$4 out=$5
    shift 5
    local min=$(( w < h ? w : h ))
    local pt=$(( min / 10 > 6 ? min / 10 : 6 ))
    local cpt=$(( pt / 2 > 6 ? pt / 2 : 6 ))
    local line=$(( min / 200 > 1 ? min / 200 : 1 ))
    local border=$(( min / 60 > 1 ? min / 60 : 1 ))
    local cx=$(( w / 2 )) cy=$(( h / 2 )) arm=$(( min / 8 ))
    local grid=()
    for i in 1 2 3 4 5 6 7 8 9; do
        grid+=(-draw "line $(( w * i / 10 )),0 $(( w * i / 10 )),$h")
        grid+=(-draw "line 0,$(( h * i / 10 )) $w,$(( h * i / 10 ))")
    done

    mkdir -p "$(dirname "${out#*:}")"
    convert -size "${w}x${h}" "xc:hsl($hue,55%,35%)" \
        -stroke "hsl($hue,40%,55%)" -strokewidth "$line" "${grid[@]}" \
        -stroke white -strokewidth $(( line * 2 )) \
        -draw "line $(( cx - arm )),$cy $(( cx + arm )),$cy" \
        -draw "line $cx,$(( cy - arm )) $cx,$(( cy + arm ))" \
        -fill none -stroke red -strokewidth $(( border * 2 )) -draw "rectangle 0,0 $(( w - 1 )),$(( h - 1 ))" \
        -stroke none -fill white -font "$FONT" \
        -pointsize "$cpt" \
        -gravity NorthWest -annotate +$(( border * 3 ))+$(( border * 3 )) TL \
        -gravity NorthEast -annotate +$(( border * 3 ))+$(( border * 3 )) TR \
        -gravity SouthWest -annotate +$(( border * 3 ))+$(( border * 3 )) BL \
        -gravity SouthEast -annotate +$(( border * 3 ))+$(( border * 3 )) BR \
        -pointsize "$pt" -gravity Center -annotate +0-$(( pt * 6 / 10 )) "$caption" \
        -pointsize "$cpt" -annotate +0+$(( pt * 6 / 10 )) "${w} x ${h}" \
        "$@" "$out"
}

rm -rf "$ROOT"/{formats,sizes,many,ordering,edge,archives}

# formats/: the same 640x480 card in every supported format, plus upper-case
# extensions and a file that must be skipped.
F=$ROOT/formats
card 640 480 0   "PNG"            "$F/01-png.png"
card 640 480 30  "JPG"            "$F/02-jpg.jpg" -quality 85
card 640 480 60  "JPEG"           "$F/03-jpeg.jpeg" -quality 85
card 640 480 90  "BMP"            "BMP3:$F/04-bmp.bmp" -type Palette -compress RLE
card 640 480 120 "GIF"            "$F/05-gif.gif"
card 640 480 150 "TIF"            "$F/06-tif.tif" -compress LZW
card 640 480 180 "TIFF"           "$F/07-tiff.tiff" -compress LZW
card 640 480 210 "WEBP"           "$F/08-webp.webp" -quality 85
card 640 480 240 "PNG (.PNG)"     "$F/09-upper.PNG"
card 640 480 270 "JPG (.JPG)"     "$F/10-upper.JPG" -quality 85
card 640 480 300 "WEBP (.WebP)"   "$F/11-mixed.WebP" -quality 85
echo "Not an image. EyeBrowse should skip this file." > "$F/12-not-an-image.txt"

# sizes/: aspect ratios and sizes from tiny to far larger than any screen.
S=$ROOT/sizes
card 16   16   0   "16"           "$S/01-tiny-16x16.png"
card 160  120  30  "small"        "$S/02-small-160x120.png"
card 640  480  60  "VGA"          "$S/03-vga-640x480.png"
card 1280 720  90  "720p"         "$S/04-720p-1280x720.png"
card 1920 1080 120 "1080p"        "$S/05-1080p-1920x1080.png"
card 3840 2160 150 "4K"           "$S/06-4k-3840x2160.png"
card 8000 6000 180 "huge"         "$S/07-huge-8000x6000.png"
card 1200 1200 210 "square"       "$S/08-square-1200x1200.png"
card 6000 600  240 "panorama"     "$S/09-panorama-6000x600.png"
card 600  4000 270 "tall"         "$S/10-tall-600x4000.png"
card 1080 1920 300 "portrait"     "$S/11-portrait-1080x1920.png"
card 1    1    330 ""             "$S/12-single-pixel-1x1.png"

# many/: lots of small images to step through; the hue walks the colour wheel.
for i in $(seq 1 60); do
    n=$(printf '%03d' "$i")
    card 400 300 $(( i * 6 )) "$n / 060" "$ROOT/many/$n.png"
done

# ordering/: names that sort wrongly as plain text. The caption is the
# position each should have: numbers in natural order, case ignored.
O=$ROOT/ordering
card 400 300 0   "1 of 6"         "$O/page 1.png"
card 400 300 60  "2 of 6"         "$O/page 2.png"
card 400 300 120 "3 of 6"         "$O/Page 3.png"
card 400 300 180 "4 of 6"         "$O/page 10.png"
card 400 300 240 "5 of 6"         "$O/page 20.png"
card 400 300 300 "6 of 6"         "$O/zebra.png"

# edge/: awkward cases.
E=$ROOT/edge
card 640 480 0   "transparent"    "$E/transparent.png" \
    \( -size 640x480 xc:black -fill white -draw "circle 320,240 320,20" \) \
    -alpha off -compose CopyOpacity -composite
card 640 480 60  "frame 1 of 3"   "$E/animated-1.png"
card 640 480 180 "frame 2 of 3"   "$E/animated-2.png"
card 640 480 300 "frame 3 of 3"   "$E/animated-3.png"
convert -delay 70 -loop 0 "$E"/animated-{1,2,3}.png "$E/animated.gif"
rm "$E"/animated-{1,2,3}.png
# edge/exif/: one JPEG per EXIF orientation, stored transformed so that it is
# upright, and reads normally, only if that orientation is applied correctly.
exif_inverse=("" "" -flop "-rotate 180" -flip -transpose "-rotate -90" -transverse "-rotate 90")
for o in 1 2 3 4 5 6 7 8; do
    # shellcheck disable=SC2086 # the inverse transform is meant to split into words
    card 640 480 $(( o * 40 )) "EXIF $o"  "$E/exif/orientation-$o.jpg" ${exif_inverse[$o]} -quality 85
    # ImageMagick only updates an existing EXIF block, so insert one by hand: an
    # APP1 segment holding a single IFD entry, Orientation (0x0112).
    python3 - "$E/exif/orientation-$o.jpg" "$o" <<'PY'
import struct, sys
path, orientation = sys.argv[1], int(sys.argv[2])
data = open(path, 'rb').read()
tiff = b'II*\x00' + struct.pack('<I', 8) + struct.pack('<H', 1) \
     + struct.pack('<HHIHH', 0x0112, 3, 1, orientation, 0) + struct.pack('<I', 0)
app1 = b'Exif\x00\x00' + tiff
open(path, 'wb').write(data[:2] + b'\xff\xe1' + struct.pack('>H', len(app1) + 2) + app1 + data[2:])
PY
done
card 640 480 240 "spaces"         "$E/name with spaces.png"
card 640 480 330 "unicode"        "$E/ünïcödé 名前.png"
card 640 480 30  "alone"          "$E/alone/only-image.png"
# Truncated PNG, kept in its own folder so it can't break the other cases.
card 640 480 90  "broken"         "$E/broken/truncated.png"
head -c 2000 "$E/broken/truncated.png" > "$E/broken/truncated.tmp"
mv "$E/broken/truncated.tmp" "$E/broken/truncated.png"
card 640 480 150 "next to broken" "$E/broken/fine.png"

# archives/: zip and comic book (.cbz) archives, opened like an image. Pages are
# stored in reverse order, so they only appear in order if EyeBrowse sorts them.
A=$ROOT/archives
STAGE=$(mktemp -d)
for i in $(seq 1 12); do
    card 600 900 $(( i * 30 )) "Page $i of 12" "$STAGE/comic/page$i.jpg" -quality 85
done
n=0
for chapter in 1 2 10; do
    for p in 1 2 3; do
        n=$(( n + 1 ))
        card 600 900 $(( n * 40 )) "Ch $chapter p $p ($n/9)" "$STAGE/manga/Vol 1/Chapter $chapter/0$p.jpg" -quality 85
    done
done
card 640 480 0   "PNG in zip"     "$STAGE/mixed/1-png.png"
card 640 480 50  "JPEG in zip"    "$STAGE/mixed/2-jpeg.jpg" -quality 85
card 640 480 100 "BMP in zip"     "BMP3:$STAGE/mixed/3-bmp.bmp" -type Palette -compress RLE
card 640 480 150 "TIFF in zip"    "$STAGE/mixed/4-tiff.tif" -compress LZW
card 640 480 200 "WebP in zip"    "$STAGE/mixed/5-webp.webp" -quality 85
card 640 480 250 "PNG (.PNG)"     "$STAGE/mixed/6-UPPER.PNG"
cp "$ROOT/edge/animated.gif" "$STAGE/mixed/7-animated.gif"
card 640 480 300 "upper 1 of 2"   "$STAGE/upper/1.png"
card 640 480 330 "upper 2 of 2"   "$STAGE/upper/2.png"

python3 - "$STAGE" "$A" <<'PY'
import os, sys, zipfile
stage, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
STAMP = (2026, 1, 1, 0, 0, 0)  # fixed, so regenerating doesn't change the bytes

def build(name, entries, compression=zipfile.ZIP_STORED):
    """entries: (name in archive, bytes); written in the given order."""
    with zipfile.ZipFile(os.path.join(out, name), 'w') as z:
        for arcname, data in entries:
            info = zipfile.ZipInfo(arcname, STAMP)
            info.compress_type = compression
            z.writestr(info, data)

def files(folder):
    root = os.path.join(stage, folder)
    found = []
    for dirpath, _, names in os.walk(root):
        for n in names:
            path = os.path.join(dirpath, n)
            found.append((os.path.relpath(path, root).replace(os.sep, '/'), open(path, 'rb').read()))
    return sorted(found, reverse=True)  # reverse order on purpose

comic_info = b'<?xml version="1.0"?>\n<ComicInfo><Title>Test comic</Title></ComicInfo>\n'
build('comic.cbz', files('comic') + [('ComicInfo.xml', comic_info)])
build('manga.cbz', files('manga') + [
    ('__MACOSX/Vol 1/Chapter 1/._01.jpg', b'\x00\x05\x16\x07 macOS metadata, not an image'),
    ('ComicInfo.xml', comic_info)])
build('mixed-formats.zip', files('mixed'), zipfile.ZIP_DEFLATED)
build('UPPER-CASE.CBZ', files('upper'))
build('no-images.cbz', [('ComicInfo.xml', comic_info), ('readme.txt', b'No pages in here.\n')])
whole = open(os.path.join(out, 'comic.cbz'), 'rb').read()
open(os.path.join(out, 'broken.cbz'), 'wb').write(whole[:len(whole) // 3])
PY
rm -rf "$STAGE"

echo "Generated $(find "$ROOT" -type f ! -name generate.sh ! -name README.md | wc -l) files," \
     "$(du -sh --exclude=generate.sh "$ROOT" | cut -f1) total."
