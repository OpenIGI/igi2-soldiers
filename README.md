# I.G.I. 2 soldiers

An [OpenIGI](https://github.com/OpenIGI/OpenIGI) mod: Project I.G.I.'s missions guarded by I.G.I. 2's soldiers, with
their bodies, weapons and AI. The mod's id is `igi2soldiers`. It needs an installed copy of I.G.I. 2 as well as
Project I.G.I., and the launcher's Mods page installs it from the community index.

## Building

The mod builds against OpenIGI in one of two ways:

- **From source.** Point `OPENIGI_ROOT` at a checkout of OpenIGI, or clone it as `igi-remastered` next to this
  repository. The mod then references the game's own projects, and builds in the configuration you ask for.
- **From a release.** With no checkout found, the build restores the `OpenIGI.Modding.Sdk` package at the version pinned in
  `Directory.Packages.props`. The package carries the same assemblies.

```sh
dotnet build -c Release OpenIGI.Mods.Igi2Soldiers.slnx
```

Every build writes the installable package, the folder the launcher puts in place, to
`src/OpenIGI.Mods.Igi2Soldiers/bin/Release/net10.0/mod/`. To try it in the game, copy that folder into the launcher's
`<Library>/mods/local/`, where the Mods page lists it as a local mod.

## Testing

```sh
IGI_GAME_PATH=... IGI2_GAME_PATH=... dotnet test -c Release --no-build OpenIGI.Mods.Igi2Soldiers.slnx
```

The tests need a checkout of OpenIGI, because they use the game's own test support and mod loader. A test that needs
the retail data of either game skips itself when that game's path isn't set.

## Publishing

A version is published through OpenIGI's mods index: a pull request that adds `mods/igi2soldiers/<version>.json`, naming
this repository's commit. The index's CI builds the mod from that commit, never from an author's build. OpenIGI's
`sdk/README.md` covers the format and the rules.
