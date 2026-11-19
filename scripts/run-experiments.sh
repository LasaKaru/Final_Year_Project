#!/usr/bin/env bash
# Final-experiment driver (methodology §3): configurations × dataset cases × repetitions, then the summary and a
# determinism check. Run it only after the labels are frozen (21 Mar 2027); earlier runs are rehearsals.
#
#   scripts/run-experiments.sh                         # symbolic arm, all cases, 3 repetitions
#   REPS=1 CASES="A-001 A-002" scripts/run-experiments.sh
#   MODES="symbolic hybrid" MODEL=models/phi3-mini REPS=1 scripts/run-experiments.sh   # hybrid = both arms (ablation)
#
# The baseline SAST arm comes from .github/workflows/baseline-codeql.yml (SARIF artifacts) and is added per case with
#   bolalens eval --report … --labels … --baseline-sarif csharp.sarif --baseline-name CodeQL --baseline-cwe 639 285 862 863
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
REPS="${REPS:-3}"
MODES="${MODES:-symbolic}"
CASES="${CASES:-$(python3 -I -c "import json,sys;print(' '.join(c['caseId'] for c in json.load(open(sys.argv[1]))['cases']))" "$ROOT/eval/datasets/cases.json")}"
export RUN_DATE="${RUN_DATE:-$(date -u +%F)}"

for mode in $MODES; do
  for rep in $(seq 1 "$REPS"); do
    suffix=""; [[ "$rep" -gt 1 ]] && suffix="-r$rep"
    for c in $CASES; do
      echo "### $mode · $c · repetition $rep"
      MODE="$mode" RUN_SUFFIX="$suffix" "$ROOT/scripts/run-case.sh" "$c" | grep -E "→|!!" || true
    done
  done
  python3 -I "$ROOT/scripts/dataset-report.py" --mode "$mode"
done

# Determinism: every repetition must give the same final verdict per (endpoint, taint source) for every case.
python3 -I - "$ROOT" "$RUN_DATE" "$MODES" <<'PY'
import glob, gzip, json, os, re, sys
root, date, modes = sys.argv[1], sys.argv[2], sys.argv[3].split()
def verdicts(run):
    gz = os.path.join(run, "report.json.gz")
    r = json.load(gzip.open(gz, "rt")) if os.path.exists(gz) else json.load(open(os.path.join(run, "report.json")))
    return {(f["path"]["endpoint"]["id"], f["path"]["taintSource"]["name"]): (f["symbolic"]["status"], f["final"]) for f in r["findings"]}
bad = 0
for mode in modes:
    groups = {}
    for run in glob.glob(os.path.join(root, "eval", "runs", f"{date}-{mode}-*")):
        base = re.sub(r"-r\d+$", "", run)
        groups.setdefault(base, []).append(run)
    for base, runs in sorted(groups.items()):
        if len(runs) < 2: continue
        ref = verdicts(runs[0])
        diffs = [r for r in runs[1:] if verdicts(r) != ref]
        print(f"{'OK ' if not diffs else 'DIFF'} {os.path.basename(base)} ({len(runs)} runs)")
        bad += len(diffs)
print("determinism:", "all repetitions identical" if bad == 0 else f"{bad} run(s) differ")
sys.exit(1 if bad else 0)
PY
