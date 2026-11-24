#!/usr/bin/env bash
# Pre-demo check for the IPD: clean tree, Release build, all tests, committed model, one BolaLab scan. Offline after the first clone.
# Usage (repository root):  ipd/08-demo/demo-prep.sh
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

step() { printf '\n== %s\n' "$*"; }

step "Git state"
git describe --tags --always
git status --short | head -5
[[ -z "$(git status --porcelain)" ]] && echo "clean tree" || echo "WARNING: uncommitted changes"

step "Build (Release)"
dotnet build BolaLens.sln -c Release -v q
dotnet build fixtures/BolaLab/BolaLab.sln -c Release -v q

step "Tests (without the model-dependent ones)"
dotnet test BolaLens.sln -c Release --no-build --filter "Category!=RequiresModel" 2>&1 | grep -E "Passed!|Failed!"

step "Committed SmolLM2 model checksums"
scripts/ensure-model.sh

step "Symbolic scan of BolaLab (expect 16/16, exit code 1 because of Vulnerable findings)"
set +e
dotnet run --project src/BolaLens.Cli -c Release --no-build -- scan fixtures/BolaLab/BolaLab.sln --out /tmp/bolalens-demo-report.json --quiet
echo "exit code $? (1 = Vulnerable findings present, as expected)"
set -e
dotnet run --project src/BolaLens.Cli -c Release --no-build -- eval --report /tmp/bolalens-demo-report.json --labels fixtures/BolaLab/expected.json | head -8

step "Dataset clones for the real-CVE step"
for d in ../datasets/A-001-vuln ../datasets/A-001-fixed; do [[ -d $d ]] && echo "ok  $d" || echo "MISSING $d (run scripts/run-case.sh A-001 with network)"; done

printf '\nReady. Start the UI with: dotnet run --project src/BolaLens.Web -c Release  (http://127.0.0.1:5057)\n'
