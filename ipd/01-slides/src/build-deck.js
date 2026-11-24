// Builds ipd/01-slides/BolaLens-IPD.pptx (structured deck: theme, layouts, sections, speaker notes).
// Usage:  NODE_PATH=<folder with pptxgenjs> node ipd/01-slides/src/build-deck.js [applyThemeModulePath]
// Numbers on the slides come from eval/results/*.md and docs/requirements.md (24 Nov 2026) — update them before the IPD.
const path = require("path");
const pptxgen = require("pptxgenjs");

const ROOT = path.resolve(__dirname, "..", "..", "..");
const D = (f) => path.join(ROOT, "ipd", "diagrams", f);
const S = (f) => path.join(ROOT, "ipd", "screenshots", f);
const OUT = path.join(ROOT, "ipd", "01-slides", "BolaLens-IPD.pptx");

const THEME = {
  name: "BolaLens",
  headFontFace: "Cambria",
  bodyFontFace: "Calibri",
  colors: {
    dk1: "0F1B2D", lt1: "FFFFFF", dk2: "1F3A5F", lt2: "EEF2F6",
    accent1: "1F6F78", accent2: "B03A2E", accent3: "2E7D4F", accent4: "A86B12", accent5: "5B6573", accent6: "D9E7EA",
    hlink: "1F6F78", folHlink: "5B6573",
  },
};
const HEX = THEME.colors;

const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE"; // 13.333 x 7.5 in
pres.author = "Lasantha Karunarathne";
pres.company = "IIT / University of Westminster";
pres.title = "BolaLens — Interim Progression Demonstration";
pres.subject = "Automated Context-Aware BOLA Detection in ASP.NET Core Web APIs";
pres.theme = { headFontFace: THEME.headFontFace, bodyFontFace: THEME.bodyFontFace };
const C = pres.SchemeColor;

const W = 13.333, MX = 0.6, CW = W - 2 * MX;
const FOOT = "BolaLens · IPD 2026/27 · Lasantha Karunarathne (20223087)";

// ---------- layouts ----------
pres.defineSlideMaster({
  title: "TITLE_DARK",
  background: { color: C.text1 },
  objects: [
    { placeholder: { options: { name: "title", type: "title", x: MX, y: 2.05, w: 10.6, h: 1.5, fontFace: THEME.headFontFace, fontSize: 44, bold: true, color: C.background1, valign: "bottom", align: "left", margin: 0 }, text: "" } },
    { placeholder: { options: { name: "body", type: "body", x: MX, y: 3.75, w: 10.6, h: 1.1, fontSize: 20, color: C.accent6, valign: "top", align: "left", margin: 0 }, text: "" } },
  ],
});
pres.defineSlideMaster({
  title: "CONTENT",
  background: { color: C.background1 },
  margin: [0.5, 0.6, 0.6, 0.6],
  objects: [
    { placeholder: { options: { name: "title", type: "title", x: MX, y: 0.32, w: CW, h: 0.85, fontFace: THEME.headFontFace, fontSize: 32, bold: true, color: C.text1, valign: "middle", margin: 0 }, text: "" } },
    { text: { text: FOOT, options: { x: MX, y: 7.05, w: 8, h: 0.3, fontSize: 10, color: C.accent5, margin: 0 } } },
  ],
  slideNumber: { x: 12.2, y: 7.05, w: 0.6, h: 0.3, fontSize: 10, color: C.accent5, align: "right" },
});
pres.defineSlideMaster({
  title: "SECTION_DARK",
  background: { color: C.text2 },
  objects: [
    { placeholder: { options: { name: "title", type: "title", x: MX, y: 2.6, w: CW, h: 1.2, fontFace: THEME.headFontFace, fontSize: 40, bold: true, color: C.background1, valign: "middle", margin: 0 }, text: "" } },
    { placeholder: { options: { name: "body", type: "body", x: MX, y: 3.9, w: CW, h: 1.0, fontSize: 20, color: C.accent6, valign: "top", margin: 0 }, text: "" } },
  ],
  slideNumber: { x: 12.2, y: 7.05, w: 0.6, h: 0.3, fontSize: 10, color: C.accent6, align: "right" },
});

