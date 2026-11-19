# 03 — Environment Setup Guide (step by step)

Target: a reproducible dev environment on a ~16 GB RAM laptop (Windows 11, macOS or Linux). Commands are given for **PowerShell (Windows)** and **bash (macOS/Linux)** where they differ.

---

## 1. Install the toolchain

| Tool | Version | Why | Check |
|---|---|---|---|
| .NET SDK | **10.0.x** (LTS) | Builds the tool; MSBuildWorkspace uses it to load target projects | `dotnet --info` |
| (Optional) .NET 8 targeting | via NuGet automatically | Needed only to *build* real projects that target net8.0 — the .NET 10 SDK restores the net8.0 reference pack | `dotnet build` on a net8.0 repo |
| IDE | Visual Studio 2026 / 2022 17.14+ **or** JetBrains Rider **or** VS Code + C# Dev Kit | Debugging Roslyn code is much easier in a full IDE | open a .sln |
| Git | latest | Version control | `git --version` |
| GitHub CLI (optional) | latest | Repo/issue creation from terminal | `gh auth status` |
| Python | 3.11+ | **Only** to download the ONNX model with the Hugging Face CLI | `python --version` |
| Draw.io desktop or diagrams.net | latest | Stakeholder onion, use-case diagrams | — |
| Mermaid live editor (browser) | — | Gantt, sequence, class, flow diagrams | mermaid.live |
| PlantUML (VS Code extension or plantuml.com) | — | Use case diagram | — |

### Windows
```powershell
winget install Microsoft.DotNet.SDK.10
winget install Git.Git
winget install GitHub.cli
winget install Python.Python.3.12
# IDE: install Visual Studio (".NET desktop" + "ASP.NET and web" workloads) or Rider
```

### macOS
```bash
brew install --cask dotnet-sdk   # make sure it installs 10.0.x
brew install git gh python@3.12
```

### Ubuntu/Debian
```bash
# follow https://learn.microsoft.com/dotnet/core/install/linux for your distro, then:
sudo apt-get install -y dotnet-sdk-10.0 git python3 python3-pip
```

**Verify:**
```bash
dotnet --list-sdks      # must include 10.0.x
dotnet --info
```

> **Why .NET 10 and not .NET 8?** .NET 8 and .NET 9 reached end of support on 10 November 2026; .NET 10 is the current LTS (support until November 2028). Your *analysed* projects can still be .NET 8/9 — that is unchanged research scope. If your supervisor insists on net8.0, change `<TargetFramework>` in `Directory.Build.props` (one line) — nothing else in this pack depends on .NET 10 features.

---

## 2. IDE settings worth turning on

- Enable **nullable warnings** (already set in `Directory.Build.props`).
- Install the **Syntax Visualizer** (Visual Studio: *.NET Compiler Platform SDK* component in the installer; Rider: "PSI Viewer"/Roslyn syntax tree plugin; VS Code: none — use `playground/` printouts). Being able to *see* the syntax tree of BolaLab code will save you days.
- Set the debugger to "Just My Code" **off** when stepping into Roslyn is needed.

---

## 3. Create the GitHub repository

```bash
gh auth login
gh repo create bolalens --private --clone --description "Context-aware BOLA detection for ASP.NET Core + EF Core (Roslyn + local SLM)"
cd bolalens
```
(or create it on github.com and `git clone`)

Then on github.com:
- Settings → Collaborators → add your supervisor (read access).
- Settings → Branches → protect `main`: require a pull request and status checks (CI) to pass.
- Projects → New project (Board) → columns: *Backlog, This sprint, In progress, Review, Done*.

---

## 4. Scaffold the solution (copy–paste, in order)

Run from the repo root (`bolalens/`).

