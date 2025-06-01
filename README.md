# RustEditProCrack

Automatic Unity assembly patcher that removes restrictions and enables PRO features.

## Features

- **PRO Mode**: Auto-patches obfuscated boolean methods via `DiscordPresence.UpdateActivity`
- **Unlock Prefabs**: Removes `File.Exists()` validation 
- **Remove Password**: Eliminates `WorldSaveLoad` password protection
- **Remove Blocks**: Patches `NJFSINOIPNMDA` class restrictions

## Usage

1. Place `Assembly-CSharp.dll` in `Managed/` folder
2. Build and run:
   ```bash
   dotnet build
   dotnet run -- "Managed/Assembly-CSharp.dll"
   ```
3. Get patched `Assembly-CSharp_Modifi.dll`

## Requirements

- .NET 6.0+
- Unity `Assembly-CSharp.dll`

## How it works

1. Finds obfuscated PRO class via "512_2" string in `DiscordPresence.UpdateActivity`
2. Patches boolean method to always return `true`
3. Removes file validation and password checks
4. Replaces blocking instructions with `NOP`

**Output**: Fully unlocked Unity assembly with all restrictions removed. 