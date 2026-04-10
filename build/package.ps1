param(
  [ValidateSet("Debug","Release")]
  [string]$Configuration = "Release",

  [ValidateSet("Build","Rebuild")]
  [string]$Target = "Rebuild",

  [string]$Project = "MaterialCodingSystem\\MaterialCodingSystem.csproj",

  [string]$PackageVersion,

  [switch]$IncludePdb
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $root

Write-Host "Packaging MaterialCodingSystem..." -ForegroundColor Cyan

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw "dotnet not found. Install .NET SDK."
}

$projPath = (Resolve-Path (Join-Path $root $Project)).Path
if (-not (Test-Path $projPath)) { throw "Project not found: $projPath" }

$appName = [IO.Path]::GetFileNameWithoutExtension($projPath)

$distRoot = Join-Path $root "dist"
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

$publishDir = Join-Path $distRoot ("{0}-publish-{1}" -f $appName, $Configuration)
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$publishArgs = @(
  "publish", $projPath,
  "-c", $Configuration,
  "-t:$Target",
  "-o", $publishDir
)
if (-not $IncludePdb) {
  $publishArgs += @("-p:DebugType=None", "-p:DebugSymbols=false")
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $publishDir ("{0}.exe" -f $appName)
$version =
  if ($PackageVersion) { $PackageVersion }
  elseif (Test-Path $exe) { (Get-Item $exe).VersionInfo.FileVersion }
  else { "unknown" }
$safeVersion = ($version -replace '[^\w\.\-]+','_')

$zipPath = Join-Path $distRoot ("{0}-{1}-{2}.zip" -f $appName, $safeVersion, $Configuration)
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "OK" -ForegroundColor Green
Write-Host ("Publish: {0}" -f $publishDir)
Write-Host ("Zip : {0}" -f $zipPath)

