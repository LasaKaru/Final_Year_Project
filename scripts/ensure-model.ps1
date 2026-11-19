# Windows version of ensure-model.sh: use the committed tiny model if it matches its SHA-256 manifest,
# otherwise rebuild it with get-model-smollm2.ps1 (network needed once).
$ErrorActionPreference = "Stop"
$Root = Resolve-Path "$PSScriptRoot\.."
$Name = "smollm2-135m-instruct-int4"
$Dir  = Join-Path $Root "models\$Name"
$Sums = Join-Path $Root "models\$Name.sha256"

function Test-Model {
  if (-not (Test-Path (Join-Path $Dir "genai_config.json"))) { return $false }
  foreach ($line in Get-Content $Sums) {
    $parts = $line -split '\s+', 2
    $file = Join-Path $Dir $parts[1].Trim()
    if (-not (Test-Path $file)) { return $false }
    if ((Get-FileHash $file -Algorithm SHA256).Hash.ToLower() -ne $parts[0]) { return $false }
  }
  return $true
}

if (Test-Model) { Write-Host "OK: $Name is present and matches its checksums - no download needed."; exit 0 }

Write-Host "Model missing or modified at $Dir - rebuilding with scripts\get-model-smollm2.ps1"
if (Test-Path $Dir) { Remove-Item -Recurse -Force $Dir }
& "$PSScriptRoot\get-model-smollm2.ps1" -Out $Dir
if (Test-Model) { Write-Host "OK: rebuilt $Name and verified checksums." }
else { Write-Warning "Rebuilt model differs from $Sums (different builder version?). It will still run; record the new hashes." }
