<h1 align="center">
  <img src="docs/icon.png" alt="" height="48"><br>
  Titanite
</h1>

<p align="center">
  <a href="https://github.com/adds39939/titanite/releases"><img src="https://img.shields.io/badge/Download-Flatpak-4A90D9?style=flat&logo=flatpak&logoColor=white" alt="Download Flatpak"></a>
  <a href="https://github.com/adds39939/titanite/releases/latest"><img src="https://img.shields.io/github/v/release/adds39939/titanite?include_prereleases&style=flat&label=Release" alt="Latest release"></a>
  <a href="https://github.com/adds39939/titanite/releases"><img src="https://img.shields.io/github/downloads/adds39939/titanite/total?style=flat&label=Downloads" alt="Total downloads"></a><br>
  <a href="https://github.com/adds39939/titanite/actions/workflows/build.yml"><img src="https://img.shields.io/github/actions/workflow/status/adds39939/titanite/build.yml?style=flat&label=Build" alt="Build status"></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?style=flat&logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/SteamOS-Ready-1A9FFF?style=flat&logo=steamdeck&logoColor=white" alt="SteamOS Ready">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/adds39939/titanite?style=flat&label=Licence" alt="GPL-3.0 licence"></a>
</p>

<p align="center">Easy launch argument configuration for your Linux Steam games.</p>

<p align="center">
  <img src="docs/screenshot.png" alt="Titanite showing a Steam library" width="49%">
  <img src="docs/screenshot-settings.png" alt="Titanite showing a game's launch settings" width="49%">
</p>

Configure launch arguments for Proton, DXVK, Gamescope, MangoHud, HDR, upscalers and more for any game, all from a real settings page. Save presets and reuse them across games.

## Features

- Easy launch argument configuration for a wide range of game settings, such as Proton, DXVK, VKD3D, Gamescope, MangoHud, HDR, upscalers and GPU-specific options
- Choose the Proton build per game, including custom builds like GE-Proton
- Presets of environment variables and wrapper commands you can apply to any game
- Built-in updates

## How it works

Titanite writes launch options and Proton builds through Steam itself, so changes show up in Steam right away.

Everything Titanite sets ends up as plain launch options in Steam. There are no extra config files, wrapper scripts or background services, and nothing needs to be running when you play. You can see and edit the result in Steam's own properties window, and if you uninstall Titanite your games keep working exactly as you left them.

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

## Contributing

Bug reports and feature requests go in [issues](https://github.com/adds39939/titanite/issues). Pull requests are welcome.

## Licence

Titanite is licensed under the [GPL-3.0](LICENSE).
