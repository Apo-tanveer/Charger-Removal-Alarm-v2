# Charger Removal Alarm v2

A simple and elegant cross-platform utility application that alerts you when your computer's charger is unplugged.

## Features
- **Charger Monitoring:** Instantly detects when your power source changes from AC to battery.
- **Audio Alarm:** Generates and plays a custom synthesized WAV alert when the charger is disconnected.
- **Cross-Platform Compatibility:**
  - **Windows:** Custom-painted WinForms interface ([Program.cs](file:///home/tanveer/Desktop/hello/Program.cs)).
  - **macOS:** Cocoa application logic ([MacProgram.cs](file:///home/tanveer/Desktop/hello/MacProgram.cs)).
  - **Linux:** Linux target compatibility ([ChargerRemovalAlarm.Linux.csproj](file:///home/tanveer/Desktop/hello/ChargerRemovalAlarm.Linux.csproj)).
- **Custom UI Controls:** Premium, clean, custom-rendered components for a sleek look.

## How to Build & Run

### Windows
Open `ChargerRemovalAlarm.csproj` in Visual Studio or run:
```bash
dotnet build
dotnet run
```

### macOS
Run the build script:
```bash
chmod +x build_mac.sh
./build_mac.sh
```

### Linux
Run:
```bash
dotnet build ChargerRemovalAlarm.Linux.csproj
```
