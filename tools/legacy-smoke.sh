#!/usr/bin/env bash
# Hosted E2E smoke for the legacy Zune compatibility hosts.
#
# Usage: tools/legacy-smoke.sh [base-url]
#   base-url defaults to http://127.0.0.1:5080 (the API; the gateway on :5088
#   works too and additionally exercises Host preservation).
#
# Exits non-zero if any endpoint returns 5xx. Corpus-backed endpoints are
# allowed to 404 when their corpus is not configured (fail-closed by design);
# a 5xx is always a failure.
set -uo pipefail

BASE="${1:-http://127.0.0.1:5080}"
fail=0

check() {
	local host="$1" path="$2" label="$3"
	local code
	code="$(curl -s -m 15 -o /dev/null -w '%{http_code}' -H "Host: $host" "$BASE$path" || echo 000)"
	printf '%-8s %-42s %s\n' "$code" "$host$path" "$label"
	if [[ "$code" == 5* || "$code" == 000 ]]; then
		fail=1
	fi
}

echo "Legacy Zune smoke against $BASE"
echo "----------------------------------------------------------------"

check "127.0.0.1"                          "/v1/catalog/ping"                         "modern JSON API"
check "127.0.0.1"                          "/"                                        "modern root"
check "resources.zune.net"                 "/FirmwareUpdate.xml"                      "firmware manifest (404=no corpus)"
check "catalog.zune.net"                   "/v3.2/en-US/hubs/music"                   "catalog hubs"
check "catalog.zune.net"                   "/appCategories"                           "app catalog categories"
check "image.catalog.zune.net"             "/"                                        "image host root"
check "tiles.zune.net"                     "/"                                        "tiles host root"
check "mix.zune.net"                       "/v4.0/en-US/track/00000000-0000-0000-0000-000000000000/similarTracks" "mix similar tracks"
check "socialapi.zune.net"                 "/members"                                 "social search (400=needs query)"
check "inbox.zune.net"                     "/messaging/smoke/inbox"                   "inbox listing"
check "tuners.zune.net"                    "/"                                        "tuners host root"
check "login.zune.net"                     "/ppcrlconfig.bin"                         "ppcrl config"
check "fai.music.metaservices.microsoft.com" "/ZuneAPI/EndPoints.aspx"                "metaservices discovery"

echo "----------------------------------------------------------------"
if [[ "$fail" -eq 0 ]]; then
	echo "PASS (no 5xx)"
else
	echo "FAIL (a 5xx or connection error was seen)"
fi
exit "$fail"
