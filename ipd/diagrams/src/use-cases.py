"""Draws the BolaLens UML use case diagram as SVG (status-coloured). Usage: python3 use-cases.py out.svg
Same model as use-cases.puml, plus status colours: implemented / partial / pending (from docs/requirements.md)."""
import sys
from xml.sax.saxutils import escape

W, H = 1700, 1130
COLX = {"L": 600, "R": 1100}
RX, RY = 205, 38
ROW0, STEP = 130, 100
STATUS = {"done": ("#dff3e4", "#2e7d4f"), "partial": ("#fff1cc", "#b7791f"), "pending": ("#f1f1f4", "#6b7280")}

ucs = {  # id: (column, row, title, second line, status)
    "UC02": ("L", 0, "UC02 Configure analysis", "bolalens.json", "done"),
    "UC01": ("L", 1, "UC01 Scan solution", "symbolic or hybrid", "done"),
    "UC03": ("L", 2, "UC03 Extract context", "endpoints · policy · schema · DI", "done"),
    "UC04": ("L", 3, "UC04 Trace id-to-sink paths", "inter-procedural taint", "done"),
    "UC05": ("L", 4, "UC05 Classify paths", "rules SYM-01 … SYM-11", "done"),
    "UC07": ("L", 5, "UC07 View findings report", "console · JSON", "done"),
    "UC08": ("L", 6, "UC08 Inspect path evidence", "steps · guards · slice · spans", "done"),
    "UC13": ("L", 7, "UC13 Browse runs + call graph", "local web UI", "done"),
    "UC16": ("L", 8, "UC16 Export offline HTML report", "", "done"),
    "UC10": ("R", 0, "UC10 Gate pull request", "exit code · GitHub Action", "done"),
    "UC09": ("R", 1, "UC09 Export SARIF 2.1.0", "GitHub code scanning", "done"),
    "UC06": ("R", 2, "UC06 Validate with local SLM", "fusion threshold τ uncalibrated", "partial"),
    "UC12": ("R", 3, "UC12 Provision + pin local model", "ONNX int4 · SHA-256", "done"),
    "UC14": ("R", 4, "UC14 Run robustness perturbations", "P1–P4", "done"),
    "UC11": ("R", 5, "UC11 Run evaluation", "labels → metrics", "done"),
    "UC15": ("R", 6, "UC15 Compare with baseline SAST", "CodeQL SARIF arm", "done"),
    "UC17": ("R", 7, "UC17 Generate labelling worksheets", "blind, 2 raters", "done"),
    "UC18": ("R", 8, "UC18 Calibrate τ + compare models", "after label freeze (S8)", "pending"),
}

def centre(u):
    c, r = ucs[u][0], ucs[u][1]
    return COLX[c], ROW0 + r * STEP

actors = {"Developer": (130, 330), "Security reviewer": (130, 860), "CI pipeline": (1570, 230), "Researcher": (1570, 800)}
assoc = [("Developer", u) for u in ("UC02", "UC01", "UC07")] + \
        [("Security reviewer", u) for u in ("UC07", "UC08", "UC13", "UC16")] + \
        [("CI pipeline", u) for u in ("UC10", "UC09")] + \
        [("Researcher", u) for u in ("UC12", "UC14", "UC11", "UC15", "UC17", "UC18")]
# (from, to, stereotype, route): straight unless a bulge is given (negative = to the left)
rels = [("UC01", "UC03", "include", 0), ("UC01", "UC04", "include", -150), ("UC01", "UC05", "include", -230),
        ("UC13", "UC08", "include", 0), ("UC06", "UC12", "include", 0), ("UC15", "UC11", "extend", 0),
        ("UC06", "UC01", "extend", 0), ("UC10", "UC01", "include", 0), ("UC11", "UC01", "include", 0),
        ("UC14", "UC05", "include", 0), ("UC09", "UC07", "extend", 0)]

out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="Arial, Helvetica, sans-serif">',
       '<defs><marker id="open" markerWidth="12" markerHeight="12" refX="10" refY="5" orient="auto"><path d="M0,0 L10,5 L0,10" fill="none" stroke="#4a5a6a" stroke-width="1.6"/></marker></defs>',
       f'<rect width="{W}" height="{H}" fill="#ffffff"/>',
       '<text x="850" y="42" text-anchor="middle" font-size="28" font-weight="bold" fill="#13202e">BolaLens — use case diagram (status as of 24 Nov 2026)</text>',
       '<rect x="345" y="72" width="1010" height="960" rx="14" fill="#f7f9fc" stroke="#1f4e79" stroke-width="2"/>',
       '<text x="365" y="100" font-size="18" font-weight="bold" fill="#1f4e79">BolaLens</text>']

