<p align="center">
  <img src="public/logo.png" alt="Stash Windows Logo" width="120" />
</p>

<h1 align="center">Stash for Windows</h1>

<p align="center"><strong>Hoard everything. Find anything. Now on Windows.</strong></p>

<p align="center">
  This is a Windows port of the excellent <a href="https://github.com/savewithstash/stash">Stash</a> project.
  It packages the application into a lightweight, portable Windows launcher (<code>Stash.exe</code>, ~60 KB) that runs in your system tray, manages dependencies automatically, and launches the app with a single click.
</p>

<p align="center">
  <a href="https://github.com/savewithstash/stash"><img src="https://img.shields.io/badge/original_repo-savewithstash%2Fstash-38e0d4?style=flat-square" alt="Original Repo"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="License: MIT"></a>
  <a href="https://docs.qvac.tether.io/"><img src="https://img.shields.io/badge/powered_by-QVAC-9b8cff?style=flat-square" alt="Powered by QVAC"></a>
  <img src="https://img.shields.io/badge/platform-Windows_10_%2F_11-c7f464?style=flat-square" alt="Runs on Windows 10 / 11">
</p>

---

## 🚀 Quick Start for Windows Users

You don't need Git, Docker, Node.js, or any complex terminal setup. Just download and run!

1. Go to the **[Releases](../../releases/latest)** page of this repository.
2. Download **`Stash.exe`**.
3. Move `Stash.exe` to a folder of your choice (e.g. `C:\Users\YourName\Stash`) and double-click to run it.

### What happens when you run `Stash.exe`?
- **First Run Setup:** If Node.js is not installed system-wide, the launcher automatically downloads a lightweight, portable copy of Node.js (~30MB) into `%APPDATA%\Stash\node` and extracts it. It then downloads the latest Stash app package, runs `npm install`, and starts the background server.
- **System Tray Integration:** Stash runs silently in the background. An icon will appear in your system tray. 
  - **Double-click** the tray icon to open the web dashboard in your browser (`http://localhost:5173`).
  - **Right-click** the tray icon to view server logs, toggle Windows startup launching, force-check for updates, or exit.
- **On-Device AI:** The app is immediately usable. During first-run startup, Stash downloads about 1.3 GB of default model weights locally. Once ready, the local AI classification and semantic search (Ask mode) activate. Everything stays completely on your computer.

---

## 🔄 Automatic & UI Update Notifications

This port is designed so that Windows users get updates as soon as they reach Linux or macOS users, with zero manual maintenance:

- **App Code Auto-Updates:** On every startup, the launcher checks the repository for code updates. If a new update is found, it downloads and extracts it automatically.
- **Active UI Notifications:** If you keep Stash running in the background for a long time, the web interface will display a sleek, subtle banner at the top notifying you:
  - When an app code update is available: *"A new Stash update is available. Please close and restart the app to apply updates."*
  - When a launcher binary (`Stash.exe`) update is available: *"A new Stash Windows Launcher is available. Please download Stash.exe to upgrade."*

---

## 🛠️ Compilation (For Developers)

If you'd like to build the C# launcher yourself from the source code, you can use the included batch script:

1. Double-click `build_launcher.bat` or run it from your command prompt.
2. It uses the native Windows C# compiler (`csc.exe` in the `.NET Framework v4.0`) to compile `launcher.cs` into `Stash.exe`.
3. Alternatively, the GitHub Actions workflow in this repository automatically builds the executable whenever a new release is published.

---

## 📜 Attribution & License

This project is a downstream packaging of **Stash**, created by **[supersuryaansh](https://github.com/supersuryaansh)**. 

- **Original Repository:** [savewithstash/stash](https://github.com/savewithstash/stash)
- **Original License:** MIT (Original code remains copyrighted to its respective authors).
- **Windows Packaging Wrapper:** MIT.

All local machine-learning operations are powered on-device by [QVAC SDK](https://docs.qvac.tether.io/).
