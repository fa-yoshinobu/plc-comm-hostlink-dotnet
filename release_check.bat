@echo off
setlocal

echo ===================================================
echo [RELEASE] Host Link .NET release check
echo ===================================================

echo [1/5] Checking registry version...
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check_registry_duplicate.ps1 -Registry nuget -Package PlcComm.KvHostLink -VersionSource csproj -ManifestPath Directory.Build.props
if %errorlevel% neq 0 (
    echo [ERROR] Release version check failed.
    exit /b %errorlevel%
)

echo [2/5] Checking canonical HostLink profile fixtures...
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\update_hostlink_profile_jsons.ps1 -FailIfChanged
if %errorlevel% neq 0 (
    echo [ERROR] Canonical HostLink profile JSON check failed.
    exit /b %errorlevel%
)

echo [3/5] Checking GitHub source archive contents...
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check_source_archive.ps1
if %errorlevel% neq 0 (
    echo [ERROR] Source archive content check failed.
    exit /b %errorlevel%
)

echo [4/5] Running CI...
call run_ci.bat
if %errorlevel% neq 0 (
    echo [ERROR] CI failed.
    exit /b %errorlevel%
)

echo [5/5] Packing NuGet package...
dotnet pack src\PlcComm.KvHostLink\PlcComm.KvHostLink.csproj -c Release
if %errorlevel% neq 0 (
    echo [ERROR] Pack failed.
    exit /b %errorlevel%
)

echo ===================================================
echo [SUCCESS] Release check passed.
echo ===================================================
endlocal