```bash
# 4.1 pin the SDK
dotnet new globaljson --sdk-version 10.0.100 --roll-forward latestFeature

# 4.2 solution file (classic .sln format = widest tool compatibility)
dotnet new sln -n BolaLens --format sln

# 4.3 source projects
dotnet new classlib -n BolaLens.Core       -o src/BolaLens.Core
dotnet new classlib -n BolaLens.Analysis   -o src/BolaLens.Analysis
dotnet new classlib -n BolaLens.Slicing    -o src/BolaLens.Slicing
dotnet new classlib -n BolaLens.Inference  -o src/BolaLens.Inference
dotnet new classlib -n BolaLens.Reporting  -o src/BolaLens.Reporting
dotnet new classlib -n BolaLens.Eval       -o src/BolaLens.Eval
dotnet new console  -n BolaLens.Cli        -o src/BolaLens.Cli

# 4.4 test projects (xUnit v3 template; if not available: `dotnet new install xunit.v3.templates`)
dotnet new install xunit.v3.templates
dotnet new xunit3 -n BolaLens.Analysis.Tests     -o tests/BolaLens.Analysis.Tests
dotnet new xunit3 -n BolaLens.Integration.Tests  -o tests/BolaLens.Integration.Tests
dotnet new xunit3 -n BolaLens.Inference.Tests    -o tests/BolaLens.Inference.Tests

# 4.5 add everything to the solution
# bash:
dotnet sln BolaLens.sln add src/*/*.csproj tests/*/*.csproj
# PowerShell equivalent:
# dotnet sln BolaLens.sln add (Get-ChildItem src,tests -Recurse -Filter *.csproj).FullName

# 4.6 project references (dependency direction: Cli -> everything; nothing -> Cli)
dotnet add src/BolaLens.Analysis   reference src/BolaLens.Core
dotnet add src/BolaLens.Slicing    reference src/BolaLens.Core
dotnet add src/BolaLens.Inference  reference src/BolaLens.Core
dotnet add src/BolaLens.Reporting  reference src/BolaLens.Core
dotnet add src/BolaLens.Eval       reference src/BolaLens.Core
dotnet add src/BolaLens.Cli reference src/BolaLens.Core src/BolaLens.Analysis src/BolaLens.Slicing src/BolaLens.Inference src/BolaLens.Reporting src/BolaLens.Eval

dotnet add tests/BolaLens.Analysis.Tests    reference src/BolaLens.Analysis src/BolaLens.Core
dotnet add tests/BolaLens.Integration.Tests reference src/BolaLens.Cli src/BolaLens.Analysis src/BolaLens.Core src/BolaLens.Reporting src/BolaLens.Eval src/BolaLens.Slicing
dotnet add tests/BolaLens.Inference.Tests   reference src/BolaLens.Inference src/BolaLens.Slicing src/BolaLens.Core
```

> If `--format sln` is rejected by your SDK, run `dotnet new sln -n BolaLens` (it may create `.slnx`, which is fine for building; the tool itself must still accept `.sln`, `.slnx` and `.csproj` inputs).

### 4.7 Delete template noise
Delete `Class1.cs` in each classlib and the sample test file contents (keep one trivial passing test per test project for now).

---

## 5. Repository-level files

### 5.1 `Directory.Build.props` (repo root)
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Deterministic>true</Deterministic>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <Authors>Lasantha Karunarathne</Authors>
    <Product>BolaLens</Product>
    <Version>0.1.0</Version>
  </PropertyGroup>
</Project>
```
Remove `<TargetFramework>`, `<Nullable>`, `<ImplicitUsings>` lines from the individual `.csproj` files so the root file controls them.

### 5.2 `Directory.Packages.props` (central package management)
**Step 1 — create it with only the switch on:**
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
  </ItemGroup>
</Project>
```
**Step 2 — run the `dotnet add package` commands in §5.3.** With central management on, each command writes a version-less `<PackageReference>` into the csproj and adds the latest stable `<PackageVersion>` here automatically.

**Step 3 — check the result looks like this** (your version numbers will be whatever was current on the day — keep them, never float them, and copy them into `docs/reproducibility.md`):
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Roslyn: all Microsoft.CodeAnalysis.* MUST share one version -->
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="X.Y.Z" />
    <PackageVersion Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="X.Y.Z" />
    <PackageVersion Include="Microsoft.Build.Locator" Version="X.Y.Z" />
    <!-- CLI -->
    <PackageVersion Include="System.CommandLine" Version="2.0.X" />
    <PackageVersion Include="Spectre.Console" Version="X.Y.Z" />
    <!-- Local inference (CPU build). Swap for .DirectML (Windows GPU) or .Cuda later -->
    <PackageVersion Include="Microsoft.ML.OnnxRuntimeGenAI" Version="0.X.Y" />
    <!-- Tests -->
    <PackageVersion Include="xunit.v3" Version="X.Y.Z" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="X.Y.Z" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="X.Y.Z" />
    <PackageVersion Include="Shouldly" Version="X.Y.Z" />
    <PackageVersion Include="Verify.XunitV3" Version="X.Y.Z" />
    <!-- Test-only references so in-memory compilations can resolve EF Core / ASP.NET types -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.X" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.X" />
  </ItemGroup>
