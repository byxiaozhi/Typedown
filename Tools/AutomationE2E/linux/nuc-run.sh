#!/bin/bash
# Automation checks of the Uno edition on the NUC's real XFCE desktop (DISPLAY=:1), in an isolated profile: nothing of
# the installed Typedown (settings, session, its single-instance socket) is touched.
set -u
A=$(cd "$(dirname "$0")" && pwd)
T=/tmp/tdnuc
APP_PATTERN="[t]dauto/uno/Typedown.Uno"
pkill -f "$APP_PATTERN"; sleep 1
rm -rf $T && mkdir -p $T/home $T/data/Typedown.Uno $T/run $T/docs && chmod 700 $T/run
printf '{ "AllowLocalAutomation": true, "FileStartupAction": 0, "Language": "en" }' > $T/data/Typedown.Uno/settings.json
printf '# NUC\n\nThe quick brown fox.\n' > $T/docs/tdnuc-a.md
# The desktop's X authority stays the user's: only the Typedown profile is moved aside.
export XAUTHORITY=${XAUTHORITY:-$HOME/.Xauthority}
export HOME=$T/home XDG_DATA_HOME=$T/data XDG_RUNTIME_DIR=$T/run XDG_CONFIG_HOME=$T/home/.config DISPLAY=:1
SOCK=$T/run/typedown/automation.v1.sock
CTL="dotnet $A/cli/typedownctl.dll --json --endpoint $SOCK"
# Launched the way the installed /usr/bin/typedown launches it (its per-user links to the unversioned GTK libraries),
# with only the program swapped for this build.
sed "s|exec /opt/typedown/Typedown.Uno|exec $A/uno/Typedown.Uno|" /usr/bin/typedown > $T/launch.sh
start_app() {
  sh $T/launch.sh "$@" > $T/app.log 2>&1 &
  APP_PID=$!
  for i in $(seq 1 90); do $CTL status >/dev/null 2>&1 && return 0; sleep 1; done
  echo "FAIL the app never answered"; return 1
}
# This instance's own window (the desktop may show another Typedown): found by process, activated, then real keys.
# Found by the test document's unique name in the title (Uno sets no _NET_WM_PID on the main window); a click in the
# editor gives it the keyboard before the keys.
app_window() { for i in $(seq 1 20); do w=$(xdotool search --name "$1 - Typedown" 2>/dev/null | head -1); [ -n "$w" ] && { echo $w; return; }; sleep 0.5; done; }
key_to_app() {
  w=$(app_window "$1"); xdotool windowactivate --sync $w 2>/dev/null; sleep 0.5
  eval $(xdotool getwindowgeometry --shell $w); xdotool mousemove $((X + WIDTH / 2)) $((Y + HEIGHT / 2)) click 1; sleep 0.5
  xdotool key "$2"
}
text_of() { $CTL get $1 --latest --text | python3 -c 'import json,sys; sys.stdout.write(json.load(sys.stdin)["text"])'; }
ok=1; check() { if [ "$1" = 0 ]; then echo "PASS $2"; else echo "FAIL $2"; ok=0; fi; }

echo "== start"; start_app $T/docs/tdnuc-a.md; sleep 3
check $([ "$(stat -c %a $SOCK)" = 600 ]; echo $?) "the socket is 0600 ($(stat -c %a $SOCK))"

echo "== CLI"
ID=$($CTL documents | python3 -c 'import json,sys; print([d for d in json.load(sys.stdin)["documents"] if d["path"] and d["path"].endswith("tdnuc-a.md")][0]["documentId"])')
R=$($CTL get $ID --latest | python3 -c 'import json,sys; print(json.load(sys.stdin)["revision"])')
$CTL replace-text $ID --base-revision $R --find brown --replacement red --expected-count 1 --save >/dev/null; check $? "replace-text with save"
check $([ "$(cat $T/docs/tdnuc-a.md)" = "$(printf '# NUC\n\nThe quick red fox.')" ]; echo $?) "the file holds the write"
$CTL replace-text $ID --base-revision $R --find red --replacement blue --expected-count 1 >/dev/null 2>&1; check $([ $? = 6 ]; echo $?) "a stale revision exits 6"

echo "== MCP, then its connection cut"
python3 - "$A" "$SOCK" "$ID" <<'PY'
import json, subprocess, sys, time, os
A, sock, doc = sys.argv[1:4]
p = subprocess.Popen(["dotnet", f"{A}/cli/typedownctl.dll", "mcp", "--endpoint", sock], stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True, bufsize=1)
n = 0
def send(m, params):
    global n; n += 1
    p.stdin.write(json.dumps({"jsonrpc": "2.0", "id": n, "method": m, "params": params}) + "\n"); p.stdin.flush()
    return json.loads(p.stdout.readline())
