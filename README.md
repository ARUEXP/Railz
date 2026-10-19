# Railz

> A modern overhaul of the Open Rails train simulator for Windows — reimagined with an AMOLED dark dashboard, ACES filmic tone mapping, interactive driving deck, live hardware telemetry, and 60 FPS pacing.

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D6?logo=windows)](https://microsoft.com)
[![Language](https://img.shields.io/badge/C%23-.NET%206-512BD4?logo=csharp)](https://dotnet.microsoft.com/)
[![UI](https://img.shields.io/badge/UI-WPF%20%7C%20XAML-007ACC)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![Graphics](https://img.shields.io/badge/Graphics-DirectX%20%7C%20HLSL-FF6F00)](https://github.com/MonoGame/MonoGame)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)

---

## Overview

**Railz** is a contemporary reimagining of the Open Rails open-source railway simulation platform. Built for rail enthusiasts and casual drivers alike, Railz strips away legacy desktop clutter in favor of a sleek, dark brutalist user experience, upgraded HLSL graphics with ACES tone-mapping, responsive mouse driving controls, and live hardware telemetry.

---

## Key Highlights & Features

### 1. Modern AMOLED Launcher (`Orts.Launcher`)
- **Native WPF Architecture**: Completely replaces the legacy WinForms frontend with an isolated modern WPF application.
- **AMOLED Brutalist Aesthetics**: Designed with Google Sans Flex typography, `#0A0A0A` dark surfaces, hairline borders, and subtle blue accent states.
- **Fixed 3-Row Layout**: Designed to fit 1080p and higher displays without awkward nested scrollbars.
- **Native Immersive Dark Titlebar**: Leverages Windows DWM API (`DwmSetWindowAttribute`) for seamless OS integration.
- **Quick Drive & Route Browser**: Fast MVVM route exploration, activity selection, consist picking, and one-click handoff into the 3D simulator.

### 2. ACES Filmic Tone Mapping & Graphics Upgrades
- **Photorealistic Color Grading**: Integrated the Academy Color Encoding System (ACES) filmic tone curve across `SceneryShader`, `SkyShader`, and `CabShader`.
- **Natural Dynamic Range**: Eliminates harsh clipping and blown-out white skies while preserving rich shadow contrast.
- **Render Engine Optimizations**: Removed redundant `RenderSurface` blits and clamped shadow map memory allocation to optimize VRAM utilization.
- **Locked 60 FPS Frame Pacing**: Smooth, consistent frame delivery with microsecond-level timing.

### 3. Modern Digital HUD & Live Telemetry
- **Minimalist Driver HUD (Page 0)**: Displays an oversized digital speedometer alongside real-time dynamic traction, train brake, dynamic brake, and signal indicators.
- **Hardware Telemetry Strip**: Translucent AMOLED telemetry pill monitoring:
  - Smoothed Framerate (FPS)
  - Frame Latency (ms)
  - Simulator CPU Utilization (%)
  - Working Set RAM Footprint (MB)
  - Detected GPU Adapter Device Name

### 4. Interactive Driving Deck & Arcade Mode
- **Draggable Touch & Mouse Controls**: Drag-and-drop throttle and train-brake bars with smooth continuous controller dispatching.
- **Direction Selector**: 1-click Forward (`F`), Neutral (`N`), and Reverse (`R`) selector with motion-safety interlocks.
- **Arcade Physics Mode**: Toggle between authentic physics simulation and an Arcade 1.5x tractive effort multiplier for accessible, casual driving without waiting minutes to spool up heavy freight.
- Press `U` in-game to toggle the driving deck for an unobstructed cinematic view.

### 5. Cinematic Loading Experience
- Refined black loading band with high-resolution route name and stage text card rasterization.
- Inset, modern progress bar replacing full-bleed legacy bars.

### 6. Automated Starter Route Setup
- Includes `Get-TestRoute.ps1`, an automated setup script that downloads, extracts, and registers the official starter route (*Demo Model 1 / Scottish Capital Express*) directly into Windows registry profiles.

---

## In-Game Controls & Keybindings

| Action | Keyboard / Mouse | Driving Deck |
|---|---|---|
| **Toggle Driving Deck** | `U` | — |
| **Toggle HUD Overlay** | `F5` *(Page 0 is Modern AMOLED)* | — |
| **Throttle** | `A` (Increase) / `D` (Decrease) | Drag **THROTTLE** slider |
| **Train Brake** | `;` (Apply) / `'` (Release) | Drag **TRAIN BRAKE** slider |
| **Reverser** | `W` (Forward) / `S` (Reverse) | Click **F**, **N**, or **R** buttons |
| **Arcade Physics** | — | Click **MODE** pill |
| **Camera Views** | `1` (Cab), `2` (Front), `3` (Rear), `4` (Trackside) | — |

---

## Getting Started

### Prerequisites
- Windows 10 / 11 (64-bit)
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- DirectX runtime support

### 1. Clone the Repository
```powershell
git clone https://github.com/ARUEXP/Railz.git
cd Railz
```

### 2. Build the Solution
Build the launcher and simulator using the .NET CLI:
```powershell
# Build the WPF Launcher
dotnet build Source/Orts.Launcher/Orts.Launcher.csproj -c Release

# Build the 3D Simulator
dotnet build Source/RunActivity/RunActivity.csproj -c Release
```
*Alternatively, open `Source/OpenRails.sln` in Visual Studio 2022 and build.*

### 3. Install Starter Content (Optional)
If you don't already have an MSTS or Open Rails content directory, run the included helper script:
```powershell
# Downloads and registers the official Demo Model 1 route into your profile
.\Get-TestRoute.ps1
```

### 4. Launch Railz
```powershell
.\Program\Orts.Launcher.exe
```
Or launch directly into explore mode on the starter route:
```powershell
.\Get-TestRoute.ps1 -Launch
```

---

## Project Architecture

```
openrails-work/
├── Source/
│   ├── Orts.Launcher/          # Modern WPF AMOLED launcher application
│   ├── RunActivity/            # 3D simulation engine, HLSL shaders, and HUD
│   │   ├── Content/            # Shaders (Scenery, Sky, Cab, Loading.fx)
│   │   └── Viewer3D/           # 3D rendering pipeline & overlay popups
│   │       └── Popups/         # ModernDrivingHUD.cs & DrivingControls.cs
│   ├── Orts.Simulation/        # Locomotive physics, train mechanics & simulation core
│   ├── Orts.Formats.Msts/      # MSTS file format parsers
│   ├── Orts.Formats.OR/        # Open Rails native format extensions
│   ├── Orts.Parsers.Msts/      # Track database, wagon, and route parsers
│   └── Orts.Common/            # Common math, geometry, and utility libraries
├── Get-TestRoute.ps1           # 1-click starter route downloader and registry installer
└── Program/                    # Output binaries and shared dependencies
```

---

## Credits & License

Railz is based on the [Open Rails](http://openrails.org/) simulator project, licensed under the **GNU General Public License v3.0 (GPL-3.0)**. See the [LICENSE](LICENSE) file for complete details.