</Project>
```
If two `Microsoft.CodeAnalysis.*` packages end up on different versions, set them to the same version by hand. Make sure `Microsoft.EntityFrameworkCore*` stays on a 10.0.x version.

> Tip on assertions: use **Shouldly** (or plain `Assert`). FluentAssertions v8+ requires a commercial licence for non-OSS use; avoid the question entirely.

### 5.3 Package references per project
```bash
dotnet add src/BolaLens.Analysis package Microsoft.CodeAnalysis.CSharp.Workspaces
dotnet add src/BolaLens.Analysis package Microsoft.CodeAnalysis.Workspaces.MSBuild
dotnet add src/BolaLens.Analysis package Microsoft.Build.Locator
dotnet add src/BolaLens.Slicing  package Microsoft.CodeAnalysis.CSharp.Workspaces
dotnet add src/BolaLens.Inference package Microsoft.ML.OnnxRuntimeGenAI
dotnet add src/BolaLens.Cli      package System.CommandLine
dotnet add src/BolaLens.Cli      package Spectre.Console
dotnet add tests/BolaLens.Analysis.Tests package Shouldly
dotnet add tests/BolaLens.Analysis.Tests package Microsoft.EntityFrameworkCore.Relational
dotnet add tests/BolaLens.Integration.Tests package Shouldly
dotnet add tests/BolaLens.Integration.Tests package Verify.XunitV3
```
With central package management on, these commands write `<PackageReference Include="…" />` without versions (and add the version to `Directory.Packages.props` if missing).

In `tests/BolaLens.Analysis.Tests/*.csproj` also add:
```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```
so in-memory test compilations can reference `ControllerBase`, `[HttpPut]`, `ClaimsPrincipal`, etc.

### 5.4 `.gitignore`
```bash
dotnet new gitignore
```
Then append:
```
# BolaLens
models/**
!models/README.md
eval/runs/**/raw/
*.onnx
*.onnx.data
.idea/
```

### 5.5 `.editorconfig`
```bash
dotnet new editorconfig
```
Keep defaults; add `dotnet_diagnostic.CA1062.severity = none` if null-argument warnings become noisy.

### 5.6 Fixture isolation (important)
BolaLab lives in `fixtures/BolaLab/` with its **own** `.sln`. MSBuild walks *up* the folder tree looking for `Directory.Build.props`/`Directory.Packages.props`, so stop inheritance:

`fixtures/Directory.Build.props`
```xml
<Project>
  <!-- Intentionally empty: fixtures must look like ordinary third-party projects -->
</Project>
```
`fixtures/Directory.Packages.props`
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```
Do **not** add the fixture projects to `BolaLens.sln`.

---

## 6. Continuous integration (`.github/workflows/ci.yml`)

```yaml
name: ci
on:
  push:
    branches: [ main ]
  pull_request:

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - name: Restore & build tool
        run: |
          dotnet restore BolaLens.sln
          dotnet build BolaLens.sln -c Release --no-restore
      - name: Build fixture (integration tests load it with MSBuildWorkspace)
        run: dotnet build fixtures/BolaLab/BolaLab.sln -c Release
      - name: Test (model-dependent tests are skipped in CI)
        run: dotnet test BolaLens.sln -c Release --no-build --filter "Category!=RequiresModel"
      - name: Symbolic scan of BolaLab (smoke)
        run: dotnet run --project src/BolaLens.Cli -c Release --no-build -- scan fixtures/BolaLab/BolaLab.sln --mode symbolic --out bolalab-report.json
      - uses: actions/upload-artifact@v4
        with:
          name: bolalab-report
          path: bolalab-report.json
```
Add the CI badge to `README.md`. (Comment out the scan step until S3.)

---

## 7. Local model download (Sprint 3, S3-05)

Models are **not** committed. `models/README.md` documents how to fetch them and their hashes.

```bash
python -m pip install -U "huggingface_hub[cli]"

# Phi-3-mini-4k-instruct, int4, CPU build (~2–3 GB)
hf download microsoft/Phi-3-mini-4k-instruct-onnx \
  --include "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/*" \
  --local-dir models/phi3-mini-4k
# (older CLI name: huggingface-cli download ... same arguments)
```

Record hashes (reproducibility, NFR05):
```powershell
# Windows
Get-ChildItem models\phi3-mini-4k -Recurse -File | Get-FileHash -Algorithm SHA256 | Format-Table Hash, Path
```
```bash
# macOS/Linux
find models/phi3-mini-4k -type f -exec sha256sum {} \; | sort -k2
```
Paste the result into `models/README.md` and `docs/reproducibility.md`. Also record the model card URL, licence (MIT for Phi-3) and the date.

**Later candidates (S8):** `microsoft/Phi-4-mini-instruct-onnx` (check folder name for the CPU int4 variant on the model page), Qwen2.5-Coder-1.5B/3B-Instruct exported with the ONNX Runtime GenAI model builder (`python -m onnxruntime_genai.models.builder -m <hf-id> -o <out> -p int4 -e cpu`). Check each licence before use.

**Offline proof (NFR01):** after the model is downloaded and packages restored, disconnect the network (airplane mode) and run `bolalens scan --mode hybrid`. Screenshot/record it — this is your evidence for "100 % local inference".

---

## 8. Sanity checklist at the end of setup

- [ ] `dotnet build BolaLens.sln` → success
- [ ] `dotnet test BolaLens.sln` → all trivial tests pass
- [ ] `dotnet build fixtures/BolaLab/BolaLab.sln` → success (after S0-07)
- [ ] CI shows green on `main`
- [ ] `docs/reproducibility.md` lists SDK version, package versions, OS, CPU, RAM
