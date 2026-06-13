# One-shot GitHub setup for Fren Mits.
# Requires the GitHub CLI (https://cli.github.com). Run from the FrenMits folder:
#   powershell -ExecutionPolicy Bypass -File .\setup-github.ps1
#
# Creates a PRIVATE repo, pushes, enables Pages (GitHub Actions source), and
# fills your username into repo.json so the custom-repo links resolve.

$ErrorActionPreference = "Stop"
$repo = "FrenMits"

# 1. gh present + authenticated?
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "GitHub CLI not found. Install it from https://cli.github.com then re-run." -ForegroundColor Yellow
    Write-Host "Or do it manually:" -ForegroundColor Yellow
    Write-Host "  git remote add origin https://github.com/<you>/$repo.git"
    Write-Host "  git push -u origin main"
    exit 1
}
try { gh auth status 1>$null 2>$null } catch { gh auth login }

# 2. Username
$user = (gh api user --jq .login).Trim()
Write-Host "GitHub user: $user" -ForegroundColor Cyan

# 3. Fill username into repo.json (replace the placeholder).
(Get-Content repo.json -Raw).Replace("YOUR_USERNAME", $user) | Set-Content repo.json -NoNewline
git add repo.json
git commit -m "Set repo.json links to $user" 2>$null | Out-Null

# 4. Create the private repo + push (if origin not set).
$hasOrigin = (git remote 2>$null) -contains "origin"
if (-not $hasOrigin) {
    gh repo create $repo --private --source=. --remote=origin --push
} else {
    git push -u origin main
}

# 5. Enable Pages with the GitHub Actions build type.
try {
    gh api -X POST "repos/$user/$repo/pages" -f build_type=workflow 1>$null 2>$null
    Write-Host "Pages enabled (GitHub Actions source)." -ForegroundColor Green
} catch {
    Write-Host "Could not auto-enable Pages (needs GitHub Pro for a private repo, or it's already on)." -ForegroundColor Yellow
    Write-Host "Enable it at: Settings -> Pages -> Source: GitHub Actions" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done. After the first Actions run finishes, your public files are:" -ForegroundColor Green
Write-Host "  https://$user.github.io/$repo/repo.json"
Write-Host "  https://$user.github.io/$repo/FrenMits.zip"
Write-Host "Add the first URL to Dalamud -> Custom Plugin Repositories."
