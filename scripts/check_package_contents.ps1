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
        $nuspecEntry = $archive.GetEntry("PlcComm.KvHostLink.nuspec")
        if ($null -eq $nuspecEntry) { throw "NuGet package has no nuspec metadata." }
        $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
        try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $packageVersion = [string]$nuspec.package.metadata.version
        if ([string]::IsNullOrWhiteSpace($packageVersion)) { throw "NuGet package version is missing." }
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

    $repositoryOnlyNames = @(
        ".gitattributes", ".gitignore", "AGENTS.md", "TODO.md",
        "release_check.bat", "run_ci.bat"
    )
    $forbidden = @($files | Where-Object {
        $fileName = [System.IO.Path]::GetFileName($_)
        $_ -match '(^|/)(\.github|\.codex|\.pio|\.tools|build|build_win|release-artifacts|tests?|samples?|scripts?|internal_docs|docsrc|tools)(/|$)' -or
        $fileName -in $repositoryOnlyNames -or
        $_ -match '\.(cs|csproj|sln|json|pfx|snk|pem|key)$'
    })
    if ($forbidden.Count -ne 0) {
        throw "NuGet package contains repository-only files: $($forbidden -join ', ')"
    }

    $consumerDirectory = Join-Path $outputDirectory "consumer"
    [void](New-Item -ItemType Directory -Path $consumerDirectory -Force)
    $consumerProject = Join-Path $consumerDirectory "PackedConsumer.csproj"
    $consumerProgram = Join-Path $consumerDirectory "Program.cs"
    [System.IO.File]::WriteAllText($consumerProject, @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="PlcComm.KvHostLink" Version="$packageVersion" /></ItemGroup>
</Project>
"@)
    [System.IO.File]::WriteAllText($consumerProgram, @"
using System;
using PlcComm.KvHostLink;
Console.WriteLine(typeof(KvHostLinkClient).FullName);
"@)
    & dotnet restore $consumerProject --source $outputDirectory
    if ($LASTEXITCODE -ne 0) { throw "Packed NuGet consumer restore failed." }
    & dotnet run --project $consumerProject --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Packed NuGet consumer build/run failed." }

    Write-Host "[OK] NuGet package content/consumer contract passed: package=$($packages[0].Name) files=$($files.Count) consumer=net8.0"
}
finally {
    if (Test-Path -LiteralPath $outputDirectory) {
        Remove-Item -LiteralPath $outputDirectory -Recurse -Force
    }
}
