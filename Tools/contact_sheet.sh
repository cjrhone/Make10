#!/usr/bin/env bash
# Stitch ResolutionContactSheet captures into one labelled sheet.
# Usage: Tools/contact_sheet.sh <shots_dir> [out.png] [tile_height_px]
# Requires ImageMagick (brew install imagemagick). Rows = devices, columns = menu | game | results.
set -euo pipefail
DIR="${1:?shots dir}"; OUT="${2:-$DIR/contact_sheet.png}"; H="${3:-560}"
FONT="${CONTACT_SHEET_FONT:-/System/Library/Fonts/Supplemental/Arial.ttf}"
TMP="$(mktemp -d)"
i=0
for menu in "$DIR"/*__menu.png; do
  base="${menu%__menu.png}"; name="$(basename "$base")"
  label="${name#*_}"; label="${label/_/  }"          # "iPhone15  1179x2556"
  row="$TMP/row_$(printf %02d $i).png"
  magick "$menu" "${base}__game.png" "${base}__results.png" \
    -resize "x$H" -bordercolor '#222' -border 6 +append \
    -background '#111' -fill '#eee' -font "$FONT" -pointsize 26 -gravity west \
    -splice 0x40 -annotate +12+0 "$label" "$row"
  i=$((i+1))
done
magick "$TMP"/row_*.png -background '#111' -append \
  -gravity north -fill '#eee' -font "$FONT" -pointsize 30 -splice 0x50 \
  -annotate +0+10 "Make10 resolution sweep — menu | game | results   ($(date +%F))" "$OUT"
rm -rf "$TMP"
echo "$OUT"
