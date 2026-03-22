@echo off
setlocal
set PUBLISH_DIR=.\publish

echo ===================================================
echo [CI] Build, Test, and Format
echo ===================================================

echo [1/3] Building...
dotnet build PlcComm.KvHostLink.sln
if %errorlevel% neq 0 (echo [ERROR] Build failed. & exit /b %errorlevel%)

echo [2/3] Testing...
dotnet test PlcComm.KvHostLink.sln --no-build
if %errorlevel% neq 0 (echo [ERROR] Tests failed. & exit /b %errorlevel%)

echo [3/3] Format check...
dotnet format PlcComm.KvHostLink.sln --verify-no-changes
if %errorlevel% neq 0 (echo [ERROR] Format violations found. & exit /b %errorlevel%)

echo ===================================================
echo [SUCCESS] CI passed.
echo ===================================================
endlocal

