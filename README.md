# OfficeCore — Unified On-Premises Business & HR Management Suite

> **OfficeCore** is a modern, monolithic, self-hosted enterprise operating system that unifies human resources, biometric attendance, automated payroll, Kanban project management, real-time messaging, and field operations into a single cohesive platform.

Designed specifically for on-premises deployment, OfficeCore eliminates subscription lock-in and cloud latency by running directly on your local office network with zero external dependencies.

---

## 🌟 Key Features & Capabilities

### 🏢 Multi-Tenant Workspaces & Role-Based Access Control
- Complete organization data isolation partitioned by tenant boundaries.
- Flexible Role-Based Access Control (RBAC) supporting custom roles (`OrganizationRole`, `OrganizationRoleClaims`).
- Distinct views for **Employee Self-Service (ESS)** and **Management Self-Service (MSS)**.
- Secure token-based invitation system (by direct link or email).

### 🖥️ Native Desktop Ecosystem & 1-Click Installers
- **OfficeCore Server Station (`OfficeCore.Desktop.Server`):**
  - Dedicated server host orchestrator with live stdout/stderr console streaming.
  - Automatic LAN IP detection and network sharing URL broadcasting.
  - Built-in PostgreSQL database credential configuration & connectivity testing.
  - Optional auto-start server engine on application launch.
  - Direct local embedded Web App access via Microsoft Edge WebView2.
- **OfficeCore Workstation Terminal (`OfficeCore.Desktop.Client`):**
  - Lightweight, POS-style workstation client for employee computers.
  - Zero local database installation required; connects over local LAN.
  - Auto-connect on startup option.
- **Standalone 1-Click Installers (`Setup-OfficeCore-Client.exe` & `Setup-OfficeCore-Server.exe`):**
  - Desktop & Start Menu shortcut generation.
  - Optional automatic startup when the computer / device turns on.
  - Windows Programs & Features registration with complete uninstaller support.

### ⏱️ Hybrid Biometric Attendance & Shift Scheduling
- **Hardware Integration:** Compatible with ESP32 microcontrollers and AS608 optical fingerprint sensors.
- **Dual Connectivity:**
  - *USB Web Serial API:* Direct plug-and-play browser serial communication at 9600 baud.
  - *Wi-Fi TCP Socket:* Network scanner communication via port 9000 listener (`FingerprintDeviceService`).
- Configurable work schedules with shift enforcement, break deduction thresholds, grace periods, and overtime calculation.
- Manual attendance adjustment auditing with complete change provenance.

### 🌴 Leave Management (PTO) & Salary Advances
- Employee Self-Service leave requests with document attachment uploads (`/uploads/leaves`).
- Dedicated Manager Approval portal (`/admin/leave-approvals`).
- Salary advance and installment loan request workflows with automated payroll amortization schedules.

### 💰 Automated Payroll Engine
- Supports Hourly, Fixed Rate, and Semi-Monthly / Monthly compensation models.
- Automated batch generation of payroll periods across Draft, Computed, Finalized, and Paid states.
- Seamless deduction of active loan amortizations, statutory withholdings, and integration of approved paid leaves.

### 📋 Real-Time Collaborative Kanban Boards
- Multi-list workflow management synchronized across all connected users via SignalR WebSockets (`BoardHub`).
- Multi-person task assignments with interactive member roster picker and avatar stacks.
- Card descriptions, due dates, priority tags, and direct dashboard integration.

### 📍 Live Field Staff Operations Tracking
- Real-time GPS coordinate reporting for riders and field personnel.
- Interactive Leaflet map (`Locations.razor`) displaying live staff markers with instant selection cards.
- Real-time marker position updates broadcast via SignalR (`NotificationHub`).

### 💬 Internal Chat & Notification Center
- Real-time team messaging powered by SignalR (`ChatHub`).
- Global notification center for task assignments, leave reviews, and loan status updates.

### 🌓 Complete Dark & Light Theme Parity
- Fully customized Tailwind CSS v4 styling with dynamic theme variable switching (`.light`).
- Crisp contrast, zero-bleed opaque popovers, and tailored color palettes.

---

## 🛠️ Technology Stack

| Layer | Technology |
| :--- | :--- |
| **Backend API** | C# .NET 9 LTS Web API (C# 13) |
| **Database** | PostgreSQL 18 with `Npgsql.EntityFrameworkCore.PostgreSQL` |
| **Frontend Web** | Blazor WebAssembly (Single-port hosting served by the Web API) |
| **Real-Time Engine** | ASP.NET Core SignalR WebSockets |
| **Desktop Shell** | WPF .NET 9 with Microsoft Edge WebView2 runtime |
| **Styling** | Tailwind CSS v4 |
| **Hardware** | ESP32 + AS608 Optical Fingerprint Sensor (C++ Arduino firmware) |

---

## 📂 Repository Structure

```text
OfficeCore/
├── README.md                               # This file — master project overview & system guide
├── ROADMAP.md                              # Feature pipeline, upcoming backlog & architecture plans
├── CHANGELOG.md                            # Detailed chronological record of all changes (strict add-only)
├── agent.md                                # Master AI agent operating instructions & architecture
├── .agents/                                # Antigravity IDE workspace configuration root
│   └── rules/                              # Rules automatically loaded by AI agents
│       └── operating.md                    # Mandatory workflow and architectural directives
├── publish-installers.ps1                  # Automated release script for standalone installers
├── hardware/
│   └── ESP32_Fingerprint_Device/           # C++ Arduino firmware for biometric scanners
└── src/
    ├── OfficeCore.Api/                     # ASP.NET Core Web API & Blazor WASM host
    ├── OfficeCore.Web/                     # Blazor WebAssembly frontend application
    ├── OfficeCore.Desktop.Server/          # Server Station WPF orchestrator & host dashboard
    ├── OfficeCore.Desktop.Client/          # Workstation Terminal WPF client
    ├── OfficeCore.Client.Installer/        # Setup-OfficeCore-Client.exe installer project
    └── OfficeCore.Server.Installer/        # Setup-OfficeCore-Server.exe installer project
```

---

## 🚀 Quick Start Guide

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/) (for Tailwind CSS compilation)
- [PostgreSQL 16+](https://www.postgresql.org/) (or use the built-in Server Station to configure database credentials)

### Option 1: Run with Server Station (Recommended for Desktop)
```powershell
dotnet run --project src/OfficeCore.Desktop.Server
```
*Configure your PostgreSQL credentials in the dashboard, click **Start Server**, and click **Launch OfficeCore Locally**.*

### Option 2: Run in Development Mode (Concurrently)
```powershell
npm.cmd start
```
*Launches Tailwind CSS watcher, ASP.NET Core API (`http://localhost:5026`), and Blazor WASM.*

### Option 3: Package Standalone Installers
To build standalone 1-click installer executables (`dist/Setup-OfficeCore-Client.exe` and `dist/Setup-OfficeCore-Server.exe`):
```powershell
powershell -ExecutionPolicy Bypass -File ./publish-installers.ps1
```

For complete startup flags, default credentials, and hardware setup instructions, see [agent.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/agent.md).

---

## 📖 Documentation Index

- 🎯 [ROADMAP.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/ROADMAP.md) — Master planning, upcoming feature pipeline, and backlog.
- 📝 [CHANGELOG.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/CHANGELOG.md) — Full historical changelog (strict add-only policy).
- 🧠 [agent.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/agent.md) — System architecture, database conventions, and code standards.
- ⚙️ [operating.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/.agents/rules/operating.md) — Antigravity IDE automated operating rules.
