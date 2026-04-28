<!--
  Replace this template with what you want in the release body when you cut
  a tag manually. The CI workflow uses `generate_release_notes: true`, which
  produces the change list from merged PRs/commits — but you can edit the
  release on GitHub afterwards and paste sections from this template.
-->

## ✨ What's new


## 🐛 Bug fixes


## 🔒 Security


## ⚠️ Breaking changes


---

### Downloads

| Build | When to pick |
|---|---|
| `SW-LOG-vX.Y.Z-net9-win-x64.zip` | Recommended for new installs |
| `SW-LOG-vX.Y.Z-net6-win-x64.zip` | If your environment is locked to .NET 6 |

Both archives are **self-contained** — no .NET runtime install required.
Unzip and run `ScalanceLogs.exe`.

### Verifying

```powershell
Get-FileHash .\ScalanceLogs.exe -Algorithm SHA256
```

### Upgrading

Settings live at `%APPDATA%\ScalanceLogs\settings.json` and are preserved between versions.
