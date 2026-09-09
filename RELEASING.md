# Releasing XIV Blackjack

Dalamud installs third-party plugins from a **custom repository** — a single `repo.json`
file on the internet that lists your plugins and where to download them. Users paste its
URL into Dalamud's settings once, and after that your plugins appear in their plugin
installer like any other, updates included.

## How the two repositories fit together

| Repository | Holds | Why |
| --- | --- | --- |
| [`FinalFantasyXIV`](https://github.com/NDokahara/FinalFantasyXIV) | `repo.json` only | The list Dalamud reads. One entry per plugin. Its URL is what dealers have saved, so **its name never changes.** |
| `XIVBlackjack` (this one) | Source + release zips | The plugin itself. Every future plugin gets its own repo the same way. |

So a release touches both: the zip is attached here, and the version number is bumped
there.

---

## Every release

### 1. Bump the version

In `XIVBlackjack/XIVBlackjack.csproj`:

```xml
<Version>0.2.1.0</Version>
```

Dalamud only offers an update when the version in `repo.json` is higher than what the user
has installed. Forgetting this is the single most common reason an update doesn't appear.

### 2. Build

```
dotnet build -c Release
```

Then open `XIVBlackjack/bin/x64/Release/XIVBlackjack.json`. The SDK generates that file and
fills in the authoritative values — check two of them against `repo.json`:

- **`AssemblyVersion`** must match exactly, or Dalamud refuses the install.
- **`DalamudApiLevel`** must match, or the plugin won't show up at all. No error, it is
  simply filtered out of the list. It is currently **15**, and it changes at every Dalamud
  major version, so re-check after any Dalamud update.

### 3. Package

The zip must contain the build output **at the root** — not inside a folder:

```
latest.zip
├── XIVBlackjack.dll
├── XIVBlackjack.json
├── ECommons.dll
├── images/
│   └── icon.png
└── Resources/
    └── Fonts/
        └── SourceCodePro-Medium.ttf
```

From PowerShell, in `XIVBlackjack/bin/x64/Release`:

```powershell
Remove-Item latest.zip -ErrorAction SilentlyContinue
Compress-Archive -Path *.dll, *.json, images, Resources -DestinationPath latest.zip -Force
```

Do not use `-Path *`. It sweeps up the previous `latest.zip` and any stray folders, so the
archive ends up containing a copy of itself and nested duplicates of the plugin.

`ECommons.dll` **must** be in there. It ships alongside rather than being merged in, and
without it the plugin throws on load with a missing-assembly error.

### 4. Commit the source, then tag a release

Push the source first, so the tag points at the code that produced the zip:

```
git add -A
git commit -m "0.2.1"
git push
```

Then: Releases → Draft a new release → tag `v0.2.1` → attach `latest.zip` → Publish.

The download links use `/releases/latest/download/latest.zip`, which always resolves to the
newest release, so the URLs never need touching — only the version number.

### 5. Bump `repo.json` in the list repository

In `FinalFantasyXIV`, set `AssemblyVersion` to the new version and push. Dalamud re-reads
the file on its own schedule, so dealers get the update without doing anything.

---

## Adding a second plugin later

1. New repository for it, with its own source and releases.
2. Add a second object to the array in `FinalFantasyXIV/repo.json`, with its own
   `InternalName` and download links.

Dealers do nothing — the plugin appears in their installer at the next refresh, because
they already have the list URL.

---

## When it doesn't work

**Plugin doesn't appear in the list at all.** Almost always `DalamudApiLevel` not matching
the current Dalamud. Check the generated manifest in your build output and copy the value
across.

**"Failed to install" or a version complaint.** `AssemblyVersion` in `repo.json` doesn't
match the DLL in the zip.

**Installs, then errors on load.** Usually `ECommons.dll` missing from the zip.

**Update never offered.** The version in `repo.json` isn't higher than the installed one.

**Nothing changes after you push.** Dalamud caches repo files. `/xlsettings` →
Experimental → toggle the repo off and on, or restart the game.

**`InternalName` changed between versions.** Dalamud treats it as a different plugin
entirely — separate config file, separate install. Changing it is a migration, not an
update. It changed once already, from `NandoBlackjack` to `XIVBlackjack` at 0.2.0.
