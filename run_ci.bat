@echo off
setlocal
set PUBLISH_DIR=.\publish

echo ===================================================
echo [CI] Build, Test, and Format
echo ===================================================

echo [1/8] Building library project...
dotnet build src\PlcComm.KvHostLink\PlcComm.KvHostLink.csproj
if %errorlevel% neq 0 (echo [ERROR] Library build failed. & exit /b %errorlevel%)

echo [2/8] Validating API reference...
python scripts\generate_api_reference.py --assembly src\PlcComm.KvHostLink\bin\Debug\net8.0\PlcComm.KvHostLink.dll --xml src\PlcComm.KvHostLink\bin\Debug\net8.0\PlcComm.KvHostLink.xml --output docsrc\user\API_REFERENCE.md --title "KV Host Link .NET API Reference" --package PlcComm.KvHostLink --check
if %errorlevel% neq 0 (echo [ERROR] API reference is out of date. & exit /b %errorlevel%)

echo [3/8] Building test project...
dotnet build tests\PlcComm.KvHostLink.Tests\PlcComm.KvHostLink.Tests.csproj
if %errorlevel% neq 0 (echo [ERROR] Test project build failed. & exit /b %errorlevel%)

echo [4/8] Testing...
dotnet test tests\PlcComm.KvHostLink.Tests\PlcComm.KvHostLink.Tests.csproj --no-build
if %errorlevel% neq 0 (echo [ERROR] Tests failed. & exit /b %errorlevel%)

echo [5/8] Format check...
dotnet format PlcComm.KvHostLink.sln --verify-no-changes
if %errorlevel% neq 0 (echo [ERROR] Format violations found. & exit /b %errorlevel%)

echo [6/8] Building user-facing sample projects...
dotnet build samples\PlcComm.KvHostLink.HighLevelSample\PlcComm.KvHostLink.HighLevelSample.csproj
if %errorlevel% neq 0 (echo [ERROR] HighLevelSample build failed. & exit /b %errorlevel%)
dotnet build samples\PlcComm.KvHostLink.BasicReadWriteSample\PlcComm.KvHostLink.BasicReadWriteSample.csproj
if %errorlevel% neq 0 (echo [ERROR] BasicReadWriteSample build failed. & exit /b %errorlevel%)
dotnet build samples\PlcComm.KvHostLink.NamedPollingSample\PlcComm.KvHostLink.NamedPollingSample.csproj
if %errorlevel% neq 0 (echo [ERROR] NamedPollingSample build failed. & exit /b %errorlevel%)

echo [7/8] Validating high-level XML docs coverage...
powershell -ExecutionPolicy Bypass -File scripts\check_high_level_docs.ps1
if %errorlevel% neq 0 (echo [ERROR] High-level XML docs coverage check failed. & exit /b %errorlevel%)

echo [8/8] Validating sample inventory...
powershell -ExecutionPolicy Bypass -File scripts\check_sample_inventory.ps1
if %errorlevel% neq 0 (echo [ERROR] Sample inventory validation failed. & exit /b %errorlevel%)

echo ===================================================
echo [SUCCESS] CI passed.
echo ===================================================
endlocal

