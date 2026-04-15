param(
  [ValidateSet("Debug","Release")]
  [string]$Configuration = "Release",

  [ValidateSet("Build","Rebuild")]
  [string]$Target = "Rebuild",

  [string]$Project = "MaterialCodingSystem\\MaterialCodingSystem.csproj",

  # Required when the project multi-targets (TargetFrameworks).
  # Default publishes the WPF app.
  [string]$Framework = "net8.0-windows",

  [string]$PackageVersion,

  [switch]$IncludePdb
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $root

$runtime = "win-x64"

Write-Host "Packaging MaterialCodingSystem..." -ForegroundColor Cyan

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw "dotnet not found. Install .NET SDK."
}

$projPath = (Resolve-Path (Join-Path $root $Project)).Path
if (-not (Test-Path $projPath)) { throw "Project not found: $projPath" }

$appName = [IO.Path]::GetFileNameWithoutExtension($projPath)

$stamp = (Get-Date).ToString("yyyyMMdd-HHmmss")

$tagVersion = "V0.0.1"
if (Get-Command git -ErrorAction SilentlyContinue) {
  try {
    $t = (& git describe --tags --abbrev=0 2>$null).Trim()
    if ($t) {
      if ($t -match '^[vV]') { $tagVersion = "V$($t.Substring(1))" }
      else { $tagVersion = "V$t" }
    }
  } catch {
    # Keep default V0.0.1
  }
}

$distRoot = Join-Path $root "dist"
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

$publishDir = Join-Path $distRoot ("{0}-publish-{1}-{2}-{3}" -f $appName, $tagVersion, $stamp, $Configuration)
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$publishArgs = @(
  "publish", $projPath,
  "-c", $Configuration,
  "-f", $Framework,
  "-r", $runtime,
  "--self-contained", "true",
  "/p:PublishSingleFile=true",
  "/p:IncludeNativeLibrariesForSelfExtract=true",
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

$zipPath = Join-Path $distRoot ("{0}-{1}-{2}-{3}.zip" -f $appName, $tagVersion, $stamp, $Configuration)
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "OK" -ForegroundColor Green
Write-Host ("Publish: {0}" -f $publishDir)
Write-Host ("Zip : {0}" -f $zipPath)