def edge(x, y, tx, ty):
    """Point on the ellipse around (x, y) towards (tx, ty)."""
    import math
    a = math.atan2(ty - y, tx - x)
    return x + RX * math.cos(a), y + RY * math.sin(a)

# associations (under the ellipses)
for a, u in assoc:
    ax, ay = actors[a]
    ux, uy = centre(u)
    sx = ax + (40 if ax < ux else -40)
    ex, ey = edge(ux, uy, sx, ay)
    out.append(f'<line x1="{sx}" y1="{ay - 20}" x2="{ex:.0f}" y2="{ey:.0f}" stroke="#4a5a6a" stroke-width="1.6"/>')

# include / extend
for f, t, kind, bulge in rels:
    fx, fy = centre(f)
    tx, ty = centre(t)
    if bulge:
        sx, sy = fx - RX + 10, fy + 10
        ex, ey = tx - RX + 10, ty - 10
        cx, cy = min(sx, ex) + bulge, (sy + ey) / 2
        path = f'M{sx:.0f},{sy:.0f} Q{cx:.0f},{cy:.0f} {ex:.0f},{ey:.0f}'
        lx, ly = 0.25 * sx + 0.5 * cx + 0.25 * ex + 34, 0.25 * sy + 0.5 * cy + 0.25 * ey
    else:
        sx, sy = edge(fx, fy, tx, ty)
        ex, ey = edge(tx, ty, fx, fy)
        path = f'M{sx:.0f},{sy:.0f} L{ex:.0f},{ey:.0f}'
        lx, ly = (sx + ex) / 2, (sy + ey) / 2
    out.append(f'<path d="{path}" fill="none" stroke="#4a5a6a" stroke-width="1.5" stroke-dasharray="7 5" marker-end="url(#open)"/>')
    label = f'«{kind}»' + (" [hybrid]" if (f, t) == ("UC06", "UC01") else "")
    out.append(f'<text x="{lx:.0f}" y="{ly + 4:.0f}" text-anchor="middle" font-size="12" font-style="italic" fill="#4a5a6a" '
               f'paint-order="stroke" stroke="#f7f9fc" stroke-width="4">{escape(label)}</text>')

# use cases
for u, (c, r, title, sub, st) in ucs.items():
    x, y = centre(u)
    fill, stroke = STATUS[st]
    out.append(f'<ellipse cx="{x}" cy="{y}" rx="{RX}" ry="{RY}" fill="{fill}" stroke="{stroke}" stroke-width="2"/>')
    dy = -4 if sub else 5
    out.append(f'<text x="{x}" y="{y + dy}" text-anchor="middle" font-size="15" font-weight="bold" fill="#13202e">{escape(title)}</text>')
    if sub:
        out.append(f'<text x="{x}" y="{y + 15}" text-anchor="middle" font-size="12.5" fill="#3d4b5a">{escape(sub)}</text>')

# actors (stick figures)
for name, (x, y) in actors.items():
    out.append(f'<g stroke="#13202e" stroke-width="2.4" fill="none"><circle cx="{x}" cy="{y - 62}" r="14" fill="#ffffff"/>'
               f'<line x1="{x}" y1="{y - 48}" x2="{x}" y2="{y - 8}"/><line x1="{x - 24}" y1="{y - 34}" x2="{x + 24}" y2="{y - 34}"/>'
               f'<line x1="{x}" y1="{y - 8}" x2="{x - 18}" y2="{y + 20}"/><line x1="{x}" y1="{y - 8}" x2="{x + 18}" y2="{y + 20}"/></g>')
    stereo = "«system»" if name == "CI pipeline" else ""
    out.append(f'<text x="{x}" y="{y + 44}" text-anchor="middle" font-size="16" font-weight="bold" fill="#13202e">{escape(name)}</text>')
    if stereo:
        out.append(f'<text x="{x}" y="{y + 62}" text-anchor="middle" font-size="12" fill="#4a5a6a">{stereo}</text>')

# legend
lx = 420
for i, (st, label) in enumerate([("done", "Implemented"), ("partial", "Partially implemented"), ("pending", "Pending (planned)")]):
    fill, stroke = STATUS[st]
    x = lx + i * 300
    out.append(f'<ellipse cx="{x}" cy="{H - 52}" rx="26" ry="13" fill="{fill}" stroke="{stroke}" stroke-width="2"/>'
               f'<text x="{x + 38}" y="{H - 47}" font-size="15" fill="#13202e">{label}</text>')
out.append('</svg>')
open(sys.argv[1] if len(sys.argv) > 1 else "use-cases.svg", "w", encoding="utf-8").write("\n".join(out))
