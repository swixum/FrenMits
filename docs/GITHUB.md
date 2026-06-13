# Private repo + easy updates

## 1. Put it on GitHub (private)

From the `FrenMits` folder (install the GitHub CLI `gh` first, or use the website):

```bash
git init
git add .
git commit -m "Fren Mits: initial"

# private repo + push (gh CLI)
gh repo create FrenMits --private --source=. --remote=origin --push
```

Without `gh`: create an empty **private** repo on github.com, then:

```bash
git remote add origin https://github.com/YOUR_USERNAME/FrenMits.git
git branch -M main
git push -u origin main
```

`bin/` and `obj/` are already git-ignored.

## 2. Push updates

```bash
git add -A
git commit -m "what changed"
git push
```

That's the whole loop. For your own use you don't need anything fancier — see
"Personal updates" below.

## 3. Private repo, public link via GitHub Pages

This keeps your **source private** but serves the plugin zip + manifest at a **public**
Pages URL that Dalamud can fetch anonymously. (Publishing Pages from a private repo
needs **GitHub Pro/Team**; on Free, the repo must be public.)

`.github/workflows/build.yml` does it all: builds the plugin, zips it, copies
`repo.json` next to it, and deploys both to GitHub Pages on every push to `main`.

One-time setup:
1. Repo → **Settings → Pages → Build and deployment → Source: GitHub Actions**.
2. Edit `repo.json`: replace every `YOUR_USERNAME` with your GitHub username (the
   download links become `https://YOUR_USERNAME.github.io/FrenMits/FrenMits.zip`).
3. Commit + push to `main`. The Action builds and deploys. Your public files are:
   - manifest: `https://YOUR_USERNAME.github.io/FrenMits/repo.json`
   - plugin:   `https://YOUR_USERNAME.github.io/FrenMits/FrenMits.zip`

If CI can't find Dalamud types, your in-game Dalamud is on **staging** — change the
download URL in the workflow to `.../dalamud-distrib/stg/latest.zip`.

## 4. Install / auto-update in-game

1. In game: `/xlsettings` → **Experimental** → **Custom Plugin Repositories** → add
   `https://YOUR_USERNAME.github.io/FrenMits/repo.json` → Save.
2. `/xlplugins` → install **Fren Mits**.
3. Every push to `main` that bumps the version updates it in-game (Dalamud re-checks
   the manifest). No release/tag needed — Pages always serves the latest build.

## 5. Personal updates without Pages (simplest)

For solo use you don't even need Pages — point Dalamud **Dev Plugin Locations** at your
local `src\bin\x64\Release`, `git pull` + `dotnet build src`, and Dalamud hot-reloads. Repo
stays fully private, nothing published.

## Release checklist

Bump the version in **all three** places to the same value (Dalamud rejects the
install if the zip manifest and repo manifest disagree):
- [ ] `FrenMits.csproj` → `<Version>` / `<AssemblyVersion>` / `<FileVersion>`
- [ ] `FrenMits.json` → `AssemblyVersion`
- [ ] `repo.json` → `AssemblyVersion` (and `TestingAssemblyVersion`)
- [ ] `git commit && git push` — the Pages deploy publishes the new build automatically.