// ---------- helpers ----------
let n = 0;
function content(title, section) {
  const s = pres.addSlide({ masterName: "CONTENT", sectionTitle: section });
  s.addText(title, { placeholder: "title" });
  return s;
}
function txt(s, text, o) { s.addText(text, Object.assign({ isTextBox: true, fontSize: 14, color: C.text1, margin: 0, valign: "top" }, o)); }
function card(s, x, y, w, h, fill) {
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.08, fill: { color: fill || C.background2 }, line: { color: fill || C.background2 }, objectName: `card-${++n}` });
}
function chip(s, x, y, w, label, color) {
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h: 0.34, rectRadius: 0.17, fill: { color }, line: { color }, objectName: `chip-${++n}` });
  s.addText(label, { isTextBox: true, x, y, w, h: 0.34, fontSize: 12, bold: true, color: C.background1, align: "center", valign: "middle", margin: 0 });
}
function stat(s, x, y, w, value, label, color) {
  card(s, x, y, w, 1.55);
  txt(s, value, { x: x + 0.2, y: y + 0.15, w: w - 0.4, h: 0.8, fontSize: 40, bold: true, color: color || C.accent1, fontFace: THEME.headFontFace, valign: "middle" });
  txt(s, label, { x: x + 0.2, y: y + 0.95, w: w - 0.4, h: 0.5, fontSize: 13, color: C.text2 });
}
function img(s, file, x, y, w, h, ratio, alt) {
  // fit inside the box, keep the aspect ratio, centre
  let iw = w, ih = w / ratio;
  if (ih > h) { ih = h; iw = h * ratio; }
  s.addImage({ path: file, x: x + (w - iw) / 2, y: y + (h - ih) / 2, w: iw, h: ih, altText: alt, objectName: `img-${++n}` });
}
function bullets(s, items, o) {
  const runs = items.map((t, i) => {
    const parts = Array.isArray(t) ? t : [{ text: t }];
    return parts.map((p, j) => ({ text: p.text, options: Object.assign({ bullet: j === 0 ? { indent: 16 } : undefined, breakLine: j === parts.length - 1 && i < items.length - 1, paraSpaceAfter: 8 }, p.options || {}) }));
  }).flat();
  s.addText(runs, Object.assign({ isTextBox: true, fontSize: 15, color: C.text1, margin: 0, valign: "top" }, o));
}
const STATUS = { done: { fill: "DFF3E4", text: HEX.accent3, label: "✓" }, partial: { fill: "FFF1CC", text: HEX.accent4, label: "◐" }, pending: { fill: "EEF0F3", text: HEX.accent5, label: "○" } };
function tile(s, x, y, w, h, id, name, status, note) {
  const st = STATUS[status];
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.06, fill: { color: st.fill }, line: { color: st.fill }, objectName: `tile-${id}` });
  s.addText([
    { text: `${st.label} ${id}  `, options: { bold: true, color: st.text } },
    { text: name, options: { color: HEX.dk1 } },
    ...(note ? [{ text: `\n${note}`, options: { fontSize: 11, color: HEX.accent5 } }] : []),
  ], { isTextBox: true, x: x + 0.12, y, w: w - 0.2, h, fontSize: 13, valign: "middle", margin: 0 });
}
function legend(s, x, y) {
  [["done", "Implemented"], ["partial", "Partially implemented"], ["pending", "Pending"]].forEach(([k, l], i) => {
    const st = STATUS[k];
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: x + i * 2.6, y, w: 0.34, h: 0.28, rectRadius: 0.06, fill: { color: st.fill }, line: { color: st.text }, objectName: `legend-${k}` });
    txt(s, `${st.label} ${l}`, { x: x + i * 2.6 + 0.45, y, w: 2.1, h: 0.28, fontSize: 12, color: C.text2, valign: "middle" });
  });
}

// =====================================================================================================
// 1 Title
pres.addSection({ title: "Introduction" });
{
  const s = pres.addSlide({ masterName: "TITLE_DARK", sectionTitle: "Introduction" });
  s.addText("BolaLens", { placeholder: "title" });
  s.addText("Automated Context-Aware BOLA Detection in ASP.NET Core Web APIs\nInterim Progression Demonstration · 6COSC023C", { placeholder: "body" });
  txt(s, "Lasantha Karunarathne · IIT 20223087 · UoW W19598693\nSupervisor: Ms. Pudara Dewdini · January 2027", { x: MX, y: 5.6, w: 9, h: 0.8, fontSize: 15, color: C.background1 });
  [["Vulnerable", HEX.accent2], ["Protected", HEX.accent3], ["Unknown", HEX.accent4], ["Out of scope", HEX.accent5]].forEach(([l, c], i) => chip(s, MX + i * 1.75, 1.35, 1.6, l, c));
  s.addNotes("~15 s. Introduce yourself and the project in one sentence: a static analyser that finds Broken Object Level Authorization in ASP.NET Core APIs. The four coloured chips are the four verdicts the tool gives — they come back on every slide. (Rewrite all notes in your own words.)");
}

// 2 Problem · aim · solution
{
  const s = content("One missing check lets any user change anyone's data", "Introduction");
  card(s, MX, 1.4, 5.7, 4.75);
  txt(s, "The bug (OWASP API1:2023 BOLA)", { x: MX + 0.3, y: 1.6, w: 5.1, h: 0.4, fontSize: 18, bold: true, color: C.accent2 });
  s.addText([
    { text: "[HttpPut(\"{id}/status\")]\n", options: { color: HEX.accent5 } },
    { text: "var order = await db.Orders\n    .FirstOrDefaultAsync(o => o.Id == id);\n", options: {} },
    { text: "// missing: order.UserId == caller ?\n", options: { color: HEX.accent2, bold: true } },
    { text: "order.Status = request.Status;\nawait db.SaveChangesAsync();", options: {} },
  ], { isTextBox: true, x: MX + 0.3, y: 2.15, w: 5.1, h: 1.9, fontFace: "Courier New", fontSize: 14, color: C.text1, margin: 0, valign: "top" });
  txt(s, "Compiles, passes tests, looks normal — the flaw is a check that is not there. Generic SAST tools look for a missing [Authorize], not for an owner binding.", { x: MX + 0.3, y: 4.2, w: 5.1, h: 1.7, fontSize: 15, color: C.text2 });
  const x2 = MX + 6.1, w2 = CW - 6.1;
  txt(s, "Aim", { x: x2, y: 1.45, w: w2, h: 0.4, fontSize: 18, bold: true, color: C.accent1 });
  txt(s, "Find BOLA statically: follow a client-supplied id to the EF Core operation and decide whether a check binds that object to the caller before the change.", { x: x2, y: 1.9, w: w2, h: 1.1, fontSize: 15 });
  txt(s, "Solution — three stages, fully offline", { x: x2, y: 3.15, w: w2, h: 0.4, fontSize: 18, bold: true, color: C.accent1 });
  [["1", "Symbolic (Roslyn)", "endpoints · ownership schema · id taint · guards · rules SYM-01…11"],
   ["2", "Semantic (local SLM)", "label-free slice of uncertain paths → ONNX model → conservative fusion"],
   ["3", "Report + evaluate", "JSON · SARIF · web UI call graph · metrics vs labels and CodeQL"]].forEach(([k, h, d], i) => {
    const y = 3.65 + i * 1.0;
    s.addShape(pres.shapes.OVAL, { x: x2, y, w: 0.55, h: 0.55, fill: { color: C.accent1 }, line: { color: C.accent1 }, objectName: `step-${k}` });
    txt(s, k, { x: x2, y, w: 0.55, h: 0.55, fontSize: 18, bold: true, color: C.background1, align: "center", valign: "middle" });
    txt(s, [{ text: h + "\n", options: { bold: true } }, { text: d, options: { color: HEX.dk2, fontSize: 13 } }], { x: x2 + 0.75, y: y - 0.05, w: w2 - 0.75, h: 0.9, fontSize: 15 });
  });
  s.addNotes("~50 s. Do not re-teach BOLA — the PP covered it. Point at the code: it compiles and passes tests; the vulnerability is the missing owner check. Aim in one sentence. Then the three stages: stage 1 does the real work, stage 2 is a local model as a second opinion, stage 3 reports and measures. Everything runs offline — source code never leaves the machine.");
}

