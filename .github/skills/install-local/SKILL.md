---
name: install-local
description: Download and install the Expressif Language Server VSIX from an open or closed GitHub pull request, a named release, or the latest release into VS Code Insiders. Use when testing a CI-built or released extension locally; do not use for building a VSIX from source.
---

# Install Local

Use `scripts/install-local.ps1` to resolve, download, and install the requested VSIX. The script requires authenticated GitHub CLI access (`gh auth status`) and the VS Code Insiders CLI (`code-insiders`).

## Select the source

- For a pull request, run `./scripts/install-local.ps1 -PullRequest <number>`. An open PR resolves the newest successful `build.yml` pull-request run for the PR head SHA and downloads its `vscode-extension-*` artifact. A merged PR resolves the GitHub release whose tag points to the merge commit. A closed, unmerged PR is not installable.
- For a named release, run `./scripts/install-local.ps1 -Release <tag>`.
- For the latest release, run `./scripts/install-local.ps1`, or pass `-Release latest` explicitly.

Run the command from this skill directory, or invoke the script by its full path. Pass `-DownloadDirectory <path>` when the downloaded VSIX must be kept in a particular directory; otherwise the script uses a unique directory beneath the system temporary directory.

Report the selected PR run or release tag and the installed VSIX path. If no successful, unexpired PR artifact or commit-matching release exists, stop and explain that specific condition instead of silently falling back to another source.
