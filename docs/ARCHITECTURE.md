# notes & architecture

## save format (`saveVersion: 17`)

`.wbox` files are raw zlib-compressed UTF-8 JSON.

* **chunks:** `width` and `height` are in chunks (`64 × 64` tiles each).
* **tiles:** `tileMap` holds tile strings (`deep_ocean`, `soil_high:grass_high`, etc.), and `tileArray[y]` / `tileAmounts[y]` run-length encode each row from `y = 0` (bottom) to `height * 64 - 1`.
* **flat tile indices:** `fire`, `frozen_tiles`, `conwayEater`, and `conwayCreator` use `idx = y * width_tiles + x`.
* **zones:** city territory (`cities[].zones[]`) is in `8 × 8`-tile zones (`8 × 8` zones per chunk).
* **zero omission:** WorldBox serializes JSON with default values omitted, so missing `x`/`y`/`mainX`/`mainY` keys mean `0`.
* **id counters:** `mapStats` keeps monotonic `id_*` counters for every entity collection. non-positive IDs (`<= 0`) are unset references and aren't shifted when stitching.

## mod build setup (`mods/`)

all 7 C# mods inherit from `mods/Directory.Build.props`, which turns off implicit framework references and compiles directly against WorldBox's own `mscorlib.dll`, `netstandard.dll`, `System.dll`, `System.Core.dll`, `Assembly-CSharp.dll`, `UnityEngine*.dll`, `Newtonsoft.Json.dll`, `0Harmony.dll`, and `NeoModLoader.dll` in `local/libs/`.

when installed into WorldBox's `Mods/` folder, NeoModLoader compiles each mod's `Code/*.cs` directly at startup.