// 3 Progress at a glance
{
  const s = content("Progress since the PP: a working, evaluated prototype", "Introduction");
  stat(s, MX, 1.35, 2.85, "197", "automated tests, CI green");
  stat(s, MX + 3.1, 1.35, 2.85, "27", "real cases recorded (A/B/C)");
  stat(s, MX + 6.2, 1.35, 2.85, "13 / 25", "real CVE pairs told apart", HEX.accent3);
  stat(s, MX + 9.3, 1.35, 2.83, "0 / 33", "found by CodeQL (baseline)", HEX.accent2);
  const cols = [
    ["Completed", HEX.accent3, ["Roslyn engine: endpoints, policy, EF schema, DI, taint, guards, 11 rules", "Local SLM stage: slices, gate, fusion (SmolLM2, Phi-3)", "JSON, SARIF, GitHub Action, web UI, HTML", "Evaluation harness, robustness runner, CodeQL arm"]],
    ["In progress", HEX.accent4, ["Label verification (drafts) and blind labelling of Dataset B", "Tenant scoping, creation-time BOLA, policy handlers", "Model comparison on the 16 GB laptop"]],
    ["Not started", HEX.accent5, ["Threshold τ calibration (after label freeze 21 Mar)", "RQ4 explanation study with 2 raters", "Windows run · dissertation chapters"]],
  ];
  cols.forEach(([h, c, items], i) => {
    const x = MX + i * 4.1, w = 3.9;
    chip(s, x, 3.2, 1.9, h, c);
    bullets(s, items, { x, y: 3.7, w, h: 3.1, fontSize: 14 });
  });
  s.addNotes("~35 s. The headline: this is not a plan any more — it is a working tool measured on real vulnerabilities. Read the four numbers. Then the three columns: completed, in progress, not started — the demo will show the completed parts live.");
}

// 4 Changes since PP
{
  const s = content("What changed since the PP — and why", "Introduction");
  const rows = [
    ["Change", "Why"],
    ["Unknown verdict added (four-state result)", "missing evidence must never look \"safe\" (ADR-003)"],
    ["Read BOLA added as a secondary scope", "most real advisories we found are reads (ADR-011)"],
    ["Membership objects (SYM-11), privileged roles (SYM-10)", "Kavita, Bitwarden, Oqtane use libraries / organisations / admin roles"],
    ["SLM may only suppress, never escalate", "both models added false positives when allowed to escalate (ADR-012)"],
    ["Guard windows → dominance for exit checks", "a check inside an unrelated if must not protect a change (ADR-016)"],
    ["Local offline web UI instead of React; SARIF + Action early", "offline rule; developer and CI stakeholders (ADR-010)"],
    ["Stayed on .NET 8 (PP platform)", "owner decision; end of support recorded as a threat (ADR-001)"],
  ];
  s.addTable(rows.map((r, i) => r.map((t) => ({ text: t, options: { bold: i === 0, color: i === 0 ? HEX.lt1 : HEX.dk1, fill: { color: i === 0 ? HEX.dk2 : (i % 2 ? HEX.lt2 : HEX.lt1) } } }))),
    { x: MX, y: 1.4, w: CW, colW: [5.6, CW - 5.6], fontSize: 14, fontFace: THEME.bodyFontFace, rowH: 0.62, valign: "middle", border: { type: "none" }, margin: [0.05, 0.12, 0.05, 0.12] });
  s.addNotes("~45 s. Every change is backed by an ADR in docs/adr — say which evidence forced it. Emphasise: the scope changes came from real CVEs, the SLM change came from measurement, not opinion.");
}

// 5 Stakeholders
pres.addSection({ title: "Stakeholders and requirements" });
{
  const s = content("Stakeholders shaped the requirements", "Stakeholders and requirements");
  img(s, D("stakeholder-onion.png"), MX, 1.3, 7.6, 5.6, 2800 / 1800, "Stakeholder onion model with four rings");
  const x = MX + 7.9, w = CW - 7.9;
  [["Developer", "few false alarms, exact line → source-anchored findings"],
   ["Security reviewer", "why a path is flagged → rejected-check reasons, call graph"],
   ["CI owner", "SARIF, exit codes, determinism → GitHub Action"],
   ["Compliance officer", "code must not leave → fully offline, local model"],
   ["Supervisor / examiners", "honest evaluation → Unknown, recorded runs, ADRs"]].forEach(([h, d], i) => {
    card(s, x, 1.4 + i * 1.08, w, 0.95);
    txt(s, [{ text: h + "\n", options: { bold: true, color: HEX.accent1 } }, { text: d, options: { fontSize: 13 } }], { x: x + 0.18, y: 1.4 + i * 1.08, w: w - 0.3, h: 0.95, fontSize: 15, valign: "middle" });
  });
  s.addNotes("~35 s. Inner ring operators, then the business, then the wider environment including attackers as a negative stakeholder. Right side: one concrete requirement each stakeholder caused. Full table in ipd/02-stakeholders.");
}

