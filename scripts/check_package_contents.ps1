[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$workspaceRoot = Split-Path -Parent $repositoryRoot
$outputDirectory = Join-Path $workspaceRoot (".plc-hostlink-nuget-" + [guid]::NewGuid().ToString("N"))
$projectPath = Join-Path $repositoryRoot "src\PlcComm.KvHostLink\PlcComm.KvHostLink.csproj"

try {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    $packArguments = @("pack", $projectPath, "-c", $Configuration, "--no-restore", "-o", $outputDirectory)
    if ($NoBuild) {
        $packArguments += "--no-build"
    }
    & dotnet @packArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed."
    }

    $packages = @(Get-ChildItem -LiteralPath $outputDirectory -Filter "*.nupkg" |
        Where-Object { -not $_.Name.EndsWith(".snupkg", [System.StringComparison]::OrdinalIgnoreCase) })
    if ($packages.Count -ne 1) {
        throw "Expected exactly one NuGet package, found $($packages.Count)."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packages[0].FullName)
    try {
        $files = @($archive.Entries |
            Where-Object { -not $_.FullName.EndsWith("/") } |
            ForEach-Object { $_.FullName.Replace("\", "/") } |
            Sort-Object -Unique)
    }
    finally {
        $archive.Dispose()
    }

    $required = @(
        "LICENSE",
        "README.md",
        "PlcComm.KvHostLink.nuspec",
        "lib/net8.0/PlcComm.KvHostLink.dll",
        "lib/net8.0/PlcComm.KvHostLink.xml",
        "lib/net9.0/PlcComm.KvHostLink.dll",
        "lib/net9.0/PlcComm.KvHostLink.xml",
        "lib/net10.0/PlcComm.KvHostLink.dll",
        "lib/net10.0/PlcComm.KvHostLink.xml"
    )
    $missing = @($required | Where-Object { $_ -notin $files })
    if ($missing.Count -ne 0) {
        throw "NuGet package is missing required files: $($missing -join ', ')"
    }

    $forbidden = @($files | Where-Object {
        $_ -match '(^|/)(tests?|samples?|scripts?|internal_docs|docsrc|TODO\.md)(/|$)' -or
        $_ -match '\.(cs|csproj|sln|json)$'
    })
    if ($forbidden.Count -ne 0) {
        throw "NuGet package contains repository-only files: $($forbidden -join ', ')"
    }

    Write-Host "[OK] NuGet package content contract passed: package=$($packages[0].Name) files=$($files.Count)"
}
finally {
    if (Test-Path -LiteralPath $outputDirectory) {
        Remove-Item -LiteralPath $outputDirectory -Recurse -Force
    }
}
