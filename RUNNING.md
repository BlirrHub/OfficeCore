# OfficeCore — Developer Setup & Run Guide

This guide walks you through everything needed to get the OfficeCore system running locally from scratch.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Project Structure](#2-project-structure)
3. [PostgreSQL Setup](#3-postgresql-setup)
4. [Configure the Connection String](#4-configure-the-connection-string)
5. [Running the API](#5-running-the-api)
6. [Running the Blazor Frontend](#6-running-the-blazor-frontend)
7. [Running Both Together](#7-running-both-together)
8. [Default Login Credentials](#8-default-login-credentials)
9. [Port Reference](#9-port-reference)
10. [Troubleshooting](#10-troubleshooting)

---

## 1. Prerequisites

Make sure the following are installed on your machine before proceeding.

| Tool | Minimum Version | Check Command |
|---|---|---|
| .NET SDK | 10.0 | `dotnet --version` |
| Node.js | 18+ | `node --version` |
| npm | 9+ | `npm --version` |
| PostgreSQL | 14+ | `psql --version` |

> You can download PostgreSQL from https://www.postgresql.org/download/. The installer also includes **pgAdmin** (a GUI tool) which makes database management easier.

---

## 2. Project Structure

```
OfficeCore/
├── OfficeCore.slnx               ← Solution file (open this in your IDE)
└── src/
    ├── OfficeCore.Api/           ← ASP.NET Core Web API (backend)
    │   ├── appsettings.json      ← ⚙️  Connection string lives here
    │   ├── Controllers/
    │   └── Data/DbSeeder.cs      ← Seeds DB with default admin on first run
    └── OfficeCore.Web/
        ├── OfficeCore.Web/       ← Blazor Server (frontend host)
        │   ├── appsettings.json  ← ⚙️  API URL lives here
        │   ├── Styles/app.css    ← Tailwind CSS source (edit this)
        │   ├── wwwroot/app.css   ← Compiled Tailwind CSS (auto-generated)
        │   └── package.json      ← npm / Tailwind scripts
        └── OfficeCore.Web.Client/ ← Blazor WASM client
```

---

## 3. PostgreSQL Setup

### Step 1 — Make sure PostgreSQL is running

Open **Services** (Windows: `services.msc`) and confirm `postgresql-x64-XX` is **Running**.
Or via PowerShell:

```powershell
Get-Service -Name postgresql*
```

### Step 2 — Find or reset your postgres password

The default connection string expects the user `postgres` with the password `postgres`.
If your installation uses a different password, you have two options:

**Option A — Update the connection string** *(recommended, see Section 4)*

**Option B — Reset the postgres password** (if you forgot it):

1. Open **SQL Shell (psql)** from the Start Menu, or use `psql -U postgres`.
2. Log in (you may be prompted for the current password).
3. Run:
   ```sql
   ALTER USER postgres WITH PASSWORD 'postgres';
   ```

### Step 3 — Create the database (optional)

The application **automatically creates and migrates the database** on startup via `DbSeeder`. You do **not** need to create it manually. However, if you prefer to do it yourself:

```sql
CREATE DATABASE "OfficeCoreDb";
```

---

## 4. Configure the Connection String

> ⚠️ **Most common reason the API fails to start.** Update this before running.

Open the API's settings file:

**`src/OfficeCore.Api/appsettings.json`**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=OfficeCoreDb;Username=postgres;Password=YOUR_PASSWORD_HERE"
  }
}
```

Replace `YOUR_PASSWORD_HERE` with your actual PostgreSQL password.

> 🔒 **Security tip**: Never commit real passwords to Git. For team environments, use **User Secrets** instead:
> ```powershell
> dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=OfficeCoreDb;Username=postgres;Password=yourpassword" --project src/OfficeCore.Api
> ```
> User Secrets are stored outside the project folder and never committed.

---

## 5. Running the API

The API must be started **first** — the Blazor frontend calls it for authentication and data.

### Step 1 — Run from the repo root

```powershell
dotnet run --project src/OfficeCore.Api
```

### Step 2 — Verify it's running

You should see output similar to:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5001
```

The API also runs the **database seeder** on first start:
- Creates the `OfficeCoreDb` database schema automatically via EF Core migrations.
- Creates the `Admin` role with all 10 module access claims.
- Creates the default admin user (`admin@company.com` / `Admin123!`).

On subsequent runs, the seeder detects that migrations and the admin account already exist and skips silently.

### Step 3 — (Optional) Explore the API docs

Open your browser to:
- **OpenAPI / Scalar**: `https://localhost:7001/openapi` *(Development mode only)*

---

## 6. Running the Blazor Frontend

You need **two terminal windows** for the frontend: one for .NET hot reload and one for Tailwind CSS compilation.

### Terminal 1 — Compile Tailwind CSS

Navigate to the Blazor server project and start the Tailwind watch process:

```powershell
cd src/OfficeCore.Web/OfficeCore.Web
npm install        # First time only — installs Tailwind
npm run css:watch  # Watches for class changes and recompiles
```

Leave this terminal running. Tailwind scans your `.razor` files and regenerates `wwwroot/app.css` automatically whenever you add new classes.

> **One-time build** (no live reload): `npm run css:build`

### Terminal 2 — Run the Blazor App

From the repository root:

```powershell
dotnet watch --project src/OfficeCore.Web/OfficeCore.Web
```

You should see:

```
Now listening on: https://localhost:7057
Now listening on: http://localhost:5057
```

Open your browser at **`https://localhost:7057`** — you'll be redirected to the login page.

---

## 7. Running Both Together

Full startup sequence in one view:

**Terminal 1 — API**
```powershell
dotnet run --project src/OfficeCore.Api
```

**Terminal 2 — CSS Watcher**
```bash
cd src/OfficeCore.Web
npm run css:watch
```

**Terminal 3 — Blazor Frontend**
```bash
cd src/OfficeCore.Web
dotnet watch
```

> **Tip**: Use Windows Terminal with multiple tabs for a clean experience.

---

## 8. Default Login Credentials

On the very first run, the seeder creates a default admin account:

| Field | Value |
|---|---|
| **Email** | `admin@company.com` |
| **Password** | `Admin123!` |
| **Role** | Admin (full access to all modules) |

> ⚠️ **Change the default admin password** immediately after your first login in any shared or production environment.

---

## 9. Port Reference

| Service | HTTPS Port | HTTP Port |
|---|---|---|
| **API** (`OfficeCore.Api`) | `7001` | `5001` |
| **Blazor Frontend** (`OfficeCore.Web`) | `7057` | `5057` |

### Changing Ports

Edit the `launchSettings.json` for each project:

- **API**: `src/OfficeCore.Api/Properties/launchSettings.json`
- **Blazor**: `src/OfficeCore.Web/OfficeCore.Web/Properties/launchSettings.json`

If you change the API port, also update:
- `src/OfficeCore.Web/OfficeCore.Web/appsettings.json` → `"ApiBaseUrl"`
- `src/OfficeCore.Api/appsettings.json` → `"BlazorOrigin"`

---

## 10. Troubleshooting

### ❌ `28P01: password authentication failed for user "postgres"`

Your PostgreSQL password doesn't match the connection string.

**Fix**: Update `src/OfficeCore.Api/appsettings.json`:
```json
"DefaultConnection": "Host=localhost;Database=OfficeCoreDb;Username=postgres;Password=YOUR_ACTUAL_PASSWORD"
```

---

### ❌ `Connection refused` on port 5432

PostgreSQL is not running.

**Fix**:
```powershell
# Check service status
Get-Service -Name postgresql*

# Start the service (adjust version number to match yours)
Start-Service -Name "postgresql-x64-17"
```

---

### ❌ `npm error code ENOENT` when running `css:watch`

Running the npm command from the wrong directory.

**Fix**: You must be inside the Blazor server project folder:
```powershell
cd src/OfficeCore.Web/OfficeCore.Web
npm run css:watch
```

---

### ❌ Blazor app loads but shows no styles

Tailwind CSS hasn't been compiled yet.

**Fix**: Run `npm run css:build` once inside `src/OfficeCore.Web/OfficeCore.Web`.

---

### ❌ Login fails with "Unable to connect"

The API is not running, or the Blazor app is pointing to the wrong URL.

**Fix**:
1. Confirm the API is running at `https://localhost:7001`.
2. Check `src/OfficeCore.Web/OfficeCore.Web/appsettings.json`:
   ```json
   "ApiBaseUrl": "https://localhost:7001"
   ```

---

### ❌ After login, browser loops back to `/login`

The auth cookie isn't being recognized.

**Fix**: Make sure both services are running on `localhost` (not `127.0.0.1`), and that `BlazorOrigin` in `src/OfficeCore.Api/appsettings.json` exactly matches the Blazor URL (including port):
```json
"BlazorOrigin": "https://localhost:7057"
```

---

### ❌ `dotnet ef` command not found

EF Core tools aren't installed globally.

**Fix**:
```powershell
dotnet tool install --global dotnet-ef
```

---

## Quick Reference

```
# Step 1 — Update DB password
#  Edit: src/OfficeCore.Api/appsettings.json → DefaultConnection

# Step 2 — Start PostgreSQL service

# Step 3 — Terminal A: Run the API
dotnet run --project src/OfficeCore.Api

# Step 4 — Terminal B: Start CSS watcher
cd src/OfficeCore.Web/OfficeCore.Web && npm run css:watch

# Step 5 — Terminal C: Run Blazor
dotnet watch --project src/OfficeCore.Web/OfficeCore.Web

# Step 6 — Open browser
#   https://localhost:7057

# Step 7 — Login with default credentials
#   Email:    admin@company.com
#   Password: Admin123!
```