// 6 Functional requirements
{
  const s = content("Functional requirements: 20 of 21 implemented", "Stakeholders and requirements");
  const fr = [
    ["FR01", "Load .sln/.slnx/.csproj", "done"], ["FR02", "Enumerate endpoints", "done"], ["FR03", "Global policy map", "done"],
    ["FR04", "EF ownership schema", "done"], ["FR05", "Resolve DI calls", "done"], ["FR06", "Trace ids to EF Core", "done"],
    ["FR07", "Classify EF operations", "done"], ["FR08", "Detect + bind guards", "done"], ["FR09", "Four-state verdict + rules", "done"],
    ["FR10", "Label-free IR slices", "done"], ["FR11", "Local ONNX SLM validation", "done"], ["FR12", "Fusion threshold τ", "partial"],
    ["FR13", "JSON + console report", "done"], ["FR14", "SARIF 2.1.0", "done"], ["FR15", "CI: exit codes + Action", "done"],
    ["FR16", "Visualise paths (UI)", "done"], ["FR17", "Evaluation harness", "done"], ["FR18", "Configuration file", "done"],
    ["FR19", "Inspect artefacts (CLI)", "done"], ["FR20", "Minimal APIs (Could)", "done"], ["FR21", "Robustness runner (Could)", "done"],
  ];
  const cw = (CW - 0.4) / 3;
  fr.forEach(([id, name, st], i) => tile(s, MX + (i % 3) * (cw + 0.2), 1.35 + Math.floor(i / 3) * 0.7, cw, 0.58, id, name, st));
  legend(s, MX, 6.3);
  txt(s, "FR12: τ = 0.80 is a default until it is calibrated on a separate split after the label freeze.", { x: MX + 7.9, y: 6.25, w: CW - 7.9, h: 0.5, fontSize: 12, color: C.text2 });
  s.addNotes("~45 s. MoSCoW priorities and PP IDs are in ipd/03-requirements. Only FR12 is partial: the threshold exists but is not calibrated yet — calibration on labelled data comes after the freeze, on purpose, to avoid tuning on the test set.");
}

// 7 NFRs
{
  const s = content("Non-functional requirements — measured, not claimed", "Stakeholders and requirements");
  const nfr = [
    ["NFR01", "Offline", "done", "hybrid scan inside a no-network namespace"], ["NFR02", "Performance", "partial", "symbolic 4 s – 6.4 min; SLM 108 s per candidate"],
    ["NFR03", "FP reduction measured", "done", "measured: Phi-3 changed 0 verdicts"], ["NFR04", "Explainability", "done", "path, guards, rejected-check reasons"],
    ["NFR05", "Reproducibility", "done", "commit, config + model hash per report"], ["NFR06", "Determinism", "done", "greedy: byte-identical model output"],
    ["NFR07", "Failure transparency", "done", "unresolved, truncation, parse failures"], ["NFR08", "16 GB CPU budget", "done", "symbolic ≤ 7.4 GB; Phi-3 hybrid 8.9 GB"],
    ["NFR09", "Windows + Linux", "partial", "Linux CI; Windows run in January"], ["NFR10", "Maintainability", "done", "9 projects, 197 tests, CI"],
    ["NFR11", "Tool safety", "done", "analysed code is parsed, never run"], ["NFR12", "Usable output", "done", "console + responsive UI"],
  ];
  const cw = (CW - 0.4) / 3;
  nfr.forEach(([id, name, st, note], i) => tile(s, MX + (i % 3) * (cw + 0.2), 1.35 + Math.floor(i / 3) * 1.15, cw, 1.0, id, name, st, note));
  legend(s, MX, 6.3);
  s.addNotes("~25 s. Each NFR has a measurement behind it — point at NFR01 (network-less run), NFR03 (we measured and the answer was 'no reduction'), NFR08 (memory). Two are partial: hybrid runtime on whole repositories and the Windows run.");
}

// 8 Use cases
{
  const s = content("Use cases — 4 actors, 18 use cases", "Stakeholders and requirements");
  img(s, D("use-cases.png"), MX, 1.25, CW, 5.65, 3400 / 2260, "UML use case diagram coloured by status");
  s.addNotes("~25 s. Main use case UC01 Scan solution includes context extraction, tracing and classification; UC06 (local SLM) extends it in hybrid mode and is partial because τ is uncalibrated. UC18 calibration and model comparison is pending. Full UC01 description in ipd/03-requirements.");
}

// 9 Architecture
pres.addSection({ title: "Architecture and design" });
{
  const s = content("Architecture: pipes and filters in a layered .NET solution", "Architecture and design");
  img(s, D("components.png"), MX, 1.3, 7.2, 5.5, 1568 / 1180, "Component diagram");
  const x = MX + 7.5, w = CW - 7.5;
  txt(s, "Technology choices", { x, y: 1.35, w, h: 0.4, fontSize: 18, bold: true, color: C.accent1 });
  bullets(s, [
    [{ text: "C# / .NET 8, Roslyn 4.14 ", options: { bold: true } }, { text: "— full semantic model of the analysed API" }],
    [{ text: "Own taint walker with entity tokens ", options: { bold: true } }, { text: "— \"same object\" reasoning EF-aware" }],
    [{ text: "ONNX Runtime GenAI, int4 CPU ", options: { bold: true } }, { text: "— local SLM, greedy, no network" }],
    [{ text: "Core has no Roslyn types ", options: { bold: true } }, { text: "— CLI, web UI and Action share one ScanService" }],
    [{ text: "No database ", options: { bold: true } }, { text: "— a static analyser never runs the target; results are reproducible files" }],
  ], { x, y: 1.9, w, h: 4.9, fontSize: 15 });
  s.addNotes("~45 s. Walk the diagram left to right: two front ends, one App service, three stages, a Core of plain records. Justify Roslyn (types and DI resolution) and the local model (compliance). If asked about a database: the tool does not run the analysed application; its own data are JSON files so that every result is reproducible from the repository.");
}

