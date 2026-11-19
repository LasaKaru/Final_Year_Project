# ADR-010: A local, dependency-free responsive web UI (instead of a React dashboard)
Date: 2026-11-19 · Status: Accepted

## Context
The PP promised a dashboard (FR16, reduced to "Could" in ADR-002). The owner now wants a suitable responsive UI for users.
Constraints: the tool must stay offline (NFR01), handle untrusted text from analysed code safely, and be explainable in Q&A.

## Decision
* `src/BolaLens.Web` — ASP.NET Core 8 minimal API bound to `127.0.0.1:5057`: start scans (background job, one at a time),
  browse runs, inspect findings (traced path, guards with rejection reasons, sinks, IR slice with evidence lines), endpoints,
  policy map, ownership schema, DI map, run info (phase timings, gate) and evaluate against labels.
* Front end: one HTML file + vanilla JS + CSS, **no framework, no CDN, no build step** (React would add an npm toolchain
  and remote assets). Layout uses CSS grid/flex with breakpoints at 1024 px and 720 px (tables become cards, master/detail
  becomes list → detail on phones), light/dark themes, keyboard focus styles, `prefers-reduced-motion`.
* The same assets produce an **offline HTML report** (`bolalens html`, or "Offline HTML report" in the UI) with the report
  JSON embedded — a hand-out for the supervisor/IPD.
* Security: localhost binding; state-changing requests require the `X-BolaLens: 1` header (CSRF defence, no CORS);
  CSP `default-src 'self'`; run ids validated (no path traversal); strings from analysed code rendered only as text nodes;
  embedded JSON escapes `<`.
* `BolaLens.App` holds the application services (ScanService, RunStore) shared by the CLI and the web host.

## Consequences
+ Usable demo surface for the IPD; no new runtime dependencies; testable with WebApplicationFactory + Playwright screenshots.
− Not a multi-user server (no authentication) — by design it must not be exposed beyond localhost.
