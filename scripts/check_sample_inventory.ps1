$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$sampleProjects = @(
    "samples\PlcComm.KvHostLink.HighLevelSample\PlcComm.KvHostLink.HighLevelSample.csproj",
    "samples\PlcComm.KvHostLink.BasicReadWriteSample\PlcComm.KvHostLink.BasicReadWriteSample.csproj",
    "samples\PlcComm.KvHostLink.NamedPollingSample\PlcComm.KvHostLink.NamedPollingSample.csproj"
)

$docFiles = @(
    "README.md",
    "samples\README.md",
    "docsrc\user\USER_GUIDE.md"
)

$errors = New-Object System.Collections.Generic.List[string]

foreach ($sampleProject in $sampleProjects) {
    $samplePath = Join-Path $root $sampleProject
    if (-not (Test-Path $samplePath)) {
        $errors.Add("Missing sample project: $sampleProject")
        continue
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

if ($errors.Count -gt 0) {
    Write-Host "[ERROR] Sample inventory validation failed." -ForegroundColor Red
    foreach ($message in $errors) {
        Write-Host " - $message" -ForegroundColor Red
    }
    exit 1
}

Write-Host "[OK] Sample inventory validation passed."
