@echo off
setlocal EnableExtensions
REM MaterialCodingSystem build: SDK-style .NET (WPF net8.0-windows)
REM Usage: build\build.cmd [project_or_solution] [Configuration] [Target]
REM   build\build.cmd
REM   build\build.cmd MaterialCodingSystem\MaterialCodingSystem.csproj Debug
REM   build\build.cmd MaterialCodingSystem.Validation\MaterialCodingSystem.Validation.csproj Release Build

set "ROOT=%~dp0.."
pushd "%ROOT%" || exit /b 1

set "PROJ=%~1"
if "%PROJ%"=="" set "PROJ=MaterialCodingSystem\MaterialCodingSystem.csproj"

set "CFG=%~2"
if "%CFG%"=="" set "CFG=Debug"

set "TARGET=%~3"
if "%TARGET%"=="" set "TARGET=Rebuild"

where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
  echo ERROR: dotnet not found. Install .NET SDK.
  popd
  exit /b 1
)

echo RESTORE: "%PROJ%"
dotnet restore "%PROJ%"
if %ERRORLEVEL% neq 0 (
  set "ERR=%ERRORLEVEL%"
  popd
  exit /b %ERR%
)

echo BUILD: "%PROJ%" Configuration=%CFG% Target=%TARGET%
dotnet build "%PROJ%" -c "%CFG%" -t:%TARGET%
set "ERR=%ERRORLEVEL%"
popd
exit /b %ERR%
