$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$xmlPath = Join-Path $root "src\PlcComm.KvHostLink\bin\Debug\net9.0\PlcComm.KvHostLink.xml"

if (-not (Test-Path $xmlPath)) {
    Write-Error "XML documentation file not found: $xmlPath"
}

[xml]$doc = Get-Content -Path $xmlPath
$members = @{}
foreach ($member in $doc.doc.members.member) {
    $members[$member.name] = $member
}

function Get-NodeText {
    param(
        [System.Xml.XmlElement]$Member,
        [string]$XPath
    )

    $node = $Member.SelectSingleNode($XPath)
    if ($null -eq $node) {
        return $null
    }

    return ($node.InnerText -replace "\s+", " ").Trim()
}

$expected = @(
    @{
        Name = 'T:PlcComm.KvHostLink.KvHostLinkClientExtensions'
        Tags = @("summary", "remarks")
    },
    @{
        Name = 'M:PlcComm.KvHostLink.KvHostLinkClientExtensions.ReadTypedAsync(PlcComm.KvHostLink.KvHostLinkClient,System.String,System.String,System.Threading.CancellationToken)'
        Tags = @("summary", "returns", "remarks")
        Params = @("client", "device", "dtype", "ct")
    },
    @{
        Name = 'M:PlcComm.KvHostLink.KvHostLinkClientExtensions.WriteTypedAsync``1(PlcComm.KvHostLink.KvHostLinkClient,System.String,System.String,``0,System.Threading.CancellationToken)'
        Tags = @("summary", "remarks")
        Params = @("client", "device", "dtype", "value", "ct")
    },
    @{
        Name = 'M:PlcComm.KvHostLink.KvHostLinkClientExtensions.WriteBitInWordAsync(PlcComm.KvHostLink.KvHostLinkClient,System.String,System.Int32,System.Boolean,System.Threading.CancellationToken)'
        Tags = @("summary", "remarks")
        Params = @("client", "device", "bitIndex", "value", "ct")
    },
    @{
        Name = 'M:PlcComm.KvHostLink.KvHostLinkClientExtensions.ReadNamedAsync(PlcComm.KvHostLink.KvHostLinkClient,System.Collections.Generic.IEnumerable{System.String},System.Threading.CancellationToken)'
        Tags = @("summary", "returns", "remarks")
        Params = @("client", "addresses", "ct")
    },
    @{
        Name = 'M:PlcComm.KvHostLink.KvHostLinkClientExtensions.PollAsync(PlcComm.KvHostLink.KvHostLinkClient,System.Collections.Generic.IEnumerable{System.String},System.TimeSpan,System.Threading.CancellationToken)'
        Tags = @("summary", "remarks")
        Params = @("client", "addresses", "interval", "ct")
    },
    @{
        Name = 'M:PlcComm.KvHostLink.KvHostLinkClientExtensions.ReadWordsAsync(PlcComm.KvHostLink.KvHostLinkClient,System.String,System.Int32,System.Threading.CancellationToken)'
        Tags = @("summary", "returns", "remarks")
        Params = @("client", "device", "count", "ct")
    },
    @{
        Name = 'M:PlcComm.KvHostLink.KvHostLinkClientExtensions.ReadDWordsAsync(PlcComm.KvHostLink.KvHostLinkClient,System.String,System.Int32,System.Threading.CancellationToken)'
        Tags = @("summary", "returns", "remarks")
        Params = @("client", "device", "count", "ct")
    },
    @{
        Name = 'M:PlcComm.KvHostLink.KvHostLinkClientExtensions.OpenAndConnectAsync(System.String,System.Int32,System.Threading.CancellationToken)'
        Tags = @("summary", "returns", "remarks")
        Params = @("host", "port", "ct")
    }
)

$errors = New-Object System.Collections.Generic.List[string]

foreach ($entry in $expected) {
    $member = $members[$entry.Name]
    if ($null -eq $member) {
        $errors.Add("Missing XML docs member: $($entry.Name)")
        continue
    }

    foreach ($tag in $entry.Tags) {
        $text = Get-NodeText -Member $member -XPath $tag
        if ($null -eq $text) {
            $errors.Add("Member $($entry.Name) is missing <$tag>.")
            continue
        }

        if ([string]::IsNullOrWhiteSpace($text)) {
            $errors.Add("Member $($entry.Name) has an empty <$tag>.")
        }
    }

    if ($entry.ContainsKey("Params")) {
        foreach ($paramName in $entry.Params) {
            $paramText = Get-NodeText -Member $member -XPath "param[@name='$paramName']"
            if ($null -eq $paramText) {
                $errors.Add("Member $($entry.Name) is missing <param name=`"$paramName`">.")
                continue
            }

            if ([string]::IsNullOrWhiteSpace($paramText)) {
                $errors.Add("Member $($entry.Name) has an empty <param name=`"$paramName`">.")
            }
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Host "[ERROR] High-level XML docs coverage check failed." -ForegroundColor Red
    foreach ($message in $errors) {
        Write-Host " - $message" -ForegroundColor Red
    }
    exit 1
}

Write-Host "[OK] High-level XML docs coverage check passed."
