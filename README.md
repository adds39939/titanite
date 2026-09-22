<h1 align="center">
  <img src="docs/icon.png" alt="" height="48"><br>
  Titanite
</h1>

<p align="center">
  <a href="https://github.com/adds39939/titanite/releases"><img src="https://img.shields.io/badge/Download-Flatpak-4A90D9?style=flat&logo=flatpak&logoColor=white" alt="Download Flatpak"></a>
  <a href="https://github.com/adds39939/titanite/releases/latest"><img src="https://img.shields.io/github/v/release/adds39939/titanite?include_prereleases&style=flat&label=Release" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/adds39939/titanite?style=flat&label=Licence" alt="GPL-3.0 licence"></a>
</p>

<p align="center">Easy launch argument configuration for your Linux Steam games.</p>

<p align="center">
  <img src="docs/screenshot.png" alt="Titanite showing a Steam library" width="49%">
  <img src="docs/screenshot-settings.png" alt="Titanite showing a game's launch settings" width="49%">
</p>

Configure launch arguments for Proton, DXVK, Gamescope, MangoHud, HDR, upscalers and more for any game, all from a real settings page. Save presets and reuse them across games.
## Install

Download the latest `.flatpak` from the [releases page](https://github.com/adds39939/titanite/releases), then run:

```sh
flatpak install --user Titanite-*-x86_64.flatpak
```

## Build from source

You'll need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Then build and run:

```sh
dotnet build Titanite.slnx
dotnet run --project src/Titanite.App
```

Run the tests with:

```sh
dotnet test Titanite.slnx
```
