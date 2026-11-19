/* BolaLens UI — vanilla JS, no dependencies, works offline.
 * Two modes: (1) served by BolaLens.Web (API mode: scan, runs, eval) and (2) a self-contained exported
 * report (static mode: the report JSON is embedded in <script id="bolalens-data">).
 * Security: analysed source code is untrusted — every string is inserted as a text node, never as HTML. */
(() => {
  'use strict';

  const dataEl = document.getElementById('bolalens-data');
  const STATIC = !!dataEl;
  const S = { info: null, runs: [], runId: null, report: null, context: null, tab: 'findings', filter: 'all', search: '',
              selected: null, job: null, jobTimer: null, scanForm: { target: '', mode: 'symbolic', model: '', gate: true }, evalResult: null };

  // ------------------------------------------------------------ helpers
  function h(tag, attrs, ...children) {
    const el = document.createElement(tag);
    for (const [k, v] of Object.entries(attrs || {})) {
      if (k.startsWith('aria-') && typeof v === 'boolean') { el.setAttribute(k, String(v)); continue; } // aria-pressed="true"/"false"
      if (v === null || v === undefined || v === false) continue;
      if (k === 'class') el.className = v;
      else if (k.startsWith('on') && typeof v === 'function') el.addEventListener(k.slice(2), v);
      else if (k === 'dataset') Object.assign(el.dataset, v);
      else el.setAttribute(k, v === true ? '' : String(v));
    }
    for (const c of children.flat(Infinity)) {
      if (c === null || c === undefined || c === false) continue;
      el.append(c instanceof Node ? c : document.createTextNode(String(c)));
    }
    return el;
  }
  const statusClass = s => 's-' + String(s || 'unknown').toLowerCase();
  const pill = s => h('span', { class: 'pill ' + statusClass(s) }, s === 'OutOfScope' ? 'Out of scope' : s);
  const rank = s => ({ Vulnerable: 3, Unknown: 2, Protected: 1 })[s] || 0;
  const fmt = (n, d = 1) => (n === null || n === undefined) ? 'n/a' : Number(n).toFixed(d);
  const mb = b => (b / 1024 / 1024).toFixed(0) + ' MB';
  const where = r => r ? `${r.filePath}:${r.startLine}` : '';
  const date = s => { try { return new Date(s).toLocaleString(); } catch { return s; } };

  async function api(path, opts = {}) {
    const res = await fetch(path, { ...opts, headers: { 'X-BolaLens': '1', 'Content-Type': 'application/json', ...(opts.headers || {}) } });
    const text = await res.text();
    const body = text ? JSON.parse(text) : null;
    if (!res.ok) throw new Error((body && (body.error || body.title)) || res.statusText);
    return body;
  }

  function setMain(...nodes) {
    const main = document.getElementById('main');
    main.replaceChildren(...nodes);
  }

  function toast(msg) {
    const n = h('div', { class: 'notice', role: 'status', style: 'position:fixed;right:16px;bottom:16px;z-index:20;max-width:420px' }, msg);
    document.body.append(n);
    setTimeout(() => n.remove(), 5000);
  }

  // ------------------------------------------------------------ theme + nav
  function initTheme() {
    let saved = null;
    try { saved = localStorage.getItem('bolalens-theme'); } catch { /* storage may be blocked */ }
    if (saved) document.documentElement.dataset.theme = saved;
    document.getElementById('theme').addEventListener('click', () => {
      const dark = document.documentElement.dataset.theme === 'dark' ||
        (!document.documentElement.dataset.theme && matchMedia('(prefers-color-scheme: dark)').matches);
      const next = dark ? 'light' : 'dark';
      document.documentElement.dataset.theme = next;
      try { localStorage.setItem('bolalens-theme', next); } catch { /* ignore */ }
    });
  }

  function renderNav() {
    const nav = document.getElementById('nav');
    const route = location.hash || '#/';
    const link = (href, label) => h('a', { href, 'aria-current': route === href || (href !== '#/' && route.startsWith(href)) ? 'page' : null }, label);
    nav.replaceChildren(...(STATIC
      ? [h('span', { class: 'muted small' }, 'Exported report (read-only)')]
      : [link('#/', 'Dashboard'), link('#/runs', 'Runs'), link('#/about', 'How it works')]));
  }

  // ------------------------------------------------------------ router
  async function route() {
    renderNav();
    const hash = location.hash || '#/';
    try {
      if (STATIC) return renderRun();
      const m = hash.match(/^#\/runs\/([A-Za-z0-9._-]+)(?:\/(\w+))?/);
      if (m) {
        if (S.runId !== m[1]) await loadRun(m[1]);
        if (m[2]) S.tab = m[2];
        return renderRun();
      }
      if (hash.startsWith('#/runs')) return renderRuns();
      if (hash.startsWith('#/about')) return renderAbout();
      return renderDashboard();
    } catch (e) {
      setMain(h('div', { class: 'card' }, h('h2', {}, 'Something went wrong'), h('p', { class: 'muted' }, e.message), h('a', { class: 'btn', href: '#/' }, 'Back to dashboard')));
    }
  }

  async function loadRun(id) {
    S.runId = id; S.selected = null; S.filter = 'all'; S.search = ''; S.evalResult = null; S.tab = 'findings';
    setMain(h('p', { class: 'muted pad' }, 'Loading run…'));
    S.report = await api(`/api/runs/${encodeURIComponent(id)}`);
    S.context = (await api(`/api/runs/${encodeURIComponent(id)}/context`).catch(() => null)) || null;
  }

  // ------------------------------------------------------------ dashboard
  async function renderDashboard() {
    if (!S.info) S.info = await api('/api/info');
    S.runs = await api('/api/runs');
    const f = S.scanForm;
    if (!f.target && S.info.targets.length) f.target = S.info.targets[0];
    if (!f.model && S.info.models.length) f.model = S.info.models[0].path;

    const modeBtn = (mode, label) => h('button', { type: 'button', 'aria-pressed': f.mode === mode, onclick: () => { f.mode = mode; renderDashboard(); } }, label);
    const running = S.job && (S.job.status === 'queued' || S.job.status === 'running');

    const form = h('form', { class: 'card stack', onsubmit: e => { e.preventDefault(); startScan(); } },
      h('h2', {}, 'New scan'),
      h('label', { class: 'field' }, 'Solution or project (.sln, .slnx, .csproj)',
        h('input', { type: 'text', list: 'targets', value: f.target, required: true, oninput: e => { f.target = e.target.value; }, placeholder: 'fixtures/BolaLab/BolaLab.sln' }),
        h('datalist', { id: 'targets' }, S.info.targets.map(t => h('option', { value: t })))),
      h('div', { class: 'field' }, h('span', {}, 'Mode'),
        h('div', { class: 'seg', role: 'group', 'aria-label': 'Scan mode' }, modeBtn('symbolic', 'Symbolic (Roslyn only)'), modeBtn('hybrid', 'Hybrid (+ local SLM)'))),
      f.mode === 'hybrid' ? h('label', { class: 'field' }, 'Local model',
        S.info.models.length
          ? h('select', { onchange: e => { f.model = e.target.value; } }, S.info.models.map(m => h('option', { value: m.path, selected: m.path === f.model }, `${m.id} — ${fmt(m.sizeMb, 0)} MB · ${m.chatFormat}`)))
          : h('span', { class: 'notice' }, 'No local model found under models/. Run scripts/get-model-smollm2.sh once.')) : null,
      f.mode === 'hybrid' ? h('label', { class: 'check' }, h('input', { type: 'checkbox', checked: f.gate, onchange: e => { f.gate = e.target.checked; } }),
        'Calibration gate (a model that fails the labelled probes is advisory only)') : null,
      h('div', { class: 'row' },
        h('button', { class: 'btn primary', type: 'submit', disabled: running || (f.mode === 'hybrid' && !S.info.models.length) }, running ? 'Scanning…' : 'Run scan'),
        h('span', { class: 'muted small' }, 'Runs locally; no code leaves this machine.')));

    const job = S.job ? h('section', { class: 'card stack', 'aria-live': 'polite' },
      h('div', { class: 'row' }, h('h2', { style: 'margin:0' }, 'Scan progress'), h('span', { class: 'spacer' }),
        running ? h('span', { class: 'spinner', 'aria-label': 'running' }) : h('span', { class: 'pill ' + (S.job.status === 'done' ? 's-protected' : 's-vulnerable') }, S.job.status)),
      h('div', { class: 'log' }, (S.job.log || []).join('\n') || 'Queued…'),
      S.job.error ? h('div', { class: 'notice' }, S.job.error) : null,
      S.job.runId ? h('a', { class: 'btn primary', href: `#/runs/${S.job.runId}` }, 'Open results →') : null)
      : h('section', { class: 'card stack' }, h('h2', {}, 'What BolaLens checks'),
          h('p', { class: 'muted', style: 'margin:0' }, 'Follows each client-controlled id from an ASP.NET Core endpoint through services and repositories to EF Core updates/deletes, and checks that the change is guarded by the caller’s identity on the same object (owner check, owner predicate, query filter or resource authorization).'),
          h('div', { class: 'row' }, pill('Vulnerable'), pill('Protected'), pill('Unknown'), pill('OutOfScope')));

    setMain(
      h('div', { class: 'page-head' }, h('div', {}, h('h1', {}, 'Dashboard'), h('div', { class: 'muted small' }, `BolaLens ${S.info.version} · working folder ${S.info.cwd}`))),
      h('div', { class: 'grid grid-2' }, form, job),
      h('div', { class: 'section' }, h('div', { class: 'row' }, h('h2', { style: 'margin:0;font-size:16px' }, 'Recent runs'), h('span', { class: 'spacer' }), h('a', { href: '#/runs' }, 'All runs →'))),
      runsTable(S.runs.slice(0, 6)));
  }

  function runsTable(runs) {
    if (!runs.length) return h('div', { class: 'card empty' }, 'No runs yet — start a scan above.');
    return h('div', { class: 'card table-wrap' }, h('table', { class: 'responsive' },
      h('thead', {}, h('tr', {}, ['Date', 'Target', 'Mode', 'Model', 'Vulnerable endpoints', 'Time', ''].map(t => h('th', {}, t)))),
      h('tbody', {}, runs.map(r => h('tr', {},
        h('td', { 'data-label': 'Date' }, date(r.startedUtc)),
        h('td', { 'data-label': 'Target', class: 'mono' }, r.target),
        h('td', { 'data-label': 'Mode' }, h('span', { class: 'tag' }, r.mode)),
        h('td', { 'data-label': 'Model' }, r.modelId || '—'),
        h('td', { 'data-label': 'Vulnerable' }, h('span', { class: r.vulnerable ? 'bad' : 'ok' }, `${r.vulnerable} / ${r.endpoints}`)),
        h('td', { 'data-label': 'Time' }, fmt(r.totalSeconds) + ' s'),
        h('td', { 'data-label': '' }, h('div', { class: 'row' },
          h('a', { class: 'btn small', href: `#/runs/${r.id}` }, 'Open'),
          r.readOnly ? h('span', { class: 'tag', title: 'Recorded evaluation run (read-only)' }, 'recorded') :
            h('button', { class: 'btn small danger', type: 'button', onclick: () => deleteRun(r.id) }, 'Delete'))))))));
  }

  async function renderRuns() {
    S.runs = await api('/api/runs');
    setMain(h('div', { class: 'page-head' }, h('h1', {}, 'Runs'), h('a', { class: 'btn primary', href: '#/' }, 'New scan')), runsTable(S.runs));
  }

  async function deleteRun(id) {
    if (!confirm(`Delete run ${id}?`)) return;
    await api(`/api/runs/${encodeURIComponent(id)}`, { method: 'DELETE' });
    route();
  }

  async function startScan() {
    const f = S.scanForm;
    try {
      const { jobId } = await api('/api/scans', { method: 'POST', body: JSON.stringify({ target: f.target, mode: f.mode, model: f.mode === 'hybrid' ? f.model : null, gate: f.gate }) });
      S.job = { id: jobId, status: 'queued', log: [] };
      renderDashboard();
      pollJob();
    } catch (e) { toast(e.message); }
  }

  async function pollJob() {
    clearTimeout(S.jobTimer);
    if (!S.job) return;
    try { S.job = await api(`/api/scans/${S.job.id}`); } catch (e) { S.job.error = e.message; S.job.status = 'failed'; }
    if ((location.hash || '#/') === '#/') renderDashboard();
    if (S.job.status === 'queued' || S.job.status === 'running') S.jobTimer = setTimeout(pollJob, 1000);
  }

  // ------------------------------------------------------------ run view
  function endpointGroups(findings) {
    const map = new Map();
    for (const f of findings) {
      const key = f.path.endpoint.id;
      if (!map.has(key)) map.set(key, []);
      map.get(key).push(f);
    }
    return [...map.entries()].map(([id, fs]) => ({ id, findings: fs, worst: fs.reduce((a, b) => rank(b.final) > rank(a.final) ? b : a) }));
  }

  function renderRun() {
    const r = S.report;
    const run = r.run, c = r.coverage;
    const groups = endpointGroups(r.findings);
    const count = s => groups.filter(g => g.worst.final === s).length;
    const tabs = [['findings', 'Findings'], ['endpoints', 'Endpoints'], ['policy', 'Policy'], ['schema', 'Ownership schema'], ['di', 'DI map'],
                  ...(STATIC ? [] : [['eval', 'Evaluate']]), ['run', 'Run info']]
      .filter(([k]) => k === 'findings' || k === 'run' || k === 'eval' || S.context);
    if (!tabs.some(([k]) => k === S.tab)) S.tab = 'findings';

    const gate = run.modelGate;
    const head = h('div', { class: 'page-head' },
      h('div', {},
        STATIC ? null : h('div', { class: 'crumbs' }, h('a', { href: '#/runs' }, 'Runs'), ' / ', S.runId),
        h('h1', {}, run.targetPath),
        h('div', { class: 'row small muted' }, h('span', { class: 'tag' }, run.mode), run.modelId ? `model ${run.modelId}` : 'Roslyn only',
          gate ? h('span', { class: 'pill ' + (gate.passed ? 's-protected' : 's-unknown') }, `gate ${gate.passed ? 'passed' : 'failed — advisory'} ${gate.correct}/${gate.total}`) : null,
          `· ${date(run.startedUtc)}`)),
      STATIC ? null : h('div', { class: 'row' }, h('a', { class: 'btn', href: `/api/runs/${encodeURIComponent(S.runId)}/export`, download: true }, '⬇ Offline HTML report'),
        h('a', { class: 'btn', href: `/api/runs/${encodeURIComponent(S.runId)}`, download: `${S.runId}.json` }, '⬇ report.json')));

    const kpi = (v, l, cls) => h('div', { class: 'kpi ' + (cls || '') }, h('div', { class: 'v' }, v), h('div', { class: 'l' }, l));
    const kpis = h('div', { class: 'kpis' },
      kpi(c.endpoints, 'Endpoints'), kpi(count('Vulnerable'), 'Vulnerable endpoints', 'vuln'), kpi(count('Protected'), 'Protected', 'prot'),
      kpi(count('Unknown'), 'Unknown', 'unk'), kpi(c.pathsExtracted, 'Paths traced'), kpi(fmt(run.totalSeconds) + ' s', `${mb(run.peakWorkingSetBytes)} peak`));

    const tabBar = h('div', { class: 'tabs', role: 'tablist' }, tabs.map(([k, label]) =>
      h('button', { role: 'tab', type: 'button', 'aria-selected': S.tab === k, onclick: () => { S.tab = k; if (!STATIC) history.replaceState(null, '', `#/runs/${S.runId}/${k}`); renderRun(); } }, label)));

    const body = ({ findings: findingsTab, endpoints: endpointsTab, policy: policyTab, schema: schemaTab, di: diTab, eval: evalTab, run: runTab })[S.tab](groups);
    setMain(head, kpis, tabBar, body);
  }

  // ---- findings
  function findingsTab(groups) {
    const q = S.search.toLowerCase();
    const shown = groups
      .filter(g => S.filter === 'all' || g.worst.final === S.filter)
      .filter(g => !q || g.id.toLowerCase().includes(q) || g.findings.some(f => f.path.endpoint.action.toLowerCase().includes(q) || f.symbolic.ruleIds.join(' ').toLowerCase().includes(q)))
      .sort((a, b) => rank(b.worst.final) - rank(a.worst.final) || a.id.localeCompare(b.id));
    const chip = (k, label) => h('button', { class: 'chip', type: 'button', 'aria-pressed': S.filter === k, onclick: () => { S.filter = k; renderRun(); } }, label);

    const master = h('div', { class: 'master' },
      h('div', { class: 'filters' }, chip('all', 'All'), chip('Vulnerable', 'Vulnerable'), chip('Unknown', 'Unknown'), chip('Protected', 'Protected'), chip('OutOfScope', 'Out of scope')),
      h('input', { type: 'search', placeholder: 'Search route, action or rule…', value: S.search, 'aria-label': 'Search findings',
        oninput: e => { S.search = e.target.value; const pos = e.target.selectionStart; renderRun(); const s = document.querySelector('input[type=search]'); s.focus(); s.setSelectionRange(pos, pos); } }),
      h('div', { class: 'list', style: 'margin-top:10px' }, shown.length ? shown.map(g => {
        const f = g.worst;
        return h('button', { class: 'item', type: 'button', 'aria-current': S.selected === f.findingId,
          onclick: () => { S.selected = f.findingId; renderRun(); if (matchMedia('(max-width: 720px)').matches) scrollTo(0, 0); } },
          h('div', { class: 'row' }, h('span', { class: 'verb' }, f.path.endpoint.verb.toUpperCase()), pill(f.final), h('span', { class: 'spacer' }), h('span', { class: 'tag' }, f.symbolic.ruleIds.join(', '))),
          h('div', { class: 'route' }, g.id.replace(/^\S+ /, '')),
          h('div', { class: 'muted small' }, `${f.path.endpoint.controller}.${f.path.endpoint.action}` + (g.findings.length > 1 ? ` · ${g.findings.length} paths` : '')));
      }) : h('div', { class: 'empty' }, 'No findings match.')));

    const sel = S.report.findings.find(f => f.findingId === S.selected);
    const detail = h('div', { class: 'detail card' }, sel ? findingDetail(sel, groups) : h('div', { class: 'empty' }, 'Select an endpoint to see the traced path, guards and the model’s view.'));
    return h('div', { class: 'md' + (sel ? ' has-selection' : '') }, master, detail);
  }

  // ---- call graph (RQ4): endpoint → service → repository → EF Core sink, built from the traced path's steps
  const SVGNS = 'http://www.w3.org/2000/svg';
  function svg(tag, attrs, text) {
    const el = document.createElementNS(SVGNS, tag);
    for (const [k, v] of Object.entries(attrs || {})) if (v !== null && v !== undefined) el.setAttribute(k, String(v));
    if (text !== undefined) el.textContent = String(text); // text node only: analysed code is untrusted
    return el;
  }
  const shortSymbol = sym => {
    const noArgs = String(sym || '').replace(/\(.*$/, '').replace(/<.*?>/g, '');
    return noArgs.split('.').slice(-2).join('.') || String(sym || '?');
  };
  const sameRef = (a, b) => a && b && a.filePath === b.filePath && a.startLine === b.startLine;

  function graphModel(f) {
    const ep = f.path.endpoint;
    const root = { label: `${ep.controller}.${ep.action}`, depth: 0, kind: 'method', loads: 0, checks: 0, unresolved: 0, count: 1, children: [], loc: ep.location };
    const stack = [root];
    for (const s of f.path.steps) {
      const d = Math.min(s.depth || 0, stack.length - 1);
      const parent = stack[d];
      if (s.kind === 'Call') {
        const label = shortSymbol(s.symbol || s.description.replace(/^calls /, ''));
        let n = parent.children.find(c => c.kind === 'method' && c.label === label);
        if (n) n.count++;
        else { n = { label, depth: d + 1, kind: 'method', loads: 0, checks: 0, unresolved: 0, count: 1, children: [], loc: s.location }; parent.children.push(n); }
        stack.length = d + 1; stack.push(n);
      } else if (s.kind === 'Sink') {
        const sink = f.path.sinks.find(k => sameRef(k.location, s.location));
        const guarded = !!(sink && sink.guardIndices && sink.guardIndices.length);
        parent.children.push({ label: s.description.replace(/^state change: /, ''), depth: d + 1, kind: guarded ? 'guarded' : 'sink', children: [], loc: s.location });
      } else if (s.kind === 'Load') parent.loads++;
      else if (s.kind === 'Check') parent.checks++;
      else if (s.kind === 'Unresolved') parent.unresolved++;
    }
    return root;
  }

  function callGraph(f) {
    const rows = [];
    (function walk(n) { rows.push(n); n.children.forEach(walk); })(graphModel(f));
    const ROW = 46, H = 32, IND = 30, CH = 7.4;
    rows.forEach((n, i) => {
      n.y = 8 + i * ROW; n.x = 8 + n.depth * IND;
      const extra = n.kind === 'method' ? [n.loads ? `${n.loads} load` : '', n.checks ? `${n.checks} check` : '', n.unresolved ? `${n.unresolved} unresolved` : '', n.count > 1 ? `×${n.count}` : ''].filter(Boolean).join(' · ') : '';
      n.badge = extra;
      n.w = Math.max(n.label.length, extra.length) * CH + 24;
    });
    const width = Math.ceil(Math.max(...rows.map(n => n.x + n.w)) + 8);
    const height = rows.length * ROW + 8;
    const root = svg('svg', { class: 'cg', viewBox: `0 0 ${width} ${height}`, width, height, role: 'img',
      'aria-label': `Call graph with ${rows.length} nodes from ${rows[0].label}` });
    const parentOf = new Map();
    rows.forEach(n => n.children.forEach(c => parentOf.set(c, n)));
    for (const n of rows) {
      const p = parentOf.get(n);
      if (p) root.append(svg('path', { class: 'cg-edge' + (n.kind === 'sink' ? ' to-sink' : ''), d: `M${p.x + 14} ${p.y + H} V${n.y + H / 2} H${n.x}` }));
    }
    for (const n of rows) {
      const g = svg('g', { class: 'cg-node cg-' + n.kind, transform: `translate(${n.x} ${n.y})` });
      g.append(svg('title', {}, `${n.label}${n.loc ? ' — ' + where(n.loc) : ''}`));
      g.append(svg('rect', { width: n.w, height: H, rx: n.kind === 'method' ? 7 : 15 }));
      g.append(svg('text', { x: 12, y: n.badge ? 14 : 20, class: 'cg-label' }, n.label));
      if (n.badge) g.append(svg('text', { x: 12, y: 27, class: 'cg-badge' }, n.badge));
      root.append(g);
    }
    return h('div', { class: 'section' }, h('h3', {}, 'Call graph'),
      h('div', { class: 'cg-wrap', tabindex: '0', 'aria-label': 'Call graph (scrolls sideways on small screens)' }, root),
      h('div', { class: 'cg-legend muted small' },
        h('span', { class: 'lg lg-method' }, 'method on the path'), h('span', { class: 'lg lg-sink' }, 'state change without a valid guard'),
        h('span', { class: 'lg lg-guarded' }, 'state change covered by a valid guard')));
  }

  function findingDetail(f, groups) {
    const ep = f.path.endpoint;
    const siblings = groups.find(g => g.id === ep.id).findings;
    const ev = new Set((f.semantic && f.semantic.evidenceLineIds) || []);
    return h('div', {},
      h('button', { class: 'btn small back', type: 'button', onclick: () => { S.selected = null; renderRun(); } }, '← Back to list'),
      h('div', { class: 'row' }, h('span', { class: 'verb' }, ep.verb.toUpperCase()), h('strong', { class: 'mono' }, ep.routeTemplate), h('span', { class: 'spacer' }), pill(f.final)),
      h('div', { class: 'muted small' }, `${ep.controller}.${ep.action} · ${where(ep.location)} · finding ${f.findingId}`),
      siblings.length > 1 ? h('div', { class: 'filters', style: 'margin-top:10px' }, siblings.map(s =>
        h('button', { class: 'chip', type: 'button', 'aria-pressed': s.findingId === f.findingId, onclick: () => { S.selected = s.findingId; renderRun(); } },
          `via ${s.path.taintSource.name}: ${s.final}`))) : null,

      h('div', { class: 'section' }, h('h3', {}, 'Verdict'),
        h('div', { class: 'rationale' },
          h('div', { class: 'row' }, h('strong', {}, 'Symbolic'), pill(f.symbolic.status), h('span', { class: 'tag' }, f.symbolic.ruleIds.join(', '))),
          h('p', { style: 'margin:6px 0 0' }, f.symbolic.rationale)),
        f.semantic ? h('div', { class: 'rationale', style: 'margin-top:8px' },
          h('div', { class: 'row' }, h('strong', {}, 'Local SLM'), pill(f.semantic.status), h('span', { class: 'muted small' },
            `confidence ${fmt(f.semantic.confidence, 2)} · ${f.semantic.latencyMs} ms · ${f.semantic.modelId}${f.semantic.parseOk ? '' : ' · parse failed'}`)),
          h('p', { style: 'margin:6px 0 0' }, f.semantic.reason),
          ev.size ? h('div', { class: 'muted small' }, 'Evidence lines: ' + [...ev].join(', ')) : null) : null,
        h('p', { class: 'muted small' }, 'Final: ', f.finalReason)),

      callGraph(f),

      h('div', { class: 'section' }, h('h3', {}, `Traced path from “${f.path.taintSource.name}” (${f.path.taintSource.source.toLowerCase()})`),
        h('ol', { class: 'steps' }, f.path.steps.map(s => h('li', { class: 'k-' + s.kind.toLowerCase(), style: `margin-left:${(s.depth || 0) * 16}px` },
          h('div', { class: 'row' }, h('span', { class: 'tag' }, s.kind), h('span', {}, s.description)),
          s.location && s.location.snippet ? h('code', { class: 'snippet' }, s.location.snippet) : null,
          h('div', { class: 'loc' }, where(s.location)))))),

      f.path.guards.length ? h('div', { class: 'section' }, h('h3', {}, 'Checks found'),
        h('div', { class: 'table-wrap' }, h('table', { class: 'responsive' },
          h('thead', {}, h('tr', {}, ['Kind', 'Valid', 'Owner property', 'Compared with', 'Why not', 'Where'].map(t => h('th', {}, t)))),
          h('tbody', {}, f.path.guards.map(g => h('tr', {},
            h('td', { 'data-label': 'Kind' }, g.kind),
            h('td', { 'data-label': 'Valid' }, g.isValid ? h('span', { class: 'ok' }, '✓ valid') : h('span', { class: 'bad' }, '✗ rejected')),
            h('td', { 'data-label': 'Owner', class: 'mono' }, g.ownerProperty || '—'),
            h('td', { 'data-label': 'Compared', class: 'mono' }, g.callerExpression || '—'),
            h('td', { 'data-label': 'Why not' }, g.invalidReason || ''),
            h('td', { 'data-label': 'Where', class: 'small' }, where(g.location)))))))) : null,

      f.path.sinks.length ? h('div', { class: 'section' }, h('h3', {}, 'State changes (EF Core sinks)'),
        h('ul', {}, f.path.sinks.map(s => h('li', {}, h('strong', {}, s.kind), ` on ${s.entityType.split('.').pop()} at ${where(s.location)}`,
          s.bypassesQueryFilters ? ' · IgnoreQueryFilters' : '', s.isDetached ? ' · detached' : '', s.queryHasOwnerPredicate ? ' · owner predicate' : '')))) : null,

      f.path.unresolved.length ? h('div', { class: 'section' }, h('h3', {}, 'Unresolved'),
        h('ul', {}, f.path.unresolved.map(u => h('li', {}, u.reason, u.location ? h('span', { class: 'muted small' }, ` (${where(u.location)})`) : null)))) : null,

      f.slice ? h('div', { class: 'section' }, h('h3', {}, 'IR slice sent to the model (label-free)'),
        h('div', { class: 'slice', role: 'region', 'aria-label': 'IR slice' }, f.slice.split('\n').map(line => {
          const m = line.match(/^(L\d+)\s+\[([A-Z]+)\]\s?(.*)$/);
          if (!m) return h('div', { class: 'ln' }, h('span', {}), h('span', {}), h('span', { class: 'tx' }, line));
          return h('div', { class: 'ln' + (ev.has(m[1]) ? ' ev' : '') }, h('span', { class: 'id' }, m[1]), h('span', { class: 'tg' }, `[${m[2]}]`), h('span', { class: 'tx' }, m[3]));
        }))) : null);
  }

  // ---- context tabs
  function endpointsTab() {
    const eps = S.context.endpoints;
    return h('div', { class: 'card table-wrap' }, h('table', { class: 'responsive' },
      h('thead', {}, h('tr', {}, ['Verb', 'Route', 'Action', 'Authorization', 'Parameters'].map(t => h('th', {}, t)))),
      h('tbody', {}, eps.map(e => h('tr', {},
        h('td', { 'data-label': 'Verb' }, h('span', { class: 'verb' }, e.verb.toUpperCase())),
        h('td', { 'data-label': 'Route', class: 'mono' }, e.routeTemplate),
        h('td', { 'data-label': 'Action' }, `${e.controller}.${e.action}`),
        h('td', { 'data-label': 'Auth' }, e.authorization.allowAnonymous ? h('span', { class: 'bad' }, 'AllowAnonymous') : e.authorization.hasAuthorize ? '[Authorize]' : 'none',
          e.authorization.policies.length ? ` policy ${e.authorization.policies.join(',')}` : '', e.authorization.roles.length ? ` roles ${e.authorization.roles.join(',')}` : ''),
        h('td', { 'data-label': 'Params' }, e.parameters.map(p => h('div', { class: 'small' }, h('span', { class: 'mono' }, `${p.name}: ${p.type}`), ` · ${p.source.toLowerCase()}`, p.isIdLike ? h('span', { class: 'tag', style: 'margin-left:6px' }, 'id-like') : null))))))));
  }

  function policyTab() {
    const p = S.context.policy;
    return h('div', { class: 'grid grid-2' },
      h('div', { class: 'card' }, h('h2', {}, 'Global configuration'),
        h('dl', { class: 'kv' },
          h('dt', {}, 'Authentication schemes'), h('dd', {}, p.authenticationSchemes.join(', ') || 'none found'),
          h('dt', {}, 'Fallback policy'), h('dd', {}, p.fallbackRequiresAuthenticatedUser ? h('span', { class: 'ok' }, 'requires authenticated user') : h('span', { class: 'bad' }, 'none')),
          h('dt', {}, 'Global authorize filter'), h('dd', {}, p.globalAuthorizeFilter ? 'yes' : 'no')),
        h('p', { class: 'muted small' }, 'Global policies prove coarse protection (authenticated user) — they almost never prove object ownership.')),
      h('div', { class: 'card' }, h('h2', {}, 'Named policies & resource handlers'),
        p.policies.length ? h('ul', {}, p.policies.map(x => h('li', {}, h('strong', {}, x.name), ': ', x.requirements.join(', ') || '—'))) : h('p', { class: 'muted' }, 'No named policies.'),
        p.resourceHandlers.length ? h('ul', {}, p.resourceHandlers.map(x => h('li', {}, h('strong', {}, x.handlerType), ` (${x.requirementType} → ${x.resourceType})`))) : null,
        p.unresolved.length ? h('div', { class: 'notice' }, p.unresolved.map(u => h('div', {}, u.reason))) : null));
  }

  function schemaTab() {
    const s = S.context.schema;
    return h('div', { class: 'entity-grid' }, s.entities.map(e => h('div', { class: 'card' },
      h('div', { class: 'row' }, h('h3', { style: 'margin:0' }, e.entityType.split('.').pop()), h('span', { class: 'spacer' }),
        e.ownerCandidates.length ? h('span', { class: 'tag' }, `needs ${e.ownerCandidates.some(o => o.kind === 'User') ? 'User' : 'Tenant'}-level check`) : h('span', { class: 'pill s-unknown' }, 'no owner')),
      h('div', { class: 'muted small' }, `${e.dbContext}.${e.dbSetName || '?'} · key ${e.keyProperties.join(', ') || '?'}`),
      h('div', { class: 'section' }, h('h3', {}, 'Owner candidates'),
        e.ownerCandidates.length ? h('ul', {}, e.ownerCandidates.map(o => h('li', {}, h('span', { class: 'mono' }, o.property), ` (${o.kind}) — ${o.evidence.join(', ')}`))) : h('p', { class: 'muted' }, 'none')),
      h('div', { class: 'section' }, h('h3', {}, 'Query filters'),
        e.queryFilters.length ? e.queryFilters.map(q => h('div', {}, h('code', {}, q.expression), ` [${q.scopeKind}]`)) : h('p', { class: 'muted' }, 'none')))));
  }

  function diTab() {
    const entries = Object.entries(S.context.diMap);
    return h('div', { class: 'card table-wrap' }, h('table', { class: 'responsive' },
      h('thead', {}, h('tr', {}, ['Service', 'Implementation(s)'].map(t => h('th', {}, t)))),
      h('tbody', {}, entries.map(([svc, impls]) => h('tr', {},
        h('td', { 'data-label': 'Service', class: 'mono' }, svc),
        h('td', { 'data-label': 'Impl' }, impls.map(i => h('div', { class: 'mono small' }, i)), impls.length > 1 ? h('span', { class: 'pill s-unknown' }, 'ambiguous dispatch') : null))))));
  }

  // ---- evaluation
  function evalTab() {
    const guess = (S.info && S.info.defaultLabels && S.info.defaultLabels[S.report.run.targetPath]) || 'fixtures/BolaLab/expected.json';
    let labels = S.evalLabels || guess;
    const form = h('form', { class: 'card row', onsubmit: async e => {
      e.preventDefault();
      try { S.evalResult = await api(`/api/runs/${encodeURIComponent(S.runId)}/eval`, { method: 'POST', body: JSON.stringify({ labels }) }); S.evalLabels = labels; renderRun(); }
      catch (err) { toast(err.message); } } },
      h('label', { class: 'field', style: 'flex:1;min-width:240px' }, 'Labels file (frozen ground truth, JSON)',
        h('input', { type: 'text', value: labels, oninput: e => { labels = e.target.value; } })),
      h('button', { class: 'btn primary', type: 'submit', style: 'align-self:flex-end' }, 'Evaluate'));
    const res = S.evalResult;
    if (!res) return h('div', { class: 'stack' }, form, h('div', { class: 'card muted' }, 'Compares this run with labels: confusion matrix, precision, recall, F1, FPR and FDR. A hybrid run gives two arms on the same candidates (symbolic-only vs hybrid). Undefined ratios are shown as n/a.'));
    const m3 = v => fmt(v, 3);
    return h('div', { class: 'stack' }, form,
      h('div', { class: 'card table-wrap' }, h('h2', {}, res.title),
        h('table', { class: 'responsive' },
          h('thead', {}, h('tr', {}, ['Arm', 'TP', 'FP', 'FN', 'TN', 'Unknown (vuln/prot)', 'Precision', 'Recall', 'F1', 'FPR', 'FDR', 'Coverage'].map(t => h('th', {}, t)))),
          h('tbody', {}, res.arms.map(a => h('tr', {},
            h('td', { 'data-label': 'Arm' }, h('strong', {}, a.name)),
            ...[['TP', a.matrix.tp], ['FP', a.matrix.fp], ['FN', a.matrix.fn], ['TN', a.matrix.tn]].map(([l, v]) => h('td', { 'data-label': l }, v)),
            h('td', { 'data-label': 'Unknown' }, `${a.matrix.unknownOnVulnerable} / ${a.matrix.unknownOnProtected}`),
            ...[['Precision', a.metrics.precision], ['Recall', a.metrics.recall], ['F1', a.metrics.f1], ['FPR', a.metrics.fpr], ['FDR', a.metrics.fdr], ['Coverage', a.metrics.coverage]]
              .map(([l, v]) => h('td', { 'data-label': l }, m3(v)))))))),
      h('div', { class: 'card table-wrap' }, h('h2', {}, 'Per unit (last arm)'),
        h('table', { class: 'responsive' },
          h('thead', {}, h('tr', {}, ['Unit', 'Case', 'Label', 'Predicted', 'Rules', 'Match'].map(t => h('th', {}, t)))),
          h('tbody', {}, res.arms[res.arms.length - 1].units.map(u => {
            const p = u.prediction; const ok = p && p.status === u.label.label;
            return h('tr', {},
              h('td', { 'data-label': 'Unit', class: 'mono' }, u.label.unitKey),
              h('td', { 'data-label': 'Case' }, u.label.tc || ''),
              h('td', { 'data-label': 'Label' }, pill(u.label.label)),
              h('td', { 'data-label': 'Predicted' }, p ? pill(p.status) : '—'),
              h('td', { 'data-label': 'Rules' }, p ? p.ruleIds.join(', ') : ''),
              h('td', { 'data-label': 'Match' }, ok ? h('span', { class: 'ok' }, '✓') : h('span', { class: 'bad' }, '✗')));
          })))));
  }

  // ---- run info
  function runTab() {
    const run = S.report.run, c = S.report.coverage;
    const phases = Object.entries(run.phaseSeconds || {});
    const max = Math.max(0.001, ...phases.map(([, v]) => v));
    const gate = run.modelGate;
    return h('div', { class: 'grid grid-2' },
      h('div', { class: 'card' }, h('h2', {}, 'Reproducibility'),
        h('dl', { class: 'kv' },
          ...[['Tool version', run.toolVersion], ['Mode', run.mode], ['Target', run.targetPath], ['Target commit', run.targetCommit || '—'],
              ['Config hash', run.configHash], ['Model', run.modelId || '—'], ['Model hash', run.modelSha256 || '—'], ['Prompt', run.promptVersion || '—'],
              ['Started (UTC)', run.startedUtc], ['Total', fmt(run.totalSeconds, 2) + ' s'], ['Model load', run.modelLoadSeconds != null ? fmt(run.modelLoadSeconds, 2) + ' s' : '—'],
              ['Peak memory', mb(run.peakWorkingSetBytes)], ['Machine', run.machine]]
            .flatMap(([k, v]) => [h('dt', {}, k), h('dd', { class: k.includes('hash') || k === 'Target commit' ? 'mono small' : '' }, v)]))),
      h('div', { class: 'stack' },
        h('div', { class: 'card' }, h('h2', {}, 'Time per phase'),
          h('div', { class: 'bars' }, phases.map(([k, v]) => h('div', { class: 'bar' }, h('span', {}, k), h('span', { class: 'track' }, h('span', { class: 'fill', style: `width:${(v / max * 100).toFixed(1)}%` })), h('span', { class: 'mono' }, fmt(v, 2) + ' s'))))),
        gate ? h('div', { class: 'card' }, h('h2', {}, 'Calibration gate'),
          h('div', { class: 'notice' + (gate.passed ? ' ok' : '') }, gate.passed ? `Passed ${gate.correct}/${gate.total} — model verdicts are fused.` : `Failed ${gate.correct}/${gate.total} (needs ${gate.minCorrect}) — model verdicts are advisory only.`),
          h('ul', {}, gate.answers.map(a => h('li', { class: 'small' }, a)))) : null,
        h('div', { class: 'card' }, h('h2', {}, 'Coverage'),
          h('dl', { class: 'kv' }, ...Object.entries(c).flatMap(([k, v]) => [h('dt', {}, k.replace(/([A-Z])/g, ' $1').toLowerCase()), h('dd', {}, v)]))),
        S.report.loadDiagnostics.length ? h('div', { class: 'card' }, h('h2', {}, 'Diagnostics'), h('ul', {}, S.report.loadDiagnostics.map(d => h('li', { class: 'small' }, d.reason)))) : null));
  }

  // ------------------------------------------------------------ about
  function renderAbout() {
    const stage = (n, title, text) => h('div', { class: 'card' }, h('div', { class: 'tag' }, `Stage ${n}`), h('h2', { style: 'margin-top:8px' }, title), h('p', { class: 'muted', style: 'margin:0' }, text));
    setMain(h('div', { class: 'page-head' }, h('h1', {}, 'How BolaLens works')),
      h('div', { class: 'grid', style: 'grid-template-columns:repeat(auto-fit,minmax(240px,1fr))' },
        stage(1, 'Roslyn symbolic engine', 'Loads the solution with MSBuildWorkspace, lists endpoints, the global policy map, the EF Core ownership schema and the DI map, then follows every client-controlled id through controller → service → repository to EF Core writes and validates the guards (rules SYM-01…SYM-09).'),
        stage(2, 'Local SLM validation', 'Candidate paths become label-free IR slices. A local ONNX model (no network) answers in schema-constrained JSON; answers must cite slice lines. Models that fail the calibration gate are advisory only.'),
        stage(3, 'Reporting & evaluation', 'JSON report with spans, rule IDs and reasons, CI exit codes, this UI and an offline HTML export; the evaluation harness computes the confusion matrix and P/R/F1/FPR/FDR against frozen labels.')),
      h('div', { class: 'card section' }, h('h2', {}, 'Verdicts'),
        h('div', { class: 'row' }, pill('Vulnerable'), 'a client-controlled id selects an owned object that is changed without a valid owner check.'),
        h('div', { class: 'row', style: 'margin-top:6px' }, pill('Protected'), 'a valid check binds the caller identity to the same object before the change.'),
        h('div', { class: 'row', style: 'margin-top:6px' }, pill('Unknown'), 'evidence is missing (ambiguous dispatch, external code, depth limit) — never silently “safe”.'),
        h('div', { class: 'row', style: 'margin-top:6px' }, pill('OutOfScope'), 'the id never reaches a state change.')));
  }

  // ------------------------------------------------------------ boot
  initTheme();
  if (STATIC) {
    const data = JSON.parse(dataEl.textContent);
    S.report = data.report; S.context = data.context; S.runId = data.id;
  }
  window.addEventListener('hashchange', route);
  route();
})();