// 10 Core algorithm
{
  const s = content("Core algorithm: id taint → sink → guard → verdict", "Architecture and design");
  const steps = [
    ["Seed", "every id-like client parameter (route, query, body, nested, list) gets a taint token"],
    ["Walk", "through DI and MediatR, depth ≤ 5; a query turns the id into an entity token for that row"],
    ["Sink", "Update / Remove / SaveChanges on the token = state change (reads: secondary)"],
    ["Bind", "a guard counts only if it is on the same token, compares with the caller identity, is not permissive and dominates the change"],
  ];
  steps.forEach(([h, d], i) => {
    const y = 1.4 + i * 1.3;
    s.addShape(pres.shapes.OVAL, { x: MX, y: y + 0.08, w: 0.6, h: 0.6, fill: { color: C.accent1 }, line: { color: C.accent1 }, objectName: `alg-${i}` });
    txt(s, String(i + 1), { x: MX, y: y + 0.08, w: 0.6, h: 0.6, fontSize: 18, bold: true, color: C.background1, align: "center", valign: "middle" });
    txt(s, [{ text: h + "\n", options: { bold: true, fontSize: 16 } }, { text: d, options: { fontSize: 14, color: HEX.dk2 } }], { x: MX + 0.8, y, w: 4.9, h: 1.2 });
  });
  const rules = [
    ["SYM-03 / 04 / 05", "owner check · query filter · delegated access check", "Protected", HEX.accent3],
    ["SYM-06", "IgnoreQueryFilters, no valid guard", "Vulnerable", HEX.accent2],
    ["SYM-07", "detached or bulk write", "Vulnerable", HEX.accent2],
    ["SYM-08", "owned object changed, no valid guard", "Vulnerable", HEX.accent2],
    ["SYM-11", "membership object, no access check", "Vulnerable", HEX.accent2],
    ["SYM-02 / 09", "id lost in unresolved code · no owner model", "Unknown", HEX.accent4],
    ["SYM-01 / 10", "no state change · privileged-role endpoint", "Out of scope", HEX.accent5],
  ];
  const x = MX + 6.1, w = CW - 6.1;
  rules.forEach(([id, d, v, c], i) => {
    const y = 1.4 + i * 0.75;
    card(s, x, y, w, 0.65);
    txt(s, [{ text: id + "  ", options: { bold: true } }, { text: d, options: { fontSize: 13, color: HEX.dk2 } }], { x: x + 0.15, y, w: w - 1.95, h: 0.65, fontSize: 14, valign: "middle" });
    chip(s, x + w - 1.7, y + 0.15, 1.55, v, c);
  });
  s.addNotes("~1 min. Four steps on the left, then the rule table: first match wins, worst verdict per endpoint. Stress step 4 — binding — that is where BOLA lives: the check must be about the same row, compare with the caller (never a request value), not be permissive, and run on every path to the change (ADR-016). Decision flow diagram: ipd/diagrams/algorithm.png.");
}

// 11 Guard binding by example
{
  const s = content("Guard binding: one correct check, four ways to get it wrong", "Architecture and design");
  card(s, MX, 1.35, 6.0, 5.4);
  chip(s, MX + 0.3, 1.55, 1.6, "Protected", HEX.accent3);
  txt(s, "TC02 · SYM-03", { x: MX + 2.1, y: 1.55, w: 3.5, h: 0.34, fontSize: 14, bold: true, color: C.text2, valign: "middle" });
  s.addText([
    { text: "var order = await db.Orders\n    .FirstOrDefaultAsync(o => o.Id == id);\n" },
    { text: "if (order is null) return NotFound();\n" },
    { text: "if (order.UserId != currentUser.UserId)\n    return Forbid();\n", options: { bold: true, color: HEX.accent3 } },
    { text: "order.Status = request.Status;\nawait db.SaveChangesAsync();" },
  ], { isTextBox: true, x: MX + 0.3, y: 2.15, w: 5.5, h: 3.0, fontFace: "Courier New", fontSize: 14, color: C.text1, margin: 0, valign: "top" });
  txt(s, "Same row · caller identity · before the change · on every path → Protected", { x: MX + 0.3, y: 5.5, w: 5.5, h: 0.9, fontSize: 15, color: C.accent3, bold: true });
  const x = MX + 6.3, w = CW - 6.3;
  [["TC01", "no check at all", "SYM-08"], ["TC03", "compares with request.UserId — a client value, not the caller", "rejected"],
   ["TC04", "checks the source order, changes the target order", "wrong object"], ["TC05", "check runs after SaveChanges", "too late"]].forEach(([tc, d, r], i) => {
    const y = 1.35 + i * 1.37;
    card(s, x, y, w, 1.22);
    chip(s, x + 0.2, y + 0.18, 1.55, "Vulnerable", HEX.accent2);
    txt(s, `${tc} · ${r}`, { x: x + 1.95, y: y + 0.18, w: w - 2.1, h: 0.34, fontSize: 14, bold: true, valign: "middle" });
    txt(s, d, { x: x + 0.2, y: y + 0.62, w: w - 0.4, h: 0.55, fontSize: 14, color: C.text2 });
  });
  s.addNotes("~35 s. This is BolaLab, the development fixture (16 cases, labels only in expected.json). Left: the correct check. Right: four realistic mistakes the tool must tell apart — no check, comparing with a request value, checking the wrong object, checking too late. I will show these live in the UI.");
}

