#!/usr/bin/env bash
# Renders every Mermaid source here to ../<name>.svg and ../<name>.png, and the use case diagram (use-cases.py).
# Needs: node + @mermaid-js/mermaid-cli (npx), a Chromium (PUPPETEER_EXECUTABLE_PATH), python3; PNGs via sharp-cli or mmdc.
set -euo pipefail
cd "$(dirname "$0")"
MMDC=${MMDC:-npx -y @mermaid-js/mermaid-cli@11.4.2}
PUP=${PUP:-puppeteer.json}
[[ -f $PUP ]] || echo "{\"executablePath\":\"${PUPPETEER_EXECUTABLE_PATH:-/opt/pw-browsers/chromium}\",\"args\":[\"--no-sandbox\"]}" > "$PUP"
for f in *.mmd; do
  n=${f%.mmd}
  [[ $n == layers || $n == plan-flow ]] && continue   # kept as history only
  $MMDC -p "$PUP" -c mermaid-config.json -b white -i "$f" -o "../$n.svg" -q
  $MMDC -p "$PUP" -c mermaid-config.json -b white -s 2 -i "$f" -o "../$n.png" -q
done
python3 use-cases.py ../use-cases.svg
node -e "require('sharp')('../use-cases.svg', {density: 144}).png().toFile('../use-cases.png')"   # needs sharp (NODE_PATH)