send("initialize", {"protocolVersion": "2025-06-18", "clientInfo": {"name": "NUC agent"}})
read = send("tools/call", {"name": "typedown_read_document", "arguments": {"documentId": doc}})["result"]["structuredContent"]
w = send("tools/call", {"name": "typedown_replace_text", "arguments": {"documentId": doc, "baseRevision": read["revision"], "find": "quick", "replacement": "very quick", "reveal": True}})["result"]
print(("PASS" if not w["isError"] else "FAIL") + " MCP replace_text with reveal")
time.sleep(0.5)
title = subprocess.run("xdotool search --name 'Typedown' | xargs -I{} xprop -id {} _NET_WM_NAME", shell=True, capture_output=True, text=True).stdout
print(("PASS" if "NUC agent (MCP)" in title else "FAIL") + " the title names the agent: " + " | ".join(l.split("=", 1)[-1].strip() for l in title.splitlines()))
subprocess.run(["import", "-window", "root", "/tmp/tdnuc/mcp-write.png"])
p.kill()  # the agent host dies mid-session
PY
sleep 1
$CTL get $ID --latest >/dev/null; check $? "a new client is served after an agent's connection was cut"

echo "== mode switch (Ctrl+/) with writes in source mode"
key_to_app tdnuc-a.md ctrl+slash; sleep 2
check $(grep -q '"SourceCode": true' $T/data/Typedown.Uno/settings.json; echo $?) "Ctrl+/ switched to source mode"
R=$($CTL get $ID --latest | python3 -c 'import json,sys; print(json.load(sys.stdin)["revision"])')
$CTL replace-text $ID --base-revision $R --find fox --replacement cat --expected-count 1 --allow-unknown-normalization >/dev/null; check $? "a write in source mode"
TXT=$(text_of $ID); check $([ "$TXT" = "$(printf '# NUC\n\nThe very quick red cat.\n')" ]; echo $?) "read back exactly in source mode"
key_to_app tdnuc-a.md ctrl+slash; sleep 2
check $(grep -q '"SourceCode": false' $T/data/Typedown.Uno/settings.json; echo $?) "Ctrl+/ switched back"
TXT=$(text_of $ID); check $([ "$TXT" = "$(printf '# NUC\n\nThe very quick red cat.\n')" ]; echo $?) "and after switching back"

echo "== equivalence scenario (fresh app in visual mode)"
pkill -f "$APP_PATTERN"; sleep 2
python3 -c "import json; p='$T/data/Typedown.Uno/settings.json'; s=json.load(open(p)); s['SourceCode']=False; json.dump(s,open(p,'w'))"
start_app; sleep 3
python3 $A/scripts/scenario.py --examples $A/scripts --socket $SOCK --workdir $T/eq > $T/eq-nuc.json; check $? "the scenario ran"

echo "== Linux checks"
python3 $A/scripts/linux_checks.py --examples $A/scripts --socket $SOCK --workdir $T/lx

echo "== the installed typedownctl: view and settings (no restart)"
if [ -x /usr/bin/typedownctl ]; then
  ICTL="/usr/bin/typedownctl --json --endpoint $SOCK"
  $ICTL status >/dev/null; check $? "the installed typedownctl answers"
  W=$($ICTL windows | python3 -c 'import json,sys; print(json.load(sys.stdin)["windows"][0]["windowId"])')
  V=$($ICTL view $W --mode source --side-pane outline --size 1100x700)
  check $(echo "$V" | python3 -c 'import json,sys; v=json.load(sys.stdin); sys.exit(0 if v["mode"]=="source" and v["sidePane"]=={"open":True,"page":"outline"} and v["bounds"]["width"]==1100 and v["bounds"]["height"]==700 else 1)'; echo $?) "view: source mode, the outline, 1100x700 in one call ($(echo $V | cut -c1-120))"
  V=$($ICTL view $W --mode visual --side-pane closed)
  check $(echo "$V" | python3 -c 'import json,sys; v=json.load(sys.stdin); sys.exit(0 if v["mode"]=="visual" and not v["sidePane"]["open"] else 1)'; echo $?) "view: back to visual, pane closed"
  R=$($ICTL settings get | python3 -c 'import json,sys; print(json.load(sys.stdin)["settingsRevision"])')
  R=$($ICTL settings set ui.language ja --base-revision $R | python3 -c 'import json,sys; print(json.load(sys.stdin)["settingsRevision"])'); check $? "settings: the language set to ja"
  check $(grep -q '"Language": "ja"' $T/data/Typedown.Uno/settings.json; echo $?) "the settings file says ja"
  $ICTL settings set ui.language en --base-revision $R >/dev/null; check $? "and back to en"
else
  echo "(no /usr/bin/typedownctl: the package is not installed)"
fi

echo "== clean exit removes the socket (a fresh instance, nothing unsaved to ask about)"
pkill -f "$APP_PATTERN"; sleep 2
printf '# exit\n' > $T/docs/tdnuc-exit.md
start_app $T/docs/tdnuc-exit.md; sleep 3
w=$(app_window tdnuc-exit.md); xdotool windowactivate --sync $w key alt+F4
for i in $(seq 1 20); do pgrep -f "$APP_PATTERN" >/dev/null || break; sleep 1; done
pkill -f "$APP_PATTERN" && echo "(had to be killed)"
check $([ ! -e $SOCK ]; echo $?) "no socket file after the app exits"
$CTL status >/dev/null 2>&1; check $([ $? = 3 ]; echo $?) "typedownctl says not running (exit 3)"
echo "OVERALL $([ $ok = 1 ] && echo PASS || echo FAIL)"