// 12 Design artefacts: sequence + data model
{
  const s = content("Design: core model and data organisation", "Architecture and design");
  img(s, D("class-core.png"), MX, 1.3, 8.4, 5.5, 1568 / 680, "Class diagram of the core model");
  const x = MX + 8.7, w = CW - 8.7;
  txt(s, "Data organisation", { x, y: 1.35, w, h: 0.4, fontSize: 18, bold: true, color: C.accent1 });
  bullets(s, [
    "ScanReport → Finding → ExecutionPath with steps, sinks and guards; symbolic + optional semantic verdict",
    "Inferred ownership schema: entities, keys, owner candidates, query filters, membership paths",
    "RunInfo: tool version, target commit, config hash, model SHA-256, prompt version",
    "Files, not a database: results stay reproducible from git",
  ], { x, y: 1.9, w, h: 4.9, fontSize: 14 });
  s.addNotes("~20 s. The core records and the validator abstraction (Null vs ONNX validator = the ablation switch). The sequence diagram is in Appendix E. The 'database design' of this project is the ownership schema BolaLens reconstructs from the analysed DbContext, plus the report model (ER diagram in ipd/diagrams/data-model.png).");
}

// 13 Hybrid stage
{
  const s = content("Stage 2: a local SLM as a second opinion — with guard rails", "Architecture and design");
  img(s, D("fusion.png"), MX, 1.3, CW, 2.75, 1568 / 352, "Semantic stage and conservative fusion decision flow");
  [["Label-free slice", "only the traced statements; comments, verdicts and rule IDs removed (no leakage)"],
   ["Calibration gate", "4 probe slices; SmolLM2 2/4 → advisory only, Phi-3-mini 3/4 → active"],
   ["Conservative fusion", "only a confident, grounded \"Protected\" can suppress a symbolic Vulnerable"]].forEach(([h, d], i) => {
    const x = MX + i * 4.1, w = 3.9;
    card(s, x, 4.35, w, 2.3);
    txt(s, h, { x: x + 0.2, y: 4.5, w: w - 0.4, h: 0.45, fontSize: 17, bold: true, color: C.accent1 });
    txt(s, d, { x: x + 0.2, y: 5.0, w: w - 0.4, h: 1.5, fontSize: 14 });
  });
  s.addNotes("~35 s. The model only sees candidates — Vulnerable, Unknown, or protected by a delegated check. It answers in a JSON schema with evidence line IDs that must exist in the slice. A model that fails the gate cannot change anything. And even a passing model may only suppress, never escalate (ADR-012) — because on BolaLab both models only added false positives when allowed to escalate.");
}

// 14 UI as built
{
  const s = content("UI design as built: local, offline, explainable", "Architecture and design");
  img(s, S("call-graph-desktop.png"), MX, 1.3, 8.3, 5.5, 2560 / 1800, "Web UI finding with call graph (desktop)");
  img(s, S("m2-findings.png"), MX + 8.6, 1.3, 3.5, 5.5, 1170 / 1992, "Web UI findings list on a phone");
  s.addNotes("~15 s. Screenshots of the real UI (Playwright, desktop and phone). Each finding shows the call graph controller → service → repository → sink, the checks found and why each was rejected. No CDN or external asset — it runs on 127.0.0.1. I will show it live in the demo.");
}

// 15 Testing + BolaLab
pres.addSection({ title: "Results" });
{
  const s = content("Testing: 197 automated tests, development set 16 / 16", "Results");
  stat(s, MX, 1.35, 2.85, "197", "tests: analysers, golden, pipeline, web");
  stat(s, MX, 3.05, 2.85, "16 / 16", "BolaLab cases with the expected rule ID", HEX.accent3);
  stat(s, MX, 4.75, 2.85, "1.000", "stability under renames + misleading comments (P1–P3)");
  const rows = [
    ["BolaLab arm (development set)", "TP", "FP", "FN", "TN", "Precision", "FPR"],
    ["Symbolic (Roslyn only)", "9", "0", "0", "6", "1.000", "0.000"],
    ["Hybrid SmolLM2, gate failed → advisory", "9", "0", "0", "6", "1.000", "0.000"],
    ["Hybrid SmolLM2, no gate", "9", "1", "0", "5", "0.900", "0.167"],
    ["Hybrid Phi-3-mini, escalation allowed", "9", "1", "0", "5", "0.900", "0.167"],
  ];
  const x = MX + 3.15, w = CW - 3.15;
  s.addTable(rows.map((r, i) => r.map((t, j) => ({ text: t, options: { bold: i === 0, align: j ? "center" : "left", color: i === 0 ? HEX.lt1 : HEX.dk1, fill: { color: i === 0 ? HEX.dk2 : (i % 2 ? HEX.lt2 : HEX.lt1) } } }))),
    { x, y: 1.4, w, colW: [4.0, 0.6, 0.6, 0.6, 0.6, 1.2, w - 7.6], fontSize: 14, fontFace: THEME.bodyFontFace, rowH: 0.5, valign: "middle", border: { type: "none" }, margin: [0.04, 0.1, 0.04, 0.1] });
  bullets(s, [
    "Tests first: every analyser pattern found on a real repository is reproduced as an in-memory compilation, then fixed",
    "The model rows motivated the calibration gate (ADR-009) and conservative fusion (ADR-012)",
    "BolaLab is the development set — never reported as generalisation",
  ], { x, y: 4.35, w, h: 2.4, fontSize: 14 });
  s.addNotes("~40 s. 197 tests: 103 analyser, 34 inference, 51 integration, 9 web. BolaLab is where the rules were developed, so 16/16 is expected — it proves correctness, not generalisation. The interesting rows are the model rows: they are why the gate and conservative fusion exist.");
}

