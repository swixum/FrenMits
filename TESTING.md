# Testing Fren Mits

## 1. Build it

You need the **.NET 10 SDK** (your Dalamud runs on net10) and a Dalamud install at
the default path. From the project folder:

```powershell
dotnet build -c Debug
```

Output: `bin\x64\Debug\FrenMits.dll` with `FrenMits.json` next to it. If the build
can't find Dalamud refs, pass the path explicitly:

```powershell
dotnet build -c Debug -p:DalamudLibPath="$env:AppData\XIVLauncher\addon\Hooks\dev\"
```

## 2. Load it as a dev plugin

1. In game: `/xlsettings` → **Experimental** → **Dev Plugin Locations** → **+** →
   add the folder `...\FrenMits\bin\x64\Debug` (the folder, not the dll). Save.
2. `/xlplugins` → **Dev Tools** (or scroll the list) → enable **Fren Mits**.
3. `/fm` opens the config.

When you rebuild, Dalamud hot-reloads dev plugins automatically. If it doesn't, toggle
the plugin off/on in `/xlplugins`, or use the reload button on the dev-plugins list.

## 3. Test without a real pull

- **Overlay look/placement:** `/fm test` (or the **Test** checkbox). A sample call
  appears so you can size/colour/drag it. Unlock on the Display tab to move it.
- **Audio:** Audio tab → **Test voice** / **Test beep**.
- **Built-in mits:** Fights tab → pick the ultimate → **Your slot** → **Load mits**.
  Open the line table to confirm the timeline + icons populated.
- **Timeline + resync without going in:** use the in-game **Duty Recorder**
  (`/duty` recordings) or a **replay** of the fight. Play it back — the plugin runs
  exactly as in a live pull: combat starts the timer, boss casts resync the clock,
  cues fire. Watch the **Timer** tab's "Last sync" line to confirm anchors are firing.
- **Capture anchors:** Timer tab → **Record boss casts this pull** during a replay to
  log casts/boss appearances, then **+phase / +mech / +boss anchor** to build P4/P5
  (or any) anchors.

## 4. Iterate

Edit code → `dotnet build` → Dalamud reloads → re-test. The config persists in
`%AppData%\XIVLauncher\pluginConfigs\FrenMits.json`; delete it to start fresh.

## Quick smoke checklist

- [ ] `/fm` opens, status header shows job/zone/timer.
- [ ] Test mode shows the sample call with an icon; dragging + lock work.
- [ ] Test voice + test beep produce sound.
- [ ] Load mits for your slot fills the line table (icons resolve).
- [ ] In a replay: timer starts on combat, calls fire ~3s early, "Last sync" updates,
      DTR bar shows the next mit, it all resets on wipe.
