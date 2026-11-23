#!/usr/bin/env python3
"""Draw a stratified, reproducible sample of findings for the RQ4 explanation-quality study (docs/evaluation/rq4-rubric.md).

    python3 scripts/rq4-sample.py --n 40 --seed 2027        # → eval/rq4/rating-sheet.csv and .md

Strata: final verdict (Vulnerable / Protected via delegated rule SYM-05 / Unknown) × dataset (A vulnerable commits,
C fixed commits, B wild repos). Latest completed symbolic run per case variant; BolaLab is never sampled.
Standard library only. Rating columns are left empty for the human raters.
"""
import argparse, csv, glob, gzip, json, os, random

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def runs():
    cases = json.load(open(os.path.join(ROOT, "eval", "datasets", "cases.json")))["cases"]
    for c in cases:
        for variant in c["variants"]:
            found = sorted(r for r in glob.glob(os.path.join(ROOT, "eval", "runs", f"*-symbolic-{c['caseId']}-{variant}"))
                           if os.path.exists(os.path.join(r, "env.md")))
            if not found:
                continue
            ds = "C" if c["dataset"] == "A" and variant == "fixed" else c["dataset"]
            yield c, variant, ds, found[-1]


def load(run):
    gz = os.path.join(run, "report.json.gz")
    return json.load(gzip.open(gz, "rt")) if os.path.exists(gz) else json.load(open(os.path.join(run, "report.json")))


def stratum(f):
    if f["final"] == "Vulnerable":
        return "Vulnerable"
    if f["final"] == "Unknown":
        return "Unknown"
    if f["final"] == "Protected" and "SYM-05" in f["symbolic"]["ruleIds"]:
        return "Protected-delegated"
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--n", type=int, default=40)
    ap.add_argument("--seed", type=int, default=2027)
    a = ap.parse_args()
    rng = random.Random(a.seed)
    pools = {}
    for c, variant, ds, run in runs():
        for f in load(run)["findings"]:
            s = stratum(f)
            if s:
                pools.setdefault((s, ds), []).append((c, variant, run, f))
    keys = sorted(pools)
    per = max(1, a.n // max(1, len(keys)))
    picked = []
    for k in keys:
        pool = pools[k]
        rng.shuffle(pool)
        picked += [(k, x) for x in pool[:per]]
    rng.shuffle(picked)
    picked = picked[: a.n]
    out = os.path.join(ROOT, "eval", "rq4")
    os.makedirs(out, exist_ok=True)
    cols = ["item", "case", "variant", "dataset", "stratum", "endpoint", "taint_source", "verdict", "rules", "rationale",
            "sink_location", "run", "agree", "R1_root_cause", "R2_location", "R3_actionability", "R4_unknown_honesty",
            "R5_minutes", "notes", "rater", "date"]
    with open(os.path.join(out, "rating-sheet.csv"), "w", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=cols)
        w.writeheader()
        for i, ((s, ds), (c, variant, run, f)) in enumerate(picked, 1):
            sink = next((x["location"] for x in f["path"]["sinks"] if x.get("isStateChanging")), None) or (f["path"]["sinks"][0]["location"] if f["path"]["sinks"] else f["path"]["endpoint"]["location"])
            w.writerow({"item": i, "case": c["caseId"], "variant": variant, "dataset": ds, "stratum": s,
                        "endpoint": f["path"]["endpoint"]["id"], "taint_source": f["path"]["taintSource"]["name"],
                        "verdict": f["final"], "rules": ",".join(f["symbolic"]["ruleIds"]), "rationale": f["symbolic"]["rationale"],
                        "sink_location": f"{sink['filePath']}:{sink['startLine']}", "run": os.path.relpath(run, ROOT)})
    with open(os.path.join(out, "README.md"), "w") as fh:
        fh.write(f"# RQ4 sample (seed {a.seed}, n={len(picked)})\n\nStrata: {', '.join(f'{s}/{d}: {len(pools[(s, d)])}' for s, d in keys)}.\n"
                 "Rate with docs/evaluation/rq4-rubric.md; one copy of rating-sheet.csv per rater (rating-sheet.<rater>.csv).\n")
    print(f"wrote eval/rq4/rating-sheet.csv ({len(picked)} items from {len(keys)} strata)")


if __name__ == "__main__":
    main()
