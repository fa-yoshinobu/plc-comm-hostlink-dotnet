@echo off
setlocal
set PUBLISH_DIR=.\publish

echo ===================================================
echo [CI] Build, Test, and Format
echo ===================================================

echo [1/13] Building library project...
dotnet build src\PlcComm.KvHostLink\PlcComm.KvHostLink.csproj
if %errorlevel% neq 0 (echo [ERROR] Library build failed. & exit /b %errorlevel%)

echo [2/13] Testing documented API classification policy...
python scripts\test_documented_api_diff.py
if %errorlevel% neq 0 (echo [ERROR] Documented API classification policy tests failed. & exit /b %errorlevel%)

echo [3/13] Enforcing documented API diff classifications...
python scripts\check_documented_api_diff.py --baseline-metadata internal_docs\maintainer\api_baselines\PlcComm.KvHostLink-3.2.1.json --candidate-assembly-root src\PlcComm.KvHostLink\bin\Debug --classifications internal_docs\maintainer\documented_api_diff_classifications.json --repository-root .
if %errorlevel% neq 0 (echo [ERROR] Documented API diff classification failed. & exit /b %errorlevel%)

echo [4/13] Testing API reference generator helpers...
python scripts\test_generate_api_reference.py
if %errorlevel% neq 0 (echo [ERROR] API reference generator helper tests failed. & exit /b %errorlevel%)
python scripts\test_documentation_examples.py
if %errorlevel% neq 0 (echo [ERROR] Documentation example tests failed. & exit /b %errorlevel%)

echo [5/13] Validating API reference...
python scripts\generate_api_reference.py --assembly src\PlcComm.KvHostLink\bin\Debug\net8.0\PlcComm.KvHostLink.dll --xml src\PlcComm.KvHostLink\bin\Debug\net8.0\PlcComm.KvHostLink.xml --output docsrc\user\API_REFERENCE.md --title "KV Host Link .NET API Reference" --package PlcComm.KvHostLink --check
if %errorlevel% neq 0 (echo [ERROR] API reference is out of date. & exit /b %errorlevel%)

echo [6/13] Building test project...
dotnet build tests\PlcComm.KvHostLink.Tests\PlcComm.KvHostLink.Tests.csproj
if %errorlevel% neq 0 (echo [ERROR] Test project build failed. & exit /b %errorlevel%)

echo [7/13] Testing...
dotnet test tests\PlcComm.KvHostLink.Tests\PlcComm.KvHostLink.Tests.csproj --no-build
if %errorlevel% neq 0 (echo [ERROR] Tests failed. & exit /b %errorlevel%)

echo [8/13] Format check...
dotnet format PlcComm.KvHostLink.sln --verify-no-changes
if %errorlevel% neq 0 (echo [ERROR] Format violations found. & exit /b %errorlevel%)

echo [9/13] Building all user-facing sample projects...
dotnet build samples\PlcComm.KvHostLink.HighLevelSample\PlcComm.KvHostLink.HighLevelSample.csproj
if %errorlevel% neq 0 (echo [ERROR] HighLevelSample build failed. & exit /b %errorlevel%)
dotnet build samples\PlcComm.KvHostLink.BasicReadWriteSample\PlcComm.KvHostLink.BasicReadWriteSample.csproj
if %errorlevel% neq 0 (echo [ERROR] BasicReadWriteSample build failed. & exit /b %errorlevel%)
dotnet build samples\PlcComm.KvHostLink.NamedPollingSample\PlcComm.KvHostLink.NamedPollingSample.csproj
if %errorlevel% neq 0 (echo [ERROR] NamedPollingSample build failed. & exit /b %errorlevel%)
dotnet build samples\PlcComm.KvHostLink.ConfigPollingSample\PlcComm.KvHostLink.ConfigPollingSample.csproj
if %errorlevel% neq 0 (echo [ERROR] ConfigPollingSample build failed. & exit /b %errorlevel%)
dotnet build samples\PlcComm.KvHostLink.MultiPlcMonitorSample\PlcComm.KvHostLink.MultiPlcMonitorSample.csproj
if %errorlevel% neq 0 (echo [ERROR] MultiPlcMonitorSample build failed. & exit /b %errorlevel%)
dotnet build samples\PlcComm.KvHostLink.PollingReconnectSample\PlcComm.KvHostLink.PollingReconnectSample.csproj
if %errorlevel% neq 0 (echo [ERROR] PollingReconnectSample build failed. & exit /b %errorlevel%)

echo [10/13] Validating high-level XML docs coverage...
powershell -ExecutionPolicy Bypass -File scripts\check_high_level_docs.ps1
if %errorlevel% neq 0 (echo [ERROR] High-level XML docs coverage check failed. & exit /b %errorlevel%)

echo [11/13] Validating sample inventory...
powershell -ExecutionPolicy Bypass -File scripts\check_sample_inventory.ps1
if %errorlevel% neq 0 (echo [ERROR] Sample inventory validation failed. & exit /b %errorlevel%)

echo [12/13] Validating release workflow identity guards...
python scripts\check_release_workflow.py
if %errorlevel% neq 0 (echo [ERROR] Release workflow validation failed. & exit /b %errorlevel%)

echo [13/13] Validating minimal NuGet package contents...
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check_package_contents.ps1
if %errorlevel% neq 0 (echo [ERROR] NuGet package content validation failed. & exit /b %errorlevel%)

echo ===================================================
echo [SUCCESS] CI passed.
echo ===================================================
endlocal

