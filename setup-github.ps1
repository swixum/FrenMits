# One-shot GitHub setup for Fren Mits. Run from the FrenMits folder:
#   powershell -ExecutionPolicy Bypass -File .\setup-github.ps1
#
# Works with or without the GitHub CLI. With gh it creates the private repo and
# enables Pages automatically; without gh it walks you through the manual repo
# creation and does the rest (username -> repo.json, remote, push).

$ErrorActionPreference = "Stop"
$repo = "FrenMits"

function Set-RepoJsonUser($user) {
    (Get-Content repo.json -Raw).Replace("YOUR_USERNAME", $user) | Set-Content repo.json -NoNewline
    git add repo.json 2>$null | Out-Null
    git commit -m "Set repo.json links to $user" 2>$null | Out-Null
}

$gh = Get-Command gh -ErrorAction SilentlyContinue

# Offer to install gh via winget if it's missing.
if (-not $gh -and (Get-Command winget -ErrorAction SilentlyContinue)) {
    if ((Read-Host "GitHub CLI not found. Install it now with winget? (y/n)") -eq "y") {
        winget install --id GitHub.cli -e --source winget
        Write-Host "Installed. Close and reopen PowerShell, then re-run this script." -ForegroundColor Green
        exit 0
    }
}

if ($gh) {
    # ---- Automated path (gh) ----
    try { gh auth status 1>$null 2>$null } catch { gh auth login }
    $user = (gh api user --jq .login).Trim()
    Write-Host "GitHub user: $user" -ForegroundColor Cyan
    Set-RepoJsonUser $user
    if (-not ((git remote 2>$null) -contains "origin")) {
        gh repo create $repo --private --source=. --remote=origin --push
    } else {
        git push -u origin main
    }
    try {
        gh api -X POST "repos/$user/$repo/pages" -f build_type=workflow 1>$null 2>$null
        Write-Host "Pages enabled (GitHub Actions source)." -ForegroundColor Green
    } catch {
        Write-Host "Enable Pages manually: Settings -> Pages -> Source: GitHub Actions (needs Pro for a private repo)." -ForegroundColor Yellow
    }
}
else {
    # ---- Manual path (no gh) ----
    Write-Host ""
    Write-Host "No GitHub CLI. Do this first:" -ForegroundColor Yellow
    Write-Host "  1. Open https://github.com/new"
    Write-Host "  2. Repository name: $repo   |   Private   |   do NOT add a README/gitignore"
    Write-Host "  3. Click 'Create repository', then come back here."
    Read-Host "Press Enter once the empty repo exists"

    $user = Read-Host "Your GitHub username"
    Set-RepoJsonUser $user

    if (-not ((git remote 2>$null) -contains "origin")) {
        git remote add origin "https://github.com/$user/$repo.git"
    }
    Write-Host "Pushing (a browser/login prompt may appear)..." -ForegroundColor Cyan
    git push -u origin main

    Write-Host ""
    Write-Host "Now enable Pages: repo Settings -> Pages -> Source: GitHub Actions." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "After the first Actions run finishes, your public files are:" -ForegroundColor Green
Write-Host "  https://$user.github.io/$repo/repo.json   <- add this to Dalamud Custom Plugin Repositories"
Write-Host "  https://$user.github.io/$repo/FrenMits.zip"
