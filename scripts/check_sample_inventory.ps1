$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$sampleProjects = @(
    "samples\PlcComm.KvHostLink.HighLevelSample\PlcComm.KvHostLink.HighLevelSample.csproj",
    "samples\PlcComm.KvHostLink.BasicReadWriteSample\PlcComm.KvHostLink.BasicReadWriteSample.csproj",
    "samples\PlcComm.KvHostLink.NamedPollingSample\PlcComm.KvHostLink.NamedPollingSample.csproj",
    "samples\PlcComm.KvHostLink.ConfigPollingSample\PlcComm.KvHostLink.ConfigPollingSample.csproj",
    "samples\PlcComm.KvHostLink.MultiPlcMonitorSample\PlcComm.KvHostLink.MultiPlcMonitorSample.csproj",
    "samples\PlcComm.KvHostLink.PollingReconnectSample\PlcComm.KvHostLink.PollingReconnectSample.csproj"
)

$docFiles = @(
    "README.md",
    "samples\README.md",
    "docsrc\user\USAGE_GUIDE.md"
)

$errors = New-Object System.Collections.Generic.List[string]

foreach ($sampleProject in $sampleProjects) {
    $samplePath = Join-Path $root $sampleProject
    if (-not (Test-Path $samplePath)) {
        $errors.Add("Missing sample project: $sampleProject")
        continue
    }

    [xml]$project = Get-Content -LiteralPath $samplePath -Raw
    $targetFrameworks = @(@($project.Project.PropertyGroup.TargetFramework) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $multiTargetFrameworks = @(@($project.Project.PropertyGroup.TargetFrameworks) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($targetFrameworks.Count -ne 1 -or $targetFrameworks[0] -ne "net10.0" -or $multiTargetFrameworks.Count -ne 0) {
        $errors.Add("$sampleProject must target exactly net10.0.")
    }

    $sampleName = [System.IO.Path]::GetFileNameWithoutExtension($sampleProject)
    foreach ($docFile in $docFiles) {
        $docPath = Join-Path $root $docFile
        $docText = Get-Content -Path $docPath -Raw
        if (-not $docText.Contains($sampleProject) -and -not $docText.Contains($sampleName)) {
            $errors.Add("$docFile does not reference $sampleProject.")
        }
    }
}

$rootPrefix = $root.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$discoveredSampleProjects = @(Get-ChildItem -LiteralPath (Join-Path $root "samples") -Recurse -Filter "*.csproj" |
    ForEach-Object {
        if (-not $_.FullName.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Sample project is outside the repository root: $($_.FullName)"
        }
        $_.FullName.Substring($rootPrefix.Length)
    })
$unexpectedProjects = @($discoveredSampleProjects | Where-Object { $_ -notin $sampleProjects })
if ($unexpectedProjects.Count -ne 0) {
    $errors.Add("Unclassified user-facing sample projects: $($unexpectedProjects -join ', ')")
}

if ($errors.Count -gt 0) {
    Write-Host "[ERROR] Sample inventory validation failed." -ForegroundColor Red
    foreach ($message in $errors) {
        Write-Host " - $message" -ForegroundColor Red
    }
    exit 1
}

Write-Host "[OK] Sample inventory validation passed."
