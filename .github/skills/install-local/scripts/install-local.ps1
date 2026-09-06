[CmdletBinding(DefaultParameterSetName = 'Release')]
param(
    [Parameter(Mandatory, ParameterSetName = 'PullRequest')]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $PullRequest,

    [Parameter(Position = 0, ParameterSetName = 'Release')]
    [ValidateNotNullOrEmpty()]
    [string] $Release = 'latest',

    [ValidatePattern('^[^/]+/[^/]+$')]
    [string] $Repository = 'Seddryck/Expressif.LanguageServer',

    [string] $DownloadDirectory
)

$ErrorActionPreference = 'Stop'

function Assert-Command {
    param([Parameter(Mandatory)][string] $Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Invoke-GhJson {
    param([Parameter(Mandatory)][string[]] $Arguments)

    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI failed: gh $($Arguments -join ' ')"
    }

    return $output | ConvertFrom-Json
}

function Get-ReleaseForCommit {
    param(
        [Parameter(Mandatory)][string] $CommitSha,
        [Parameter(Mandatory)][string] $Repo
    )

    $page = 1
    do {
        $releases = @(Invoke-GhJson @('api', "/repos/$Repo/releases?per_page=100&page=$page"))
        foreach ($candidate in $releases) {
            if ($candidate.draft) {
                continue
            }

            $encodedTag = [uri]::EscapeDataString([string] $candidate.tag_name)
            $tagCommit = Invoke-GhJson @('api', "/repos/$Repo/commits/$encodedTag")
            if ($tagCommit.sha -eq $CommitSha) {
                return [string] $candidate.tag_name
            }
        }

        $page++
    } while ($releases.Count -eq 100)

    throw "No published release tag points to commit $CommitSha."
}

function Save-ReleaseVsix {
    param(
        [Parameter(Mandatory)][string] $Tag,
        [Parameter(Mandatory)][string] $Repo,
        [Parameter(Mandatory)][string] $Destination
    )

    & gh release download $Tag --repo $Repo --pattern '*.vsix' --dir $Destination
    if ($LASTEXITCODE -ne 0) {
        throw "Could not download a VSIX asset from release '$Tag'."
    }
}

function Save-PullRequestVsix {
    param(
        [Parameter(Mandatory)] $PullRequestData,
        [Parameter(Mandatory)][string] $Repo,
        [Parameter(Mandatory)][string] $Destination
    )

    $headSha = [string] $PullRequestData.head.sha
    $runs = Invoke-GhJson @('api', "/repos/$Repo/actions/workflows/build.yml/runs?event=pull_request&head_sha=$headSha&status=success&per_page=100")
    $run = @($runs.workflow_runs | Where-Object { $_.conclusion -eq 'success' } | Sort-Object created_at -Descending)[0]
    if (-not $run) {
        throw "No successful Build & Test workflow run exists for open PR #$($PullRequestData.number) at $headSha."
    }

    $artifacts = Invoke-GhJson @('api', "/repos/$Repo/actions/runs/$($run.id)/artifacts?per_page=100")
    $artifact = @($artifacts.artifacts | Where-Object {
        -not $_.expired -and $_.name -like 'vscode-extension-*-win-x64'
    } | Sort-Object created_at -Descending)[0]
    if (-not $artifact) {
        throw "Workflow run $($run.id) has no unexpired VS Code extension artifact."
    }

    Write-Host "Downloading artifact '$($artifact.name)' from workflow run $($run.id)."
    & gh run download $run.id --repo $Repo --name $artifact.name --dir $Destination
    if ($LASTEXITCODE -ne 0) {
        throw "Could not download artifact '$($artifact.name)' from workflow run $($run.id)."
    }
}

Assert-Command 'gh'
Assert-Command 'code-insiders'

if (-not $DownloadDirectory) {
    $DownloadDirectory = Join-Path ([IO.Path]::GetTempPath()) "Expressif.LanguageServer/install-local/$([guid]::NewGuid())"
}
$DownloadDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($DownloadDirectory)
New-Item -ItemType Directory -Path $DownloadDirectory -Force | Out-Null

if ($PSCmdlet.ParameterSetName -eq 'PullRequest') {
    $pr = Invoke-GhJson @('api', "/repos/$Repository/pulls/$PullRequest")
    if ($pr.state -eq 'open') {
        Save-PullRequestVsix -PullRequestData $pr -Repo $Repository -Destination $DownloadDirectory
    }
    elseif ($pr.merged_at) {
        $tag = Get-ReleaseForCommit -CommitSha ([string] $pr.merge_commit_sha) -Repo $Repository
        Write-Host "PR #$PullRequest was merged as $($pr.merge_commit_sha); downloading release '$tag'."
        Save-ReleaseVsix -Tag $tag -Repo $Repository -Destination $DownloadDirectory
    }
    else {
        throw "PR #$PullRequest is closed without being merged, so it has no corresponding release."
    }
}
else {
    if ($Release -eq 'latest') {
        $latest = Invoke-GhJson @('api', "/repos/$Repository/releases/latest")
        $tag = [string] $latest.tag_name
    }
    else {
        $tag = $Release
    }

    Write-Host "Downloading VSIX from release '$tag'."
    Save-ReleaseVsix -Tag $tag -Repo $Repository -Destination $DownloadDirectory
}

$vsixFiles = @(Get-ChildItem -Path $DownloadDirectory -Filter '*.vsix' -File -Recurse)
if ($vsixFiles.Count -ne 1) {
    throw "Expected exactly one downloaded VSIX in '$DownloadDirectory', but found $($vsixFiles.Count)."
}

$vsix = $vsixFiles[0].FullName
Write-Host "Installing '$vsix' into VS Code Insiders."
& code-insiders --install-extension $vsix --force
if ($LASTEXITCODE -ne 0) {
    throw 'VS Code Insiders failed to install the extension.'
}

Write-Host "Installed Expressif Language Support from '$vsix'."
