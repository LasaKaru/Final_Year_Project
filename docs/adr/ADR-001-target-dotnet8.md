# ADR-001: The tool targets .NET 8 (net8.0)
Date: 2026-11-19 · Status: Accepted (review with supervisor)

## Context
The guide pack recommended .NET 10 LTS because .NET 8 and .NET 9 reached end of support on 10 Nov 2026.
The project owner decided to build the tool on .NET 8, the platform named in the Project Proposal.

## Decision
All BolaLens projects target `net8.0` (one line in `Directory.Build.props`); `global.json` pins SDK 8.0.1xx
(roll-forward `latestFeature`). Analysed projects may target .NET 8/9/10 as long as an SDK that can build them is installed.

## Consequences
+ Matches the PP and the supervisor's expectations; Roslyn 4.14, EF Core 8 and ASP.NET Core 8 APIs are stable.
− .NET 8 no longer receives security patches (end of support 10 Nov 2026). This is acceptable for an offline research
  prototype that never runs analysed code, but it must be stated as a threat/limitation.
− Analysing net10.0 targets needs a newer SDK installed side by side.

## Switching later
Change `<TargetFramework>` in `Directory.Build.props` to `net10.0` and `global.json` to `10.0.100`; nothing else depends on .NET 8.
