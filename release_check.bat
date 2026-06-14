@echo off
setlocal

echo ===================================================
echo [RELEASE] Host Link .NET release check
echo ===================================================

echo [1/3] Checking registry version...
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check_registry_duplicate.ps1 -Registry nuget -Package PlcComm.KvHostLink -VersionSource csproj -ManifestPath src\PlcComm.KvHostLink\PlcComm.KvHostLink.csproj
if %errorlevel% neq 0 (
    echo [ERROR] Release version check failed.
    exit /b %errorlevel%
)

echo [2/3] Running CI...
call run_ci.bat
if %errorlevel% neq 0 (
    echo [ERROR] CI failed.
    exit /b %errorlevel%
)

echo [3/3] Packing NuGet package...
dotnet pack src\PlcComm.KvHostLink\PlcComm.KvHostLink.csproj -c Release
if %errorlevel% neq 0 (
    echo [ERROR] Pack failed.
    exit /b %errorlevel%
)

echo ===================================================
echo [SUCCESS] Release check passed.
echo ===================================================
endlocal
