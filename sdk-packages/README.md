# sdk-packages

`OpenIGI.Modding.Sdk`, the project SDK this mod is built with, for a machine where the version `global.json` pins is not on
nuget.org yet. Pack it from a checkout of OpenIGI once:

```sh
../igi-remastered/tools/release/pack-modding-sdk.sh 0.5.0 sdk-packages
```

Built beside that checkout, the SDK reads its own files from it, so a change to the SDK needs no new pack. The packages
are not committed.
