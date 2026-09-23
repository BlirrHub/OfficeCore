# OfficeCore — Developer Setup & Run Guide

This guide walks you through everything needed to get the OfficeCore system running locally from scratch.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Project Architecture & Structure](#2-project-architecture--structure)
3. [Database Configuration (PostgreSQL 18)](#3-database-configuration-postgresql-18)
4. [Running the Application](#4-running-the-application)
   - [Option A: Desktop Hub (Recommended for On-Premises & POS Deployment)](#option-a-desktop-hub-recommended-for-on-premises--pos-deployment)
   - [Option B: Development Mode (CLI / Concurrently)](#option-b-development-mode-cli--concurrently)
   - [Option C: Start Services Individually](#option-c-start-services-individually)
5. [Default Login Credentials](#5-default-login-credentials)
6. [Desktop Client: Server & Client Terminal Modes](#6-desktop-client-server--client-terminal-modes)
7. [Hardware Integrations (ESP32 Biometrics)](#7-hardware-integrations-esp32-biometrics)
8. [Port Reference](#8-port-reference)
9. [Troubleshooting & FAQ](#9-troubleshooting--faq)

---

## 1. Prerequisites

Make sure the following are installed on your machine before proceeding:

| Tool | Minimum Version | Check Command | Purpose |
|---|---|---|---|
| **.NET SDK** | `9.0 LTS` | `dotnet --version` | Backend API & Blazor WebAssembly compilation |
| **PostgreSQL** | `17+ / 18` | `Get-Service postgresql*` or `psql --version` | Enterprise relational database storage |
| **Node.js** | `18+` | `node -v` | Root runner and Tailwind CSS CLI v4 |
| **npm** | `9+` | `npm -v` | Dependency scripts and build runners |

> 🐘 **Database Server:** OfficeCore uses **PostgreSQL 18** (Windows service `postgresql-x64-18` running on default port `5432`). Default database: `OfficeCoreDb`.

---

## 2. Project Architecture & Structure

```
OfficeCore/
├── OfficeCore.slnx                         # Solution file (.NET 9)
├── package.json                            # Root runner scripts (concurrently)
├── PROJECT_STATUS.md                       # High-level architecture & feature status
├── RUNNING.md                              # This document
├── agent.md                                # AI agent engineering guidelines & standards
├── CHANGELOG.md                            # Complete version history
├── hardware/
│   └── ESP32_Fingerprint_Device/           # Arduino C++ firmware for optical biometric scanner
└── src/
    ├── OfficeCore.Api/                     # ASP.NET Core 9 Web API
    │   ├── Controllers/                    # REST endpoints (Auth, Boards, Payroll, Leaves, etc.)
    │   ├── Data/DbSeeder.cs                # Automatic migration & demo account seeder
    │   ├── Hubs/                           # SignalR hubs (BoardHub, ChatHub, AttendanceHub)
    │   ├── Services/                       # TenantService, ClaimsTransformation, FingerprintService
    │   ├── Uploads/                        # File storage (avatars, logos, leave proof, board attachments)
    ├── OfficeCore.Api/                     # ASP.NET Core 9 Web API & Blazor WASM Host
    │   ├── Controllers/                    # REST endpoints (Auth, Boards, Payroll, Leaves, etc.)
    │   ├── Data/DbSeeder.cs                # Automatic migration & demo account seeder
    │   ├── Hubs/                           # SignalR hubs (BoardHub, ChatHub, AttendanceHub)
    │   ├── Models/AppDbContext.cs          # EF Core DbContext with PostgreSQL precision
    │   ├── Services/                       # TenantService, ClaimsTransformation, FingerprintService
    │   └── Uploads/                        # File storage (avatars, logos, leave proof, board attachments)
    ├── OfficeCore.Web/                     # Blazor WebAssembly client
    │   ├── Pages/                          # Feature pages (Dashboard, Boards, Payroll, Settings, etc.)
    │   ├── Styles/app.css                  # Tailwind CSS v4 design system source (EDIT THIS FILE)
    │   ├── wwwroot/                        # Static assets, compiled app.css, and JS interop
    │   └── package.json                    # Tailwind build scripts (css:build, css:watch)
    └── OfficeCore.DesktopClient/           # Windows Desktop Client (WPF + Edge WebView2)
        ├── MainWindow.xaml                 # Startup mode selection, Server dashboard, and Client view
        ├── MainWindow.xaml.cs              # View routing, server process orchestration, and WebView2
        ├── ServerProcessManager.cs         # API lifecycle manager with live log stream
        ├── NetworkHelper.cs                # LAN IP auto-detection
        └── DesktopConfigService.cs         # Local config persistence (desktop_config.json)
```

---

## 3. Database Configuration (PostgreSQL 18)

OfficeCore uses PostgreSQL as its primary database.

### Default Connection String
Configured in `src/OfficeCore.Api/appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=OfficeCoreDb;Username=postgres;Password=password;Include Error Detail=true"
}
```

### Auto-Migration & Seeding
1. **Auto-Migration:** On API startup, `DbSeeder.SeedAsync()` automatically applies all pending EF Core migrations to PostgreSQL. If the database `OfficeCoreDb` does not exist yet, running `dotnet ef database update --project src/OfficeCore.Api` creates it.
2. **Auto-Seeding:** If the database is blank, it automatically seeds:
   - Default Global Admin Account: `demo@officecore.com` / `Password123!`
   - Default Workspace Organization: **Demo Corp**
   - Standard Organization Roles (`Admin`, `Employee`) and module permission claims
   - Default Work Schedule with break rules
   - Default sample Kanban Board with lists and cards

### PostgreSQL Windows Service Check
To ensure PostgreSQL is running:
```powershell
Get-Service postgresql*
# To start if stopped:
Start-Service postgresql-x64-18
```

---

## 4. Running the Application

### Option A: Dedicated Desktop Applications (Server Station & Workstation Client)

#### 1. Server Station (Host Computer)
Dedicated server host orchestrator with live logs, LAN IP auto-detection, PostgreSQL configuration, and local app access:
```powershell
dotnet run --project src/OfficeCore.Desktop.Server
```
* Or run portable executable: `dist/OfficeCore-ServerStation-Portable/OfficeCore.ServerStation.exe`

#### 2. Client Terminal (Employee Workstations)
Lightweight workstation terminal with zero local database requirements:
```powershell
dotnet run --project src/OfficeCore.Desktop.Client
```
* **Installer for Other PCs:** `dist/Setup-OfficeCore-Client.exe` (1-click installer that sets up Desktop & Start Menu shortcuts).
* **Portable Executable:** `dist/OfficeCore-Client-Portable/OfficeCore.Client.exe` (runs directly without installation).

---

### Option B: Development Mode (CLI / Concurrently)

From the **repository root**, run:

```powershell
npm.cmd start
```

> Or use the VS Code task: Press `Ctrl+Shift+B` and select **"Start All Services"**.

This launches:
1. **Tailwind CSS Watcher** (auto-compiles `Styles/app.css` to `wwwroot/app.css` on changes)
2. **ASP.NET Core Web API** (`http://localhost:5026` / `https://localhost:7174`)
3. **Blazor WebAssembly Frontend** (`http://localhost:5261`)

---

### Option C: Start Services Individually

If you prefer dedicated terminal windows:

#### Terminal 1 — Tailwind CSS Compilation
```powershell
cd src/OfficeCore.Web
npm.cmd run css:watch
```

#### Terminal 2 — Web API Backend (Unified Host on 5026)
```powershell
dotnet run --project src/OfficeCore.Api
```
> Browsing to `http://localhost:5026` serves both the API and the Blazor client!

#### Terminal 3 — Blazor Frontend (Optional standalone dev server)
```powershell
dotnet run --project src/OfficeCore.Web
```

---

## 5. Default Login Credentials

Use the pre-seeded credentials to log in:

| Field | Value |
|---|---|
| **Server Station / Unified URL** | `http://localhost:5026` |
| **Standalone Dev Server URL** | `http://localhost:5261` |
| **Email** | `demo@officecore.com` |
| **Password** | `Password123!` |
| **Workspace** | **Demo Corp** (auto-selected upon login) |

---

## 6. Desktop Client: Server & Client Terminal Modes

`OfficeCore.DesktopClient` provides a streamlined Point-of-Sale (POS) experience tailored for local network environments:

### 1. Server Station (Host Machine)
- **Port Testing:** Automatically pings PostgreSQL on port `5432` to confirm readiness before launching the backend.
- **Process Management:** Launches and manages `OfficeCore.Api` as a child process.
- **LAN IP Display:** Discovers your non-virtual IPv4 LAN address and provides a ready-to-share URL (e.g. `http://192.168.1.50:5026`) with a 1-click **Copy** button for terminal workstations.
- **Live Logs:** Real-time stdout/stderr log console embedded in the dashboard for monitoring requests, migrations, and errors.
- **Embedded Web View:** Click **Launch OfficeCore Locally** to switch directly into the application via Microsoft Edge WebView2.

### 2. Client Terminal (Workstation PCs)
- Requires only the Server URL or LAN IP address.
- Validates network connection to the server before loading.
- Stores server address in `desktop_config.json` for instant subsequent connections.
- Renders the full OfficeCore application with full offline LAN speed and zero local database setup.

---

## 7. Hardware Integrations (ESP32 Biometrics)

OfficeCore features a hybrid device architecture for the **ESP32 + Optical Fingerprint Scanner (AS608)**:

1. **Wired Mode (Web Serial API):**
   - Connect the ESP32 to your PC via USB.
   - Navigate to `/attendance` → **Settings** tab.
   - Click **Connect USB Scanner**. Select the COM port at `9600` baud.
   - You can enroll fingerprints or clock in/out directly through the browser!
2. **Wi-Fi Mode (TCP Socket):**
   - Configure your Wi-Fi credentials in `hardware/ESP32_Fingerprint_Device/ESP32_Fingerprint_Device.ino`.
   - The device connects directly to the API's TCP listener on port `9000` via [FingerprintDeviceService.cs](file:///d:/Apps/Antigravity/Projects/OfficeCore/src/OfficeCore.Api/Services/FingerprintDeviceService.cs).

---

## 8. Port Reference

| Service | Protocol / Port | Purpose |
|---|---|---|
| **PostgreSQL Database** | `localhost:5432` | Relational database storage |
| **Unified Web & API** | `http://localhost:5026` | Unified REST API, SignalR Hubs, static uploads & Blazor WASM files |
| **Blazor Standalone Dev**| `http://localhost:5261` | Optional standalone Blazor WebAssembly dev server |
| **Biometric TCP Socket** | `TCP 0.0.0.0:9000` | Wi-Fi ESP32 fingerprint scanner connection |

---

## 9. Troubleshooting & FAQ

### ❌ `Npgsql.NpgsqlException: Failed to connect to localhost:5432`
PostgreSQL Windows service is not running.
**Fix:**
```powershell
Start-Service postgresql-x64-18
```

### ❌ `28P01: password authentication failed for user "postgres"`
The password in `appsettings.json` does not match your PostgreSQL installation.
**Fix:** Update `Password=your_password` in `src/OfficeCore.Api/appsettings.json` (or configure via the Desktop Client Server settings).

### ❌ `npm : File ... cannot be loaded because running scripts is disabled`
PowerShell execution policy blocks `.ps1` wrappers for Node.js tools.
**Fix:** Always use the `.cmd` extension on Windows PowerShell:
- Use `npm.cmd start` instead of `npm start`
- Use `npm.cmd run css:build` instead of `npm run css:build`

### ❌ Blazor styles look unstyled or broken
Tailwind CSS has not been compiled yet.
**Fix:**
```powershell
cd src/OfficeCore.Web
npm.cmd run css:build
```

### ❌ Client Terminal cannot connect to Server Station URL
Windows Firewall may block incoming connections on port `5026`.
**Fix:** On the Server Station PC, allow inbound TCP port `5026` through Windows Defender Firewall:
```powershell
New-NetFirewallRule -DisplayName "OfficeCore Server" -Direction Inbound -LocalPort 5026 -Protocol TCP -Action Allow
```
