[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$originalLocation = Get-Location

try {
    Set-Location $projectRoot

    $projectFile = 'AkashaNavigator\AkashaNavigator.csproj'
    $projectText = Get-Content -Raw -LiteralPath $projectFile
    $versionMatch = [regex]::Match(
        $projectText,
        '<Version>(?<version>[^<]+)</Version>')
    if (-not $versionMatch.Success) {
        throw "Could not read <Version> from $projectFile"
    }

    & gh auth status | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub CLI is not authenticated'
    }

    $repository = (& gh repo view --json nameWithOwner --jq '.nameWithOwner').Trim()
    if ($LASTEXITCODE -ne 0 -or $repository -ne 'ColinXHL/akasha-navigator') {
        throw "Unexpected GitHub repository: $repository"
    }

    $releaseJson = & gh release list `
        --exclude-drafts `
        --limit 1 `
        --json tagName,name,isPrerelease,publishedAt
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not read the latest GitHub Release'
    }

    $latestRelease = @($releaseJson | ConvertFrom-Json) |
        Select-Object -First 1
    $baseTag = $latestRelease.tagName

    $commits = @()
    $committedDiff = ''
    if (-not [string]::IsNullOrWhiteSpace($baseTag)) {
        $commits = @(
            & git log "$baseTag..HEAD" --pretty=format:'%h%x09%s'
        )
        if ($LASTEXITCODE -ne 0) {
            throw "Could not inspect commits since $baseTag"
        }

        $committedDiff = @(
            & git diff --stat "$baseTag..HEAD"
        ) -join "`n"
        if ($LASTEXITCODE -ne 0) {
            throw "Could not inspect diff since $baseTag"
        }
    }

    $workingTreeStatus = @(& git status --short)
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect the working tree'
    }

    $unstagedDiff = @(& git diff --stat) -join "`n"
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect unstaged changes'
    }

    $stagedDiff = @(& git diff --cached --stat) -join "`n"
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect staged changes'
    }

    $notice = Invoke-RestMethod `
        -Uri 'https://update.fisheepx.cn/notice.json'

    [ordered]@{
        repository = $repository
        projectVersion = $versionMatch.Groups['version'].Value
        latestRelease = if ($null -eq $latestRelease) {
            $null
        }
        else {
            [ordered]@{
                tag = $latestRelease.tagName
                name = $latestRelease.name
                isPrerelease = $latestRelease.isPrerelease
                publishedAt = $latestRelease.publishedAt
            }
        }
        onlineManifest = [ordered]@{
            stable = $notice.stable.version
            alpha = $notice.alpha.version
            minRequiredVersion = $notice.min_required_version
        }
        commitsSinceLatestRelease = $commits
        committedDiffSinceLatestRelease = $committedDiff
        workingTreeStatus = $workingTreeStatus
        stagedDiff = $stagedDiff
        unstagedDiff = $unstagedDiff
    } | ConvertTo-Json -Depth 6
}
finally {
    Set-Location $originalLocation
}
