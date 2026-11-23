#!/usr/bin/env python3
"""Import CodeQL SARIF artifacts of .github/workflows/baseline-codeql.yml into eval/baseline/codeql/.

    python3 scripts/baseline-codeql-import.py <dir-with-<case>-<variant>/csharp.sarif> --run-id 45130132411

Keeps the authorization-related results only (rules tagged CWE-284/285/566/639/862/863 — e.g.
cs/web/missing-function-level-access-control, cs/web/insecure-direct-object-reference) plus a count of all results, in
minimal SARIF that BaselineSarif (bolalens eval --baseline-sarif) reads. Standard library only.
"""
import argparse, glob, json, os, re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AUTHZ = {"284", "285", "566", "639", "862", "863"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("source")
    ap.add_argument("--run-id", required=True)
    a = ap.parse_args()
    out = os.path.join(ROOT, "eval", "baseline", "codeql")
    os.makedirs(out, exist_ok=True)
    index = []
    for f in sorted(glob.glob(os.path.join(a.source, "*", "*.sarif"))):
        name = os.path.basename(os.path.dirname(f))
        d = json.load(open(f))
        run = d["runs"][0]
        rules = {}
        for comp in [run["tool"]["driver"]] + run["tool"].get("extensions", []):
            for r in comp.get("rules", []):
                tags = (r.get("properties") or {}).get("tags", [])
                cwes = {m.group(1).lstrip("0") for t in tags for m in [re.search(r"cwe[-/](\d+)$", t, re.I)] if m}
                if cwes & AUTHZ:
                    rules[r["id"]] = {"id": r["id"], "properties": {"tags": [f"external/cwe/cwe-{c}" for c in sorted(cwes)]}}
        results = [{"ruleId": res.get("ruleId"), "message": {"text": res.get("message", {}).get("text", "")[:300]},
                    "locations": res.get("locations", [])[:1]}
                   for res in run.get("results", []) if res.get("ruleId") in rules]
        lite = {"$schema": "https://json.schemastore.org/sarif-2.1.0.json", "version": "2.1.0",
                "runs": [{"tool": {"driver": {"name": "CodeQL", "semanticVersion": run["tool"]["driver"].get("semanticVersion"),
                                              "rules": list(rules.values())}},
                          "results": results,
                          "properties": {"workflowRun": a.run_id, "allResults": len(run.get("results", [])),
                                         "filter": "rules tagged CWE-284/285/566/639/862/863"}}]}
        json.dump(lite, open(os.path.join(out, f"{name}.sarif"), "w"), indent=1)
        index.append(f"| {name} | {len(run.get('results', []))} | {len(results)} | {', '.join(sorted(rules)) or '—'} |")
    with open(os.path.join(out, "README.md"), "w") as fh:
        fh.write(f"# CodeQL baseline (workflow run {a.run_id})\n\nCodeQL C# `security-extended`, `build-mode: none`, each case at its pinned commit.\n"
                 "Only authorization-related results are kept (rules tagged CWE-284/285/566/639/862/863).\n\n"
                 "| Case | All results | Authorization results | Rules |\n|---|---|---|---|\n" + "\n".join(index) + "\n")
    print(f"imported {len(index)} SARIF files into eval/baseline/codeql")


if __name__ == "__main__":
    main()