// 16 Real CVEs
{
  const s = content("Real CVEs: BolaLens finds what CodeQL misses (preliminary)", "Results");
  s.addChart(pres.charts.BAR, [
    { name: "BolaLens symbolic", labels: ["Vulnerable units found (of 33)", "CVE pairs told apart (of 25)", "False positives on fixed commits"], values: [19, 13, 2] },
    { name: "CodeQL security-extended", labels: ["Vulnerable units found (of 33)", "CVE pairs told apart (of 25)", "False positives on fixed commits"], values: [0, 0, 0] },
  ], {
    x: MX, y: 1.3, w: 7.3, h: 5.5, barDir: "bar", barGrouping: "clustered", chartColors: [HEX.accent1, "9AA4B1"],
    showValue: true, dataLabelPosition: "outEnd", dataLabelFontSize: 13, dataLabelFontFace: "+mn-lt", dataLabelColor: HEX.dk1,
    catAxisLabelFontSize: 13, catAxisLabelFontFace: "+mn-lt", catAxisLabelColor: HEX.dk2, catAxisOrientation: "maxMin",
    valAxisHidden: true, valGridLine: { style: "none" }, catGridLine: { style: "none" }, valAxisMaxVal: 25,
    showLegend: true, legendPos: "b", legendFontSize: 13, legendFontFace: "+mn-lt", legendColor: HEX.dk2,
    showTitle: true, title: "14 published advisories · 5 projects · draft labels", titleFontSize: 14, titleFontFace: "+mn-lt", titleColor: HEX.dk2,
  });
  const x = MX + 7.6, w = CW - 7.6;
  stat(s, x, 1.35, (w - 0.2) / 2, "1.000", "precision, Dataset A", HEX.accent3);
  stat(s, x + (w + 0.2) / 2, 1.35, (w - 0.2) / 2, "0.731", "recall (Unknown excluded)");
  stat(s, x, 3.05, (w - 0.2) / 2, "0.091", "false-positive rate, Dataset C", HEX.accent2);
  stat(s, x + (w + 0.2) / 2, 3.05, (w - 0.2) / 2, "5", "honest Unknowns", HEX.accent4);
  txt(s, "Oqtane ×4 · Kavita ×3 · Jellyfin · Bitwarden ×5 · IoTSharp. Misses: creation-time BOLA, organisation commands, reads built outside EF, field-level exposure.", { x, y: 4.8, w, h: 1.9, fontSize: 14, color: C.text2 });
  s.addNotes("~1 min. Dataset A = the vulnerable commits of published advisories, Dataset C = the vendor's fixed commits. A pair counts only if flagged before and passed after the fix: 13 of 25. CodeQL's authorization queries look for a missing [Authorize] on an action, so they find none of these. Precision 1.000 on a small set with draft labels — I will verify every label before the freeze. Misses are explained in eval/results/error-analysis.md.");
}

// 17 RQ2
{
  const s = content("RQ2: the local SLM did not reduce false positives — yet", "Results");
  stat(s, MX, 1.35, 2.85, "0", "final verdicts changed by Phi-3-mini", HEX.accent2);
  stat(s, MX + 3.1, 1.35, 2.85, "29", "model calls on labelled units");
  stat(s, MX + 6.2, 1.35, 2.85, "108 s", "median latency per call (CPU)", HEX.accent4);
  stat(s, MX + 9.3, 1.35, 2.83, "8.9 GB", "peak memory (fits 16 GB)");
  const x = MX, w = CW;
  card(s, x, 3.2, w, 3.5);
  txt(s, "What happened", { x: x + 0.3, y: 3.35, w: 5.5, h: 0.4, fontSize: 17, bold: true, color: C.accent1 });
  bullets(s, [
    "9 disagreements — e.g. \"Vulnerable\" on three Kavita downloads protected by [ChapterAccess]",
    "6 parse failures → symbolic verdict kept, counted in coverage",
    "Conservative fusion kept every proven finding: hybrid = symbolic arm",
  ], { x: x + 0.3, y: 3.85, w: 5.6, h: 2.7, fontSize: 14 });
  txt(s, "What it means", { x: x + 6.3, y: 3.35, w: 5.5, h: 0.4, fontSize: 17, bold: true, color: C.accent1 });
  bullets(s, [
    "An honest negative result for one model on draft labels — reported, not hidden",
    "Today the model's value is the written rationale on Unknowns",
    "Next: Qwen2.5-Coder comparison and τ calibration after the label freeze",
  ], { x: x + 6.3, y: 3.85, w: 5.5, h: 2.7, fontSize: 14 });
  s.addNotes("~40 s. Research question 2 asks whether a local model reduces false positives. With Phi-3-mini on the Oqtane, Kavita and IoTSharp cases the answer so far is no: it changed nothing, and its disagreements were mostly wrong. The design worked as intended — the guard rails stopped it from adding errors. Bitwarden and Jellyfin need the 16 GB laptop (memory).");
}

// 18 Gantt
pres.addSection({ title: "Schedule and evaluation" });
{
  const s = content("Updated schedule: engineering ahead of plan", "Schedule and evaluation");
  img(s, D("gantt.png"), MX, 1.2, CW, 4.3, 1568 / 632, "Updated Gantt chart as of 24 November 2026");
  [["Ahead ~8 weeks", "engine, SLM stage, SARIF/Action/UI (planned to April)", HEX.accent3],
   ["Late", "ethics Part A — owner action this week", HEX.accent2],
   ["Critical path", "label verification → freeze 21 Mar → calibration → experiments", HEX.accent4]].forEach(([h, d, c], i) => {
    const x = MX + i * 4.1, w = 3.9;
    card(s, x, 5.6, w, 1.25);
    chip(s, x + 0.15, 5.72, 1.9, h, c);
    txt(s, d, { x: x + 0.15, y: 6.13, w: w - 0.3, h: 0.65, fontSize: 13, color: C.text2 });
  });
  s.addNotes("~40 s. Red line is today. Done items finished far earlier than the baseline (AI-assisted, logged). Ethics is late — say what you are doing about it. The critical path now runs through labels and writing, not code: label freeze 21 March, then calibration, final experiments, dissertation, viva 15 June. Baseline chart: ipd/diagrams/gantt-baseline-2026-11-18.png.");
}

