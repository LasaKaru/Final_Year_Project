#!/usr/bin/env bash
# Fetch a dataset case at its pinned commit(s), restore it, scan it with BolaLens and record the run.
#
#   scripts/run-case.sh A-001                 # all variants of a case (vuln + fixed)
#   scripts/run-case.sh A-002 vuln            # one variant
#   MODE=hybrid MODEL=models/phi3-mini scripts/run-case.sh B-001
#
# Cases are listed in eval/datasets/cases.json. Third-party code is cloned OUTSIDE this repo into
# $BOLALENS_DATASETS (default: ../datasets) and never committed. The analysed code is only parsed and
# type-checked by BolaLens, never executed; `dotnet restore` downloads its NuGet packages (network).
# Output: eval/runs/<date>-<mode>-<case>-<variant>/{report.json.gz,console.txt,env.md[,metrics.md]}.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DATASETS="${BOLALENS_DATASETS:-$(dirname "$ROOT")/datasets}"
MODE="${MODE:-symbolic}"
MODEL="${MODEL:-}"
DATE="${RUN_DATE:-$(date -u +%F)}"
CLI="$ROOT/src/BolaLens.Cli/bin/Release/net8.0/bolalens.dll"

case_id="${1:?usage: run-case.sh <caseId> [variant...]}"; shift || true

# Read the case with python (stdlib only) to keep the manifest plain JSON.
read_case() {
  python3 -I - "$ROOT/eval/datasets/cases.json" "$case_id" <<'PY'
import json, sys, shlex
cases = {c["caseId"]: c for c in json.load(open(sys.argv[1]))["cases"]}
c = cases.get(sys.argv[2])
if c is None: sys.exit(f"unknown case {sys.argv[2]}; known: {', '.join(cases)}")
print(f"family={shlex.quote(c['family'])} repo={shlex.quote(c['repo'])} solution={shlex.quote(c['solution'])} "
      f"config={shlex.quote(c.get('config') or '')}")
print("variants=(" + " ".join(f"{k}:{v}" for k, v in c["variants"].items()) + ")")
PY
}
eval "$(read_case)"

want=("$@")
mirror="$DATASETS/$family"
mkdir -p "$DATASETS"
if [[ ! -d "$mirror/.git" ]]; then
  echo "== cloning $repo → $mirror"
  git clone --quiet --filter=blob:none "$repo" "$mirror"
fi

echo "== building BolaLens (Release) — never scan with a stale build"
dotnet build "$ROOT/BolaLens.sln" -c Release -v quiet -nologo -clp:ErrorsOnly >/dev/null

for entry in "${variants[@]}"; do
  variant="${entry%%:*}"; sha="${entry#*:}"
  if [[ ${#want[@]} -gt 0 && ! " ${want[*]} " =~ " $variant " ]]; then continue; fi
  work="$DATASETS/$case_id-$variant"
  if [[ ! -d "$work" ]]; then
    git -C "$mirror" fetch --quiet origin "$sha" 2>/dev/null || true
    git -C "$mirror" worktree add --quiet --detach "$work" "$sha"
  fi
  actual="$(git -C "$work" rev-parse HEAD)"
  [[ "$actual" == "$sha" ]] || { echo "!! $work is at $actual, expected $sha" >&2; exit 2; }

  # A pinned SDK that is not installed (e.g. 10.0.403 latestFeature with only 10.0.1xx present) stops MSBuild before
  # anything is loaded. Adapt global.json to the newest installed SDK; this changes no code and is recorded in env.md.
  sdk_note="unchanged"
  if [[ -f "$work/global.json" ]] && ! (cd "$work" && dotnet --version >/dev/null 2>&1); then
    newest="$(dotnet --list-sdks | tail -1 | cut -d' ' -f1)"
    printf '{ "sdk": { "version": "%s", "rollForward": "latestMajor" } }\n' "$newest" > "$work/global.json"
    sdk_note="adapted to SDK $newest (original pinned an SDK that is not installed: git diff shows it)"
  fi

  echo "== $case_id/$variant @ ${sha:0:8}: restore $solution"
  restore_log="$(mktemp)"
  if ! (cd "$work" && dotnet restore "$solution" -nologo -v quiet -p:NuGetAudit=false >"$restore_log" 2>&1); then
    echo "!! restore failed (scan continues; missing references appear as compile errors in the report)"; tail -5 "$restore_log"
  fi

  out="$ROOT/eval/runs/$DATE-$MODE-$case_id-$variant"
  mkdir -p "$out"
  args=(scan "$work/$solution" --mode "$MODE" --out "$out/report.json" --no-fail)
  [[ -n "$config" ]] && args+=(--config "$ROOT/$config")
  [[ -n "$MODEL" ]] && args+=(--model "$MODEL")
  start=$(date +%s)
  dotnet "$CLI" "${args[@]}" > "$out/console.txt" 2>&1 || {
    echo "!! scan failed, see $out/console.txt"; tail -5 "$out/console.txt"; continue; }
  secs=$(( $(date +%s) - start ))
  rss_mb=$(python3 -I -c "import json,sys;print(json.load(open(sys.argv[1]))['run']['peakWorkingSetBytes']//1048576)" "$out/report.json")

  labels="$ROOT/eval/labels/$case_id-$variant.labels.json"
  [[ -f "$labels" ]] || labels="$ROOT/eval/labels/drafts/$case_id-$variant.labels.json"
  if [[ -f "$labels" ]]; then
    dotnet "$CLI" eval --report "$out/report.json" --labels "$labels" --out "$out/metrics.md" >/dev/null || true
  fi

  # real-repository reports are large (≈10 MB): keep them gzipped in git (zcat / gunzip -k to read them)
  gzip -9 -f "$out/report.json"

  cat > "$out/env.md" <<EOF
# Run environment — $case_id / $variant ($MODE)

* Target: $repo @ \`$sha\` — \`$solution\`
* global.json: $sdk_note
* Config: ${config:-defaults (bolalens.json)}${MODEL:+ · model \`$MODEL\`}
* Machine: $(uname -sm), $(nproc) logical cores; .NET SDKs: $(dotnet --list-sdks | cut -d' ' -f1 | paste -sd, -)
* BolaLens commit: \`$(git -C "$ROOT" rev-parse --short HEAD)\`$(git -C "$ROOT" diff --quiet || echo ' (+ uncommitted changes)')
* Wall time (incl. load): ${secs} s · peak working set: ${rss_mb} MB
* Labels: $( [[ -f "$labels" ]] && echo "\`${labels#$ROOT/}\`" || echo "none yet" )
* Recorded by \`scripts/run-case.sh\` on $(date -u +%FT%TZ).
EOF
  echo "   → ${out#$ROOT/} (${secs} s, ${rss_mb} MB)"
done
