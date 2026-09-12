#Author: Andre Mata Assis

#Input parameters
param (
    [string]$Config = "config/smoke.yaml",
    [string]$RunId = "smoke_$(Get-Date -Format 'yyyyMMdd_HHmmss')",
    [string]$BuildPath = "builds/MLClassProject.exe",
    [int]$NumEnvs = 4,
    [switch]$NoGraphics
)

Write-Host "Starting training: $RunId with config $Config"

#Extra args is whether no-graphics was specified
$extraArgs = @()
if ($NoGraphics) {
    $extraArgs += "--no-graphics"
}

uv run mlagents-learn $Config `
  --env=$BuildPath `
  --run-id=$RunId `
  --num-envs=$NumEnvs `
  --time-scale=20 `
  --force `
  @extraArgs