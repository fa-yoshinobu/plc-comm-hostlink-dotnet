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

function Remove-DirectoryWithRetry {
    param(
        [Parameter(Mandatory)]
        [string]$LiteralPath
    )

    for ($attempt = 1; $attempt -le 5; $attempt++) {
        if (-not (Test-Path -LiteralPath $LiteralPath)) { return }
        try {
            Remove-Item -LiteralPath $LiteralPath -Recurse -Force -ErrorAction Stop
            return
        }
        catch [System.IO.IOException] {
            if ($attempt -eq 5) { throw }
            Start-Sleep -Milliseconds 200
        }
    }
}

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
    $consumerPackages = Join-Path $outputDirectory "consumer-packages"
    [void](New-Item -ItemType Directory -Path $consumerDirectory -Force)
    $consumerProject = Join-Path $consumerDirectory "PackedConsumer.proj"
    $consumerProgram = Join-Path $consumerDirectory "Program.cs"
    [System.IO.File]::WriteAllText($consumerProject, @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LanguageTargets>`$(MSBuildToolsPath)\Microsoft.CSharp.targets</LanguageTargets>
  </PropertyGroup>
  <ItemGroup><PackageReference Include="PlcComm.KvHostLink" Version="$packageVersion" /></ItemGroup>
</Project>
"@)
    [System.IO.File]::WriteAllText($consumerProgram, @"
using System;
using System.Linq;
using System.Threading;
using PlcComm.KvHostLink;
var codecs = Enum.GetValues<HostLinkCommentEncoding>();
if (!codecs.SequenceEqual(new[] { HostLinkCommentEncoding.Utf8, HostLinkCommentEncoding.Cp932 }))
    throw new InvalidOperationException("Unexpected comment codec surface.");
if (typeof(KvHostLinkClient).GetMethod("ReadCommentsAsync", new[] { typeof(string), typeof(CancellationToken) }) is not null)
    throw new InvalidOperationException("Implicit comment decoder remains public.");
if (typeof(KvHostLinkClient).GetMethod("ReadCommentsAsync", new[] { typeof(string), typeof(HostLinkCommentEncoding), typeof(CancellationToken) }) is null)
    throw new InvalidOperationException("Explicit comment decoder is missing.");
if (typeof(KvHostLinkClient).GetMethod("ReadCommentBytesAsync", new[] { typeof(string), typeof(CancellationToken) }) is null)
    throw new InvalidOperationException("Raw comment payload API is missing.");
Console.WriteLine($"{typeof(KvHostLinkClient).FullName}:{string.Join(',', codecs)}");
"@)
    & dotnet restore $consumerProject --source $outputDirectory --packages $consumerPackages --no-cache --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw "Packed NuGet consumer restore failed." }
    & dotnet build $consumerProject --no-restore --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw "Packed NuGet consumer build failed." }
    $consumerAssembly = Join-Path $consumerDirectory "bin\Debug\net8.0\PackedConsumer.dll"
    & dotnet $consumerAssembly
    if ($LASTEXITCODE -ne 0) { throw "Packed NuGet consumer run failed." }

    Write-Host "[OK] NuGet package content/consumer contract passed: package=$($packages[0].Name) files=$($files.Count) consumer=net8.0"
}
finally {
    if (Test-Path -LiteralPath $outputDirectory) {
        Remove-DirectoryWithRetry -LiteralPath $outputDirectory
    }
}
