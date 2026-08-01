[CmdletBinding()]
param(
    [string]$Treeish = "HEAD",
    [switch]$UseWorktreeAttributes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$workspaceRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $repositoryRoot))
$workingRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $workspaceRoot (".plc-hostlink-source-archive-" + [guid]::NewGuid().ToString("N"))))
$workspacePrefix = $workspaceRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (-not $workingRoot.StartsWith($workspacePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use source-archive work directory outside the workspace: $workingRoot"
}
$archivePath = Join-Path $workingRoot "source.zip"
$extractRoot = Join-Path $workingRoot "extracted"
$stageRoot = Join-Path $workingRoot "staged"

function Invoke-ArchiveCommand {
    param(
        [Parameter(Mandatory)]
        [string]$Executable,
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Archive command failed ($LASTEXITCODE): $Executable $($Arguments -join ' ')"
    }
}

try {
    New-Item -ItemType Directory -Path $workingRoot | Out-Null
    & git -C $repositoryRoot rev-parse --verify "$Treeish`^{tree}" *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot resolve treeish '$Treeish'."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $worktreeFiles = @()
    if ($UseWorktreeAttributes) {
        $worktreeFiles = @(& git -C $repositoryRoot ls-files --cached --others --exclude-standard |
            ForEach-Object { $_.Replace("\", "/") } |
            Where-Object {
                (Test-Path -LiteralPath (Join-Path $repositoryRoot $_) -PathType Leaf) -and
                $_ -notin @(".gitattributes", ".gitignore") -and
                $_ -notmatch '^(build|build_win|release-artifacts)/'
            } |
            Sort-Object -Unique)
        if ($LASTEXITCODE -ne 0) { throw "Cannot enumerate current worktree files." }
        [void](New-Item -ItemType Directory -Path $stageRoot)
        foreach ($path in $worktreeFiles) {
            $destination = Join-Path $stageRoot $path
            [void](New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force)
            Copy-Item -LiteralPath (Join-Path $repositoryRoot $path) -Destination $destination -Force
        }
        [System.IO.Compression.ZipFile]::CreateFromDirectory($stageRoot, $archivePath)
    }
    else {
        & git -C $repositoryRoot archive --format=zip --output=$archivePath $Treeish
        if ($LASTEXITCODE -ne 0) { throw "git archive failed for '$Treeish'." }
    }
    if (-not (Test-Path -LiteralPath $archivePath)) {
        throw "Source archive was not created for '$Treeish'."
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $archiveFiles = @($archive.Entries |
            Where-Object { -not $_.FullName.EndsWith("/") } |
            ForEach-Object { $_.FullName.Replace("\", "/") } |
            Sort-Object -Unique)
    }
    finally {
        $archive.Dispose()
    }

    $trackedFiles = if ($UseWorktreeAttributes) { $worktreeFiles } else {
        @(& git -C $repositoryRoot ls-tree -r --name-only $Treeish |
            ForEach-Object { $_.Replace("\", "/") } |
            Sort-Object -Unique)
    }
    if ($LASTEXITCODE -ne 0) { throw "Cannot enumerate source files for '$Treeish'." }

    $requiredTracked = @($trackedFiles | Where-Object {
        $_ -match '^(test|tests|\.github|docsrc/maintainer|internal_docs|scripts|tools)/' -or
        $_ -in @("AGENTS.md", "TODO.md", "release_check.bat", "run_ci.bat")
    })
    $missingTracked = @($requiredTracked | Where-Object { $_ -notin $archiveFiles })
    if ($missingTracked.Count -ne 0) {
        throw "Source archive omits tracked validation or maintainer material: $($missingTracked -join ', ')"
    }

    foreach ($guide in @("GETTING_STARTED.md", "USAGE_GUIDE.md", "PROFILES.md", "GOTCHAS.md", "API_REFERENCE.md")) {
        $guideCandidates = @("docsrc/user/$guide", "docs/$guide")
        if (@($guideCandidates | Where-Object { $_ -in $archiveFiles }).Count -eq 0) {
            throw "Source archive is missing standard user guide '$guide'."
        }
    }

    $forbiddenFileNames = @(
        ".gitattributes",
        ".gitignore"
    )
    $forbiddenPrefixes = @(
        ".codex/",
        ".pio/",
        ".tools/",
        "build/",
        "build_win/",
        "local_folder/",
        "release-artifacts/"
    )
    $forbidden = @($archiveFiles | Where-Object {
        $fileName = [System.IO.Path]::GetFileName($_)
        $path = $_
        $forbiddenFileNames -contains $fileName -or
            @($forbiddenPrefixes | Where-Object {
                $path.StartsWith($_, [System.StringComparison]::OrdinalIgnoreCase)
            }).Count -ne 0
    })
    if ($forbidden.Count -ne 0) {
        throw "Source archive contains forbidden generated or release-output files: $($forbidden -join ', ')"
    }

    $requiredFiles = @(
        "CHANGELOG.md",
        "Directory.Build.props",
        "LICENSE",
        "PlcComm.KvHostLink.sln",
        "README.md",
        "internal_docs/maintainer/api_baselines/PlcComm.KvHostLink-3.2.1.json",
        "internal_docs/maintainer/documented_api_diff_classifications.json",
        "scripts/check_documented_api_diff.py",
        "scripts/check_high_level_docs.ps1",
        "scripts/check_package_contents.ps1",
        "scripts/check_sample_inventory.ps1",
        "scripts/generate_api_reference.py",
        "scripts/test_documented_api_diff.py",
        "scripts/test_generate_api_reference.py"
    )
    $missingRequired = @($requiredFiles | Where-Object { $_ -notin $archiveFiles })
    if ($missingRequired.Count -ne 0) {
        throw "Source archive is missing required files: $($missingRequired -join ', ')"
    }

    $expectedTests = @($trackedFiles | Where-Object { $_.StartsWith("tests/") } | Sort-Object -Unique)
    if ($expectedTests.Count -eq 0) {
        throw "Cannot enumerate a nonempty tracked test set for '$Treeish'."
    }
    $actualTests = @($archiveFiles |
        Where-Object { $_.StartsWith("tests/", [System.StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object -Unique)
    $testDifference = @(Compare-Object -ReferenceObject $expectedTests -DifferenceObject $actualTests -CaseSensitive)
    if ($testDifference.Count -ne 0) {
        $differenceText = ($testDifference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }) -join "; "
        throw "Source archive test set differs from the tracked test set: $differenceText"
    }

    $expectedSamples = @($trackedFiles | Where-Object { $_.StartsWith("samples/") } | Sort-Object -Unique)
    if ($expectedSamples.Count -eq 0) {
        throw "Cannot enumerate a nonempty tracked sample set for '$Treeish'."
    }
    $actualSamples = @($archiveFiles |
        Where-Object { $_.StartsWith("samples/", [System.StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object -Unique)
    $sampleDifference = @(Compare-Object -ReferenceObject $expectedSamples -DifferenceObject $actualSamples -CaseSensitive)
    if ($sampleDifference.Count -ne 0) {
        $differenceText = ($sampleDifference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }) -join "; "
        throw "Source archive sample set differs from the tracked sample set: $differenceText"
    }

    [System.IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $extractRoot)
    Push-Location $extractRoot
    try {
        Invoke-ArchiveCommand dotnet @("restore", "PlcComm.KvHostLink.sln")
        Invoke-ArchiveCommand dotnet @(
            "restore",
            "samples/PlcComm.KvHostLink.BasicReadWriteSample/PlcComm.KvHostLink.BasicReadWriteSample.csproj")
        Invoke-ArchiveCommand dotnet @(
            "restore",
            "samples/PlcComm.KvHostLink.NamedPollingSample/PlcComm.KvHostLink.NamedPollingSample.csproj")
        Invoke-ArchiveCommand dotnet @("build", "PlcComm.KvHostLink.sln", "-c", "Release", "--no-restore")
        Invoke-ArchiveCommand python @("scripts/test_documented_api_diff.py")
        Invoke-ArchiveCommand dotnet @(
            "test",
            "tests/PlcComm.KvHostLink.Tests/PlcComm.KvHostLink.Tests.csproj",
            "-c", "Release",
            "--no-build",
            "--logger", "trx;LogFilePrefix=archive-tests")

        $trxPaths = @(Get-ChildItem -Path "tests/PlcComm.KvHostLink.Tests/TestResults" -Filter "archive-tests*.trx" -Recurse)
        if ($trxPaths.Count -ne 3) {
            throw "Archive test run produced $($trxPaths.Count) TRX files; expected one for each of three target frameworks."
        }
        $testResultCount = 0
        foreach ($trxPath in $trxPaths) {
            [xml]$trx = Get-Content -LiteralPath $trxPath.FullName
            $testResultCount += @($trx.SelectNodes("//*[local-name()='UnitTestResult']")).Count
        }
        if ($testResultCount -eq 0) {
            throw "Archive test run executed zero tests."
        }

        Invoke-ArchiveCommand dotnet @(
            "format", "PlcComm.KvHostLink.sln", "--verify-no-changes", "--no-restore")
        Invoke-ArchiveCommand python @("scripts/test_generate_api_reference.py")
        Invoke-ArchiveCommand python @(
            "scripts/generate_api_reference.py",
            "--assembly", "src/PlcComm.KvHostLink/bin/Release/net8.0/PlcComm.KvHostLink.dll",
            "--xml", "src/PlcComm.KvHostLink/bin/Release/net8.0/PlcComm.KvHostLink.xml",
            "--output", "docsrc/user/API_REFERENCE.md",
            "--title", "KV Host Link .NET API Reference",
            "--package", "PlcComm.KvHostLink",
            "--check")
        Invoke-ArchiveCommand powershell @(
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            "scripts/check_high_level_docs.ps1", "-Configuration", "Release")
        Invoke-ArchiveCommand powershell @(
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            "scripts/check_sample_inventory.ps1")
        Invoke-ArchiveCommand dotnet @(
            "build",
            "samples/PlcComm.KvHostLink.BasicReadWriteSample/PlcComm.KvHostLink.BasicReadWriteSample.csproj",
            "-c", "Release", "--no-restore")
        Invoke-ArchiveCommand dotnet @(
            "build",
            "samples/PlcComm.KvHostLink.NamedPollingSample/PlcComm.KvHostLink.NamedPollingSample.csproj",
            "-c", "Release", "--no-restore")
        Invoke-ArchiveCommand powershell @(
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            "scripts/check_package_contents.ps1", "-Configuration", "Release", "-NoBuild")
    }
    finally {
        Pop-Location
    }

    Write-Host "[OK] Source archive contract passed: treeish=$Treeish files=$($archiveFiles.Count) tests=$testResultCount samples=$($actualSamples.Count)"
}
finally {
    if (Test-Path -LiteralPath $workingRoot) {
        Remove-Item -LiteralPath $workingRoot -Recurse -Force
    }
}
