# BUILD.md — build, test, run, package

Machine used for verification: Windows 11 Pro x64 (26200), .NET SDK 10.0.300, VS 2026 installed
(but not required for these commands), Windows App Runtime 1.8 installed system-wide.

## 1. Test

```powershell
dotnet test src/SixConsole.Tests
```
Covers: source-vs-catalogue integrity (independent regex parse of the Lua fixture),
defaults, rounding, navigation/wraparound, settings validation (incl. rejected drafts),
preserved quirks (Solis SMG-11 pull 48 etc.).

## 2. Debug build & run

```powershell
dotnet build src/SixConsole.App/SixConsole.App.csproj -c Debug
& src/SixConsole.App/bin/Debug/net10.0-windows10.0.26100.0/win-x64/SixConsole.App.exe
```

Notes:
- Build with the plain `dotnet build` CLI. (VS MSBuild `msbuild` needs the C++ workload — the
  XAML compiler's `GetLatestMSVCVersion` task enumerates `VC\Tools\MSVC` — absent here.)
- After structural XAML changes a **clean** rebuild is required for the XAML compiler's
  pass-2 LocalAssembly to regenerate; otherwise the app can crash at launch with an
  "Unknown type" error (observed & documented).
- Unpackaged, framework-dependent: binds to the installed Windows App Runtime 1.8 with NO admin.

## 3. Release, self-contained (no-install distribution path)

```powershell
dotnet publish src/SixConsole.App/SixConsole.App.csproj -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true
# output: src/SixConsole.App/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish/
```
Zip the publish folder and share it; the recipient runs `SixConsole.App.exe`. Nothing is installed.

## 4. MSIX package path (optional)

```powershell
# with VS msbuild (requires the 'Desktop development with C++' workload for WinUI tooling)
msbuild src/SixConsole.App/SixConsole.App.csproj /restore /p:Configuration=Release /p:Platform=x64 `
  /p:AppxPackage=true /p:GenerateAppxPackageOnBuild=true /p:AppxPackageSigningEnabled=false
# unsigned .msix lands in AppPackages\...; sign it with your own cert via signtool and trust it
# in Local Machine\Trusted People (admin) before Add-AppxPackage.
```
`makeappx`/`signtool` verified present at
`C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64`.

`Package.appxmanifest` declares Identity `SixConsole`, logo assets under `Assets\`,
`runFullTrust`, MinVersion 10.0.19041, Target 10.0.26100, exe name `SixConsole.App.exe`.

## 5. Housekeeping

- Regenerate the catalogue after editing the Lua source (do-not-edit otherwise):
  ```powershell
  python tools/generate-catalogue.py src/SixConsole.Tests/SourceFixtures/lua-source.txt src/SixConsole.Profile/Data/operators.json
  ```
- App window screenshots for review: `tools/shot-window.ps1` (PrintWindow with screen fallback), 
  `tools/enum-windows.ps1` for window diagnostics.
