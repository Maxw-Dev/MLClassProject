#Author: Andre Mata Assis

<# 
.SYNOPSIS
    Use this to run training.

.DESCRIPTION
    This takes a .yaml and a build of the game and runs training. The resulting .onyx file is placed in /results,
    so ensure you run this from the project root, and that you have a /result directory on your machine.

.EXAMPLE
Position 0: Config
./tools/train.ps1 config/smoke.yaml

The only required parameter is Config, and the rest is filled with defaults.
The RunId will be run_[timestamp], and the build path will be "builds/MLClassProject.exe"

.EXAMPLE
Position 0: Config | Position 1: RunId | Position 2: BuildPath | Position 3: NumEnvs | Position 4: TimeScale | Position 5: Switch
./tools/train.ps1 config/smoke.yaml Example1 "builds/MLClassProject.exe" 4 20 -NoGraphics

.PARAMETER Config
    A path to the .yaml (ex: config/smoke.yaml)
    This is the only mandatory parameter.

.PARAMETER RunId
    Naming for the folder for this run's results. 
    Default: run_[timestamp]

.PARAMETER BuildPath
    A path to the game build. 
    Default: builds\MLClassProject.exe

.PARAMETER NumEnvs
    "The number of concurrent Unity environment instances to collect experiences from when training"
    Default: 4

.PARAMETER TimeScale
    "The time scale of the Unity environment(s). Equivalent to setting Time.timeScale in Unity"
    Default: 20

.PARAMETER NoGraphics
    "Whether to run the Unity executable in no-graphics mode (i.e. without initializing the graphics driver. 
    Use this only if your agents don't use visual observations."
    Default: Runs with graphics. Add --$NoGraphics to command in order to run with no graphics.
#>

#Input parameters
param (
    [Parameter(Mandatory)]
    [string]$Config,
    [string]$RunId = "run_$(Get-Date -Format 'yyyyMMdd_HHmmss')",
    [string]$BuildPath = (Join-Path "builds" "MLClassProject.exe") ,
    [int]$NumEnvs = 4,
    [int]$TimeScale = 20,
    [switch]$NoGraphics
)

if ([string]::IsNullOrEmpty($Config)) {
    Write-Warning "Missing required parameter: -Config"
    Get-Help $PSCommandPath
    exit 1
}

Write-Host "Starting training: RunId='$RunId' with config $Config"
Write-Host "    BuildPath = $BuildPath"
Write-Host "    NumEnvs = $NumEnvs"
Write-Host "    TimeScale = $TimeScale"
Write-Host "    NoGraphics = $NoGraphics"

#Extra args is whether no-graphics was specified
$extraArgs = @()
if ($NoGraphics) {
    $extraArgs += "--no-graphics"
}

uv run mlagents-learn $Config `
  --env=$BuildPath `
  --run-id=$RunId `
  --num-envs=$NumEnvs `
  --time-scale=$TimeScale `
  --force `
  @extraArgs