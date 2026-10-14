#!/bin/bash
# README screenshots of the Uno (Linux) edition, 3 languages x 4 scenes, in ONE running app, only through the
# automation API: the language, the document (open this language's sample, close the others), the view (visual,
# source, outline), the theme and the window size - no restart.
#
#   APPDIR=<published Typedown.Uno folder with typedownctl> OUT=<folder> Tools/Screenshots/shots-linux.sh
# Needs Xvfb, xfwm4, xdotool, xprop, ImageMagick.
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
APPDIR=${APPDIR:?APPDIR: the published Typedown.Uno folder (packaging/add-cli.sh puts typedownctl in it)}
SAMPLES=$HERE/samples
OUT=${OUT:-$PWD/screenshots-linux}
WIDTH=${WIDTH:-1280} HEIGHT=${HEIGHT:-860}
mkdir -p "$OUT"
pkill -f "[X]vfb :62 " ; sleep 0.5
Xvfb :62 -screen 0 1440x960x24 >/dev/null 2>&1 & XV=$!
sleep 1; DISPLAY=:62 xfwm4 >/dev/null 2>&1 & WM=$!; sleep 1
export DISPLAY=:62 DOTNET_ROOT=/root/.dotnet
declare -A NAME=( [zh-Hans]="写作手记.md" [en]="Writing.md" [ja]="執筆メモ.md" )
T=/tmp/tdshot H=/home/demo; rm -rf $T $H; mkdir -p $T/data/Typedown.Uno $T/run $H/Documents; chmod 700 $T/run
for lang in zh-Hans en ja; do cp "$SAMPLES/$lang.md" "$H/Documents/${NAME[$lang]}"; done
printf '{ "Language": "en", "Theme": 1, "CustomTheme": "", "AllowLocalAutomation": true, "FileStartupAction": 0, "AlwaysShowTabBar": true }' > $T/data/Typedown.Uno/settings.json
HOME=$H XDG_DATA_HOME=$T/data XDG_RUNTIME_DIR=$T/run XDG_CONFIG_HOME=$H/.config "$APPDIR/Typedown.Uno" > $T/app.log 2>&1 &
app=$!; sock=$T/run/typedown/automation.v1.sock
ctl() { "$APPDIR/typedownctl" --json --endpoint $sock "$@"; }
for i in $(seq 1 60); do ctl status >/dev/null 2>&1 && break; sleep 1; done
win=$(ctl windows | python3 -c 'import json,sys; print(json.load(sys.stdin)["windows"][0]["windowId"])')
setting() { local r=$(ctl settings get $1 | python3 -c 'import json,sys; print(json.load(sys.stdin)["settingsRevision"])'); ctl settings set $1 "$2" --base-revision $r >/dev/null || echo "   setting $1 refused"; }
ctl view $win --bounds 0,0,$WIDTH,$HEIGHT >/dev/null
xw=$(xdotool search --onlyvisible --name " - Typedown" | tail -1)
shot() {
  local ext=($(xprop -id $xw _NET_FRAME_EXTENTS | sed 's/.*= //; s/,//g'))
  local geo=($(xdotool getwindowgeometry --shell $xw | sed -n 's/^WIDTH=//p; s/^HEIGHT=//p'))
  local fw=$((${geo[0]} + ${ext[0]:-0} + ${ext[1]:-0})) fh=$((${geo[1]} + ${ext[2]:-0} + ${ext[3]:-0}))
  import -window root -crop ${fw}x${fh}+0+0 +repage "$OUT/$1.png"
  echo "$1: $(identify -format '%wx%h' "$OUT/$1.png")"
}
for lang in zh-Hans en ja; do
  setting ui.language $lang
  id=$(ctl open "$H/Documents/${NAME[$lang]}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["documentId"])')
  # Only this language's document stays: the others (the startup blank, the previous sample) are closed.
  for other in $(ctl documents | python3 -c 'import json,sys; print(" ".join(d["documentId"] for d in json.load(sys.stdin)["documents"]))'); do
    [ $other != $id ] && { ctl close $other >/dev/null || echo "   could not close $other"; }
  done
  for i in 1 2 3 4 5; do ctl get $id --latest >/dev/null 2>&1 && break; sleep 2; done
  ctl view $win --mode visual --side-pane closed >/dev/null
  sleep 4   # maths and diagrams rendered, the connection marker gone from the title
  shot $lang-visual
  ctl view $win --mode source >/dev/null; sleep 1; shot $lang-source
  ctl view $win --mode visual --side-pane outline >/dev/null; sleep 1; shot $lang-outline
  ctl view $win --side-pane closed >/dev/null; setting appearance.theme '{"kind":"builtIn","id":"dark"}'; sleep 3; shot $lang-dark
  setting appearance.theme '{"kind":"builtIn","id":"light"}'; sleep 1
done
echo "one process for all: pid $app, still running: $(kill -0 $app 2>/dev/null && echo yes || echo no)"
kill $app 2>/dev/null; sleep 1; kill -9 $app 2>/dev/null; kill $WM $XV 2>/dev/null
