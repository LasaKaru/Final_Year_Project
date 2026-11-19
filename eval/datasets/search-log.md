# Dataset search log (Track P1)

One row per search — evidence of a systematic, reproducible search (guide 07 §2).

| Date | Source | Query / filter | Results seen | Candidates kept | Notes |
|---|---|---|---|---|---|
| 2026-11-19 | OSV.dev NuGet ecosystem dump (`osv-vulnerabilities.storage.googleapis.com/NuGet/all.zip`) | all advisories; keep if CWE ∈ {639, 284, 285, 862, 863, 566, 200, 269, 932} or summary/details match IDOR/"object reference"/"access control"/"authoriz" | 2,014 | 154 | 93 dropped as runtime/framework/library packages (Microsoft.*, System.*, Azure.*, drivers, parsers) — no application endpoints |
| 2026-11-19 | same 154, manual screen of summaries | application packages with object-level access control in an HTTP API | 61 | 18 | kept: Oqtane ×3 (CVE-2024-55186/55470/55471, fix PRs public), Umbraco ×6, FormCMS ×1, others (Hangfire dashboard, Telerik, FastReport, DisCatSharp, YamlDotNet, MessagePack, OpenClaw) rejected: not BOLA or no API code |
| 2026-11-19 | Oqtane git history (`git log --grep "security"` on oqtane.framework) | commits that fix access control without an advisory | ~40 | 1 | 0192d6e4 "resolve security issue with Notification PUT method" (2026-07-31) → case A-001 |
| 2026-11-19 | GitHub reference apps (guide 07 §4 list) | ASP.NET Core + EF Core APIs that build with SDK 10 | 5 | 3 | Conduit (realworld), CleanArchitecture, Oqtane v10.3.2 admitted to B; eShop rejected (main targets net11, no SDK); nopCommerce/SimplCommerce not yet screened |
