[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [switch]$SkipTests,

    [switch]$AllowPublishedVersion
)

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$originalLocation = Get-Location

try {
    Set-Location $projectRoot

    $projectFile = 'AkashaNavigator\AkashaNavigator.csproj'
    $releaseNotesFile = 'release_badges.md'

    if (-not (Test-Path -LiteralPath $projectFile)) {
        throw "Project file not found: $projectFile"
    }

    if (-not (Test-Path -LiteralPath $releaseNotesFile)) {
        throw "Release notes not found: $releaseNotesFile"
    }

    $projectText = Get-Content -Raw -LiteralPath $projectFile
    $versionMatch = [regex]::Match(
        $projectText,
        '<Version>(?<version>[^<]+)</Version>')
    if (-not $versionMatch.Success) {
        throw "Could not read <Version> from $projectFile"
    }

    $projectVersion = $versionMatch.Groups['version'].Value
    if ($projectVersion -ne $Version) {
        throw "Requested version $Version does not match project version $projectVersion"
    }

    $releaseNotes = Get-Content -Raw -LiteralPath $releaseNotesFile
    if (-not $releaseNotes.Contains("## v$Version")) {
        throw "$releaseNotesFile does not contain a ## v$Version heading"
    }

    & gh --version | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub CLI is unavailable'
    }

    & gh auth status | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub CLI is not authenticated'
    }

    $repository = (& gh repo view --json nameWithOwner --jq '.nameWithOwner').Trim()
    if ($LASTEXITCODE -ne 0 -or $repository -ne 'ColinXHL/akasha-navigator') {
        throw "Unexpected GitHub repository: $repository"
    }

    & git diff --check
    if ($LASTEXITCODE -ne 0) {
        throw 'git diff --check failed'
    }

    $remoteTag = & git ls-remote --tags origin "refs/tags/v$Version"
    if (-not $AllowPublishedVersion -and -not [string]::IsNullOrWhiteSpace($remoteTag)) {
        throw "Tag v$Version already exists on origin"
    }

    & gh release view "v$Version" --json tagName 2>$null | Out-Null
    $releaseExists = $LASTEXITCODE -eq 0
    if (-not $AllowPublishedVersion -and $releaseExists) {
        throw "GitHub Release v$Version already exists"
    }

    if (-not $SkipTests) {
        & dotnet test AkashaNavigator.sln --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw 'Test suite failed'
        }
    }

    [PSCustomObject]@{
        Repository = $repository
        Version = $Version
        ProjectVersion = $projectVersion
        ReleaseNotesHeading = "## v$Version"
        TestsRun = -not $SkipTests
        ExistingReleaseAllowed = [bool]$AllowPublishedVersion
        Ready = $true
    } | Format-List
}
finally {
    Set-Location $originalLocation
}
