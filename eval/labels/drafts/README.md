# Draft labels (NOT frozen, NOT yet verified by the project owner)

These files were derived **mechanically from the vendor's fix commits and advisories** (Dataset A rule, guide 07:
pre-patch endpoint = Vulnerable, same endpoint after the fix = Protected). They are not a judgement of the tool's output.
Before the label freeze (21 Mar 2027) the project owner must, for every entry:

1. read the endpoint at both commits (`scripts/run-case.sh` checks them out under `../datasets/<case>-<variant>`),
2. apply the 3-point protocol (client id source · cross-user effect · owner check on the stored object before the effect),
3. move the file to `eval/labels/` (same name), record the date in `eval/labels/CHANGELOG.md`, and never edit it after the freeze.

Notes per case:
* **A-002 fixed / PUT is labelled Vulnerable on purpose**: the CVE-2024-55186 fix (null user id) left the request-body owner
  check in `Put`; the vendor fixed that only in 2026 (commit 0192d6e4 = case A-001).
* **A-003** is a field-level exposure (private settings inside a returned user); ADR-011 records it as out of the model.
* Fixed variants double as **Dataset C** (secure controls): every flagged endpoint there is a false positive unless the
  label says otherwise.
