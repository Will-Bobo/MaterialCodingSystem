@echo off
setlocal EnableExtensions
REM Usage:
REM   build\package.cmd
REM   build\package.cmd Release
REM   build\package.cmd Release Rebuild
REM   build\package.cmd Release Rebuild MaterialCodingSystem\MaterialCodingSystem.csproj
REM   build\package.cmd Release Rebuild MaterialCodingSystem\MaterialCodingSystem.csproj 2026.04.07

set "CFG=%~1"
if "%CFG%"=="" set "CFG=Release"

set "TARGET=%~2"
if "%TARGET%"=="" set "TARGET=Rebuild"

set "PROJ=%~3"
if "%PROJ%"=="" set "PROJ=MaterialCodingSystem\MaterialCodingSystem.csproj"

set "PKGVER=%~4"

if "%PKGVER%"=="" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0package.ps1" -Configuration "%CFG%" -Target "%TARGET%" -Project "%PROJ%"
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0package.ps1" -Configuration "%CFG%" -Target "%TARGET%" -Project "%PROJ%" -PackageVersion "%PKGVER%"
)
exit /b %ERRORLEVEL%

