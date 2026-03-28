@echo off
setlocal
set PUBLISH_DIR=.\publish

echo ===================================================
echo [CI] Build, Test, and Format
echo ===================================================

echo [1/7] Building library project...
dotnet build src\PlcComm.KvHostLink\PlcComm.KvHostLink.csproj
if %errorlevel% neq 0 (echo [ERROR] Library build failed. & exit /b %errorlevel%)

echo [2/7] Building test project...
dotnet build tests\PlcComm.KvHostLink.Tests\PlcComm.KvHostLink.Tests.csproj
if %errorlevel% neq 0 (echo [ERROR] Test project build failed. & exit /b %errorlevel%)

echo [3/7] Testing...
dotnet test tests\PlcComm.KvHostLink.Tests\PlcComm.KvHostLink.Tests.csproj --no-build
if %errorlevel% neq 0 (echo [ERROR] Tests failed. & exit /b %errorlevel%)

echo [4/7] Format check...
dotnet format PlcComm.KvHostLink.sln --verify-no-changes
if %errorlevel% neq 0 (echo [ERROR] Format violations found. & exit /b %errorlevel%)

echo [5/7] Building user-facing sample projects...
dotnet build samples\PlcComm.KvHostLink.HighLevelSample\PlcComm.KvHostLink.HighLevelSample.csproj
if %errorlevel% neq 0 (echo [ERROR] HighLevelSample build failed. & exit /b %errorlevel%)
dotnet build samples\PlcComm.KvHostLink.BasicReadWriteSample\PlcComm.KvHostLink.BasicReadWriteSample.csproj
if %errorlevel% neq 0 (echo [ERROR] BasicReadWriteSample build failed. & exit /b %errorlevel%)
dotnet build samples\PlcComm.KvHostLink.NamedPollingSample\PlcComm.KvHostLink.NamedPollingSample.csproj
if %errorlevel% neq 0 (echo [ERROR] NamedPollingSample build failed. & exit /b %errorlevel%)

echo [6/7] Validating high-level XML docs coverage...
powershell -ExecutionPolicy Bypass -File scripts\check_high_level_docs.ps1
if %errorlevel% neq 0 (echo [ERROR] High-level XML docs coverage check failed. & exit /b %errorlevel%)

echo [7/7] Validating sample inventory...
powershell -ExecutionPolicy Bypass -File scripts\check_sample_inventory.ps1
if %errorlevel% neq 0 (echo [ERROR] Sample inventory validation failed. & exit /b %errorlevel%)

echo ===================================================
echo [SUCCESS] CI passed.
echo ===================================================
endlocal

