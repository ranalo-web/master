#!/bin/zsh
# Send IMEIs to Knox Guard through Veritech, check them, and lock them --
# for Samsung phones whose Nuovo lock may have been removed.
#
# Same calls as the app (Ranalo/VeriTechClient/VeritechApiClient.cs). The
# login is the Veritech API key, read from Ranalo/appsettings.Development.json
# (Veritech:BaseUrl, Veritech:ApiKey) -- nothing secret is printed.
#
# Steps:
#   ./veritech_knox.sh upload          send the IMEIs below to Veritech -> Knox
#   ./veritech_knox.sh status <txId>   result of an upload (txId printed by upload)
#   ./veritech_knox.sh devices         what Veritech/Knox holds for these IMEIs
#     -> approve the pending phones in the Knox Guard console (the app does this
#        with the Knox API at enrolment; there is no Veritech call for it)
#   ./veritech_knox.sh lock            lock them (only works once approved and
#                                      the phone has connected to Knox)
#   ./veritech_knox.sh unlock <imei>   undo a lock on one phone
#
# Only Samsung phones can be caught by Knox.

set -euo pipefail

IMEIS=(
  351053299931406   # 8143984  Galaxy A05
  358253401180537   # 8511324  Denis (Newway)
  355132185846260   # 8583526  Margaret (Newway)
  355132188643698   # 8680519  Harrison's phone (Newway)
)
LOCK_MESSAGE="This phone is locked by Ranalo Credit. Please call us to settle your account."

CONFIG="$(dirname "$0")/../Ranalo/appsettings.Development.json"
read_config() {
  python3 - "$CONFIG" "$1" <<'PY'
import json, re, sys
text = re.sub(r'^\s*//.*$', '', open(sys.argv[1]).read(), flags=re.M)
print(json.loads(text)['Veritech'][sys.argv[2]])
PY
}
BASE_URL="$(read_config BaseUrl)"
API_KEY="$(read_config ApiKey)"

call() {  # call METHOD PATH [JSON]
  local method=$1 path=$2 body=${3:-}
  local args=(-sS -X "$method" "${BASE_URL%/}/$path" -H "x-vtkdp-key: $API_KEY" -H "Accept: application/json")
  [[ -n "$body" ]] && args+=(-H "Content-Type: application/json" -d "$body")
  curl "${args[@]}" -w '\n[HTTP %{http_code}]\n'
}

json_list() { python3 -c 'import json,sys; print(json.dumps(sys.argv[1:]))' "$@"; }

case "${1:-}" in
  upload)
    echo "Uploading ${#IMEIS[@]} IMEIs to Veritech..."
    call POST devices/upload "{\"devices\": $(json_list "${IMEIS[@]}")}"
    echo "Keep the transaction id above for: $0 status <txId>"
    ;;
  status)
    [[ -z "${2:-}" ]] && { echo "Usage: $0 status <transactionId>"; exit 1; }
    call GET "devices/transaction-status/$2"
    ;;
  devices)
    # The full list can be long; show only our IMEIs.
    call GET devices | python3 -c '
import json, sys
raw = sys.stdin.read()
body, _, http = raw.rpartition("\n[HTTP")
wanted = set(sys.argv[1:])
def lower(o):  # the API is read case-insensitively by the app too
    if isinstance(o, dict): return {k.lower(): lower(v) for k, v in o.items()}
    if isinstance(o, list): return [lower(v) for v in o]
    return o
try:
    rows = (lower(json.loads(body)).get("data") or {}).get("devicelist") or []
except ValueError:
    print(raw); sys.exit()
found = {r.get("imeinumber"): r for r in rows}
for imei in sys.argv[1:]:
    r = found.get(imei)
    print(imei, "->", r.get("status") if r else "not in Veritech/Knox")
print("[HTTP" + http)
' "${IMEIS[@]}"
    ;;
  lock)
    for imei in "${IMEIS[@]}"; do
      echo "== $imei"
      call POST knox-guard/lock-device "{\"ImeiNumber\": \"$imei\", \"LockScreenMessage\": \"$LOCK_MESSAGE\"}"
    done
    ;;
  unlock)
    [[ -z "${2:-}" ]] && { echo "Usage: $0 unlock <imei>"; exit 1; }
    call POST knox-guard/unlock-device "{\"ImeiNumber\": \"$2\", \"RelockTimestamp\": 0, \"LockScreenMessage\": \"\"}"
    ;;
  *)
    sed -n '2,20p' "$0"
    ;;
esac