// 19 Critical evaluation
{
  const s = content("Critical evaluation", "Schedule and evaluation");
  const cols = [
    ["Achievements", HEX.accent3, ["Working EF-aware analyser, 197 tests", "13/25 real CVE pairs; CodeQL 0", "Explainable, offline, reproducible runs"]],
    ["Challenges → solutions", HEX.accent1, ["Hidden checks (helpers, filters, permission services) → each reproduced as a test", "Same row loaded twice → entity-token aliasing", "Model errors → gate + conservative fusion"]],
    ["Limitations", HEX.accent2, ["Draft labels; Dataset B unlabelled", "Name-based owner heuristic (P4 stability 0.313)", "Creation-time and tenant BOLA; SLM adds no precision yet"]],
  ];
  cols.forEach(([h, c, items], i) => {
    const x = MX + i * 4.1, w = 3.9;
    card(s, x, 1.35, w, 4.3);
    chip(s, x + 0.2, 1.55, 2.6, h, c);
    bullets(s, items, { x: x + 0.2, y: 2.15, w: w - 0.4, h: 3.4, fontSize: 16 });
  });
  card(s, MX, 5.85, CW, 0.95, HEX.accent6);
  txt(s, [{ text: "On track? ", options: { bold: true } }, { text: "Yes for the software; the research depends on owner actions — ethics, labels with a second rater, the RQ4 study and the dissertation — scheduled Dec – Jun with the 21 Mar label freeze as the checkpoint." }],
    { x: MX + 0.25, y: 5.85, w: CW - 0.5, h: 0.95, fontSize: 14, valign: "middle" });
  s.addNotes("~50 s. Be self-critical: precision 1.000 is on a small set with draft labels; owner inference relies partly on names; the SLM has not helped yet. Then the judgement: software on track, research risk is labels and writing — and risk R6, understanding AI-assisted code, which I mitigate with the class guide and code walkthroughs.");
}

// 20 Demo
pres.addSection({ title: "Demo" });
{
  const s = pres.addSlide({ masterName: "SECTION_DARK", sectionTitle: "Demo" });
  s.addText("Live demo", { placeholder: "title" });
  s.addText("Repository and CI → BolaLab scan → UI call graph → code walkthrough → real CVE (Oqtane) → metrics → hybrid → robustness", { placeholder: "body" });
  s.addNotes("~8 min (slides 1–19 take ~12 min). Switch to the terminal. Follow ipd/08-demo/demo-script.md (8 minutes). If anything hangs more than 20 seconds, say so and use the recorded run in eval/runs.");
}

// ---------- appendix ----------
pres.addSection({ title: "Appendix" });
{
  const s = content("Appendix A — decision flow of the symbolic stage", "Appendix");
  img(s, D("algorithm-lr.png"), MX, 1.4, CW, 5.4, 1568 / 426, "Decision flow: taint, sink, guard, verdict rules");
  s.addNotes("Backup for questions on the rule order.");
}
{
  const s = content("Appendix B — experimental pipeline", "Appendix");
  img(s, D("evaluation-pipeline.png"), MX, 1.3, CW, 3.2, 1568 / 506, "Evaluation pipeline: datasets, runs, labels, CodeQL, metrics");
  bullets(s, [
    "Dataset A: 14 vulnerable commits (published advisories) · Dataset C: their fixed commits · Dataset B: 13 wild repositories, 3,423 endpoints",
    "Every case cloned at a pinned SHA; every run recorded with commit, config, time and memory (eval/runs)",
    "Precision = TP/(TP+FP) · Recall = TP/(TP+FN) · operational recall counts Unknowns as misses (19/33 = 0.576) · FPR = FP/(FP+TN)",
  ], { x: MX, y: 4.75, w: CW, h: 2.1, fontSize: 14 });
  s.addNotes("Backup for questions on datasets, labels and metric definitions.");
}
{
  const s = content("Appendix C — report and ownership data model", "Appendix");
  img(s, D("data-model-lr.png"), MX, 1.25, CW, 5.65, 1568 / 1220, "Entity-relationship diagram of the report model");
  s.addNotes("Backup for the 'database design' question.");
}
{
  const s = content("Appendix D — robustness (BolaLab)", "Appendix");
  const rows = [["Perturbation", "Preserves semantics", "Stability"],
    ["P1 rename every local variable", "yes", "1.000"], ["P2 misleading \"ownership verified\" comments", "yes", "1.000"],
    ["P3 rename every private method", "yes", "1.000"], ["P4 rename owner properties (UserId → opaque)", "no", "0.313"]];
  s.addTable(rows.map((r, i) => r.map((t, j) => ({ text: t, options: { bold: i === 0, align: j ? "center" : "left", color: i === 0 ? HEX.lt1 : HEX.dk1, fill: { color: i === 0 ? HEX.dk2 : (i % 2 ? HEX.lt2 : HEX.lt1) } } }))),
    { x: MX, y: 1.4, w: CW, colW: [7.0, 2.8, CW - 9.8], fontSize: 15, fontFace: THEME.bodyFontFace, rowH: 0.6, valign: "middle", border: { type: "none" }, margin: [0.05, 0.12, 0.05, 0.12] });
  txt(s, "P2 is stable because comments never reach the slices or the rules. P4 shows the dependence on owner-property names — mitigated by foreign-key evidence and configuration, and reported as a threat to validity.", { x: MX, y: 4.7, w: CW, h: 1.2, fontSize: 15, color: C.text2 });
  s.addNotes("Backup for robustness questions.");
}

{
  const s = content("Appendix E — sequence of a hybrid scan", "Appendix");
  img(s, D("sequence-scan.png"), MX, 1.3, CW, 5.5, 1568 / 664, "Sequence diagram of a hybrid scan");
  s.addNotes("Backup: order of calls in one scan — load, symbolic pipeline per endpoint and id, then slice → validate → fuse per candidate, report, exit code.");
}

(async () => {
  await pres.writeFile({ fileName: OUT });
  const themeModule = process.argv[2];
  if (themeModule) {
    const { applyTheme } = require(themeModule);
    await applyTheme(OUT, THEME);
  }
  console.log("written", OUT);
})();
