# Sunrise Installer

Installer for [Sunrise](https://github.com/stanuwu/Sunrise).

## License

Sunrise Installer is Copyright (C) 2026 stanuwu. It is licensed under version 2 of the
[GNU General Public License](LICENSE). The full terms are stored with the project.

## DepotDownloader Credit and License

Sunrise Installer uses [DepotDownloader](https://github.com/SteamRE/DepotDownloader) to download Steam depots. DepotDownloader is developed by the SteamRE Team and uses SteamKit2.
Copyright for DepotDownloader belongs to its authors and contributors.

DepotDownloader is also licensed under GNU GPL version 2. Its full license is stored in
[DEPOTDOWNLOADER_LICENSE.txt](DEPOTDOWNLOADER_LICENSE.txt). The published Sunrise Installer contains
no DepotDownloader binary. DepotDownloader is not linked and is not a package or project dependency.

On first use, Sunrise Installer downloads the official `DepotDownloader-windows-x64.zip` release
from SteamRE into `%LOCALAPPDATA%`. It checks the GitHub digest when supplied, then extracts and
starts DepotDownloader as a separate program.

## Logo credit

Logo credit: [Solus](https://www.youtube.com/@Solus-yt).

## Operations

| action         | result                                                              |
|----------------|---------------------------------------------------------------------|
| Install        | Downloads the correct version of the game and installs the mod.     |
| Repair         | Validates the game, deletes the user config and reinstalls the mod. |
| Check / Update | Checks if a new mod version is released and installs it.            |

Install requires ~110 GiB of free space.

## Game version

Sunrise Installer uses Steam app `1085660`. A game version is the pair of depot manifests the install is
pinned to, chosen with the `Game version` box:

| version                                     | depot     | manifest              |
|---------------------------------------------|-----------|-----------------------|
| Season of Arrivals (build 86657, 23 Aug 2020) | `1085661` | `7180122903232116872` |
|                                             | `1085662` | `2210332166360342287` |

Picking `Custom...` accepts a manifest id per depot instead. Manifest ids come from the depot history on
steamdb.info. Sunrise is built against one game build, so another build downloads and installs fine but
will very likely break the mod.

The installed build is read from `destiny2.exe` and named after the season it was compiled in, so a
custom install still reads as `Season of Arrivals, build 86657 (23 Aug 2020)` rather than a manifest id.

One folder holds one version. To keep two versions, install each into its own folder: the install state,
Repair and Check / Update are all per folder. Repair and Check / Update reuse the manifests the folder was
installed with, so they never quietly convert an install to another version. Install is what changes the
version of a folder.

## Sunrise releases

Downloads latest from `https://github.com/stanuwu/Sunrise/releases`.
## Test mode

Run the installer with a local Sunrise DLL:

```powershell
.\SunriseInstaller.exe -test "C:\path\to\steam_api64.dll"
```

## Local data
`%LOCALAPPDATA%\SunriseInstaller\tools`. 

`%LOCALAPPDATA%\SunriseInstaller\logs`.
