# I.G.I. 2 soldiers

An [OpenIGI](https://github.com/OpenIGI/OpenIGI) mod: Project I.G.I.'s missions guarded by I.G.I. 2's soldiers, with
their bodies, weapons and AI. The mod's id is `igi2soldiers`. It needs an installed copy of I.G.I. 2 as well as
Project I.G.I., and the launcher's Mods page installs it from the community index.

## Building

The project is built with `OpenIGI.Modding.Sdk`, the project SDK `global.json` pins. It builds against OpenIGI's source
when a checkout is to hand -- the one `OPENIGI_ROOT` names, or `igi-remastered` beside this repository -- and against the
SDK's packages otherwise. Until the pinned version is on nuget.org, pack it from the checkout once into `sdk-packages/`
(see its README):

```sh
../igi-remastered/tools/release/pack-modding-sdk.sh 0.5.0 sdk-packages
dotnet build -c Release OpenIGI.Mods.Igi2Soldiers.slnx
```

The mod's `mod.json` is written from the project's properties, and every build writes the installable package, the folder
the launcher puts in place, to `src/OpenIGI.Mods.Igi2Soldiers/bin/Release/net10.0/mod/`. To try it in the game, copy that
folder into the launcher's `<Library>/mods/local/`, where the Mods page lists it as a local mod.

## Testing

```sh
IGI_GAME_PATH=... IGI2_GAME_PATH=... dotnet test -c Release --no-build OpenIGI.Mods.Igi2Soldiers.slnx
```

The tests are built with the mod's own SDK (`<ModTests>true</ModTests>`), against `OpenIGI.Modding.Testing`: the mod is
installed beside them as the launcher installs it, `ModHarness` configures it as the game does, and `MissionFixture`
loads Project I.G.I.'s missions headless. A test that needs the retail data of either game skips itself when that game's
path isn't set.

## Publishing

A version is published through OpenIGI's mods index: a pull request that adds `mods/igi2soldiers/<version>.json`, naming
this repository's commit. The index's CI builds the mod from that commit, never from an author's build. OpenIGI's
`sdk/README.md` covers the format and the rules.
