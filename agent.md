# OfficeCore — AI Agent Operating Guide & Project Standards

> **Notice:** This file establishes the architecture rules, project conventions, technical guidelines, development workflows, and mandatory operating rules for AI agents and engineers working on the **OfficeCore** codebase. All directives here are strictly enforced.

---

## 0. Mandatory Operating Directives & Workflow

1. **Ideation, Planning & Roadmap Synchronization (Always Check & Update ROADMAP.md & agent.md):**
   - **ALWAYS check [agent.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/agent.md) and [ROADMAP.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/ROADMAP.md)** before engaging in any ideation, scoping, architectural design, or feature planning.
   - **ALWAYS update [ROADMAP.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/ROADMAP.md)** whenever brainstorming, receiving user feature requests, creating backlog tasks, or planning new features. Keep priorities (P1-P4), status tags, and module pipelines current.
   - **When a feature is finished**, mark it as ✅ **Completed** in `ROADMAP.md` and log the technical details in `CHANGELOG.md`.
2. **CHANGELOG Updates (Strict Add-Only Policy):**
   - After completing any update, bug fix, refactor, or feature, **ALWAYS update [CHANGELOG.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/CHANGELOG.md)** under `## [Unreleased]`.
   - **DO NOT REMOVE OR DELETE ANYTHING:** You must never remove, truncate, or overwrite existing entries, milestones, or history from `CHANGELOG.md`. Only append new entries or update recent entries with additional details.
3. **Occasional & Timely Git Commits:**
   - Commit changes to Git frequently and whenever completing a feature, bug fix, or milestone.
   - Do not leave modifications uncommitted across tasks.
   - Use clean, conventional commit messages (`feat:`, `fix:`, `refactor:`, `chore:`).
4. **Multi-Tenancy & Tenant Scoping:**
   - Every workspace-scoped entity must implement `ITenantEntity` and include `OrganizationId`. All queries and mutations in controllers must be scoped to the active tenant.
5. **Theme Parity:**
   - All pages and components must support both **Dark Mode** and **Light Mode** seamlessly with no contrast or visibility flaws.
6. **Tailwind CSS v4:**
   - Whenever adding or altering Tailwind classes, run `npm.cmd run css:build` in `src/OfficeCore.Web` so they are compiled to `wwwroot/app.css`.
7. **Preserve Existing Functionality:**
   - Do not delete working code, existing entity configurations, or docstrings unless explicitly instructed.

---

## 1. Project Overview & Philosophy

**OfficeCore** is a modern, monolithic enterprise platform integrating human resource management, multi-tenant workspace isolation, automated biometric attendance, time-tracked payroll, Kanban project management, real-time messaging, and client invoicing.

### Core Philosophy
- **On-Premises & Local Office First:** Built for private organizational use with a Point-of-Sale (POS)-style Desktop Hub. A primary computer acts as the Server Station (hosting PostgreSQL and the API), while other office workstations connect seamlessly as Client Terminals.
- **Strict Tenant Isolation:** Data across workspaces must never bleed. Every multi-tenant entity and query is strictly scoped to the active organization.
- **Role-Based Granular Access:** Distinct separation between Employee Self-Service (ESS) and Management Self-Service (MSS) via dynamic claims transformation.
- **Aesthetic Excellence:** Modern, ultra-premium interface with glassmorphism, responsive navigation drawers, and seamless zero-flicker Dark and Light mode themes.

---

## 2. Technology Stack & Key Dependencies

| Layer | Technology | Details |
|---|---|---|
| **Backend API** | ASP.NET Core 9 Web API | Target framework `net9.0`, C# 13, unified single-port host on port `5026` |
| **Database & ORM** | PostgreSQL 18 + EF Core 9 | `Npgsql.EntityFrameworkCore.PostgreSQL`, database `OfficeCoreDb` on port `5432` |
| **Identity & Auth** | ASP.NET Core Identity | Cookie-based authentication with custom claims transformation |
| **Frontend Web** | Blazor WebAssembly | Client-side WASM hosted and served directly by ASP.NET Core API |
| **Desktop Client** | WPF + Edge WebView2 | `OfficeCore.Desktop.Server` (host station) + `OfficeCore.Desktop.Client` (workstation terminal) |
| **Styling** | Tailwind CSS v4 | `@tailwindcss/cli` v4.3.1, custom CSS variable token design system |
| **Real-Time** | ASP.NET Core SignalR | `ChatHub`, `AttendanceHub`, `BoardHub`, `NotificationHub` |
| **Hardware** | Web Serial API + ESP32 | Biometric fingerprint scanner over USB Serial (browser) or TCP (Wi-Fi) |

---

## 3. Repository Map & Directory Layout

```
OfficeCore/
├── OfficeCore.slnx                         # Solution file (.NET 9)
├── README.md                               # Master project overview & system documentation
├── package.json                            # Root NPM scripts (concurrently runner)
├── CHANGELOG.md                            # Comprehensive version and change history (Strict Add-Only)
├── ROADMAP.md                              # Planning, feature pipeline, upcoming backlog & ideas (Check & Update on Ideation)
├── agent.md                                # This document (AI agent master architecture & operating guide)
├── .agents/                                # Antigravity IDE workspace configuration root
│   └── rules/                              # Workspace rules auto-loaded by AI agents
│       └── operating.md                    # Core operational directives & rules of engagement
├── publish-installers.ps1                  # Packaging script for Server & Client installers
├── hardware/
│   └── ESP32_Fingerprint_Device/           # C++ Arduino firmware for biometric scanners
└── src/
    ├── OfficeCore.Api/                     # ASP.NET Core Web API & Blazor WASM host
    │   ├── Controllers/                    # REST API endpoints (Auth, Attendance, Boards, Payroll, etc.)
    │   ├── Data/                           # DbSeeder.cs (Demo account & organization seeder)
    │   ├── Models/                         # Domain entities & AppDbContext
    │   ├── Migrations/                     # EF Core PostgreSQL migration history
    │   ├── Services/                       # TenantService, ClaimsTransformation, FingerprintService
    │   ├── Hubs/                           # SignalR Hubs (ChatHub, BoardHub, AttendanceHub)
    │   └── Uploads/                        # File storage (profile avatars, org logos)
    ├── OfficeCore.Web/                     # Blazor WebAssembly frontend
    │   ├── Layout/                         # MainLayout.razor, NavMenu.razor, EmptyLayout.razor
    │   ├── Pages/                          # Blazor feature pages (Settings, Dashboard, Attendance, etc.)
    │   ├── Shared/                         # Reusable UI components (ThemeToggle, Toast, Modals)
    │   ├── Services/                       # Client state providers, ToastService, ApiService
    │   ├── Styles/app.css                  # Tailwind CSS v4 source (EDIT THIS FILE)
    │   ├── wwwroot/                        # Static assets, js interop, compiled app.css
    │   │   ├── app.css                     # Compiled CSS (DO NOT EDIT DIRECTLY)
    │   │   └── js/themeInterop.js          # JavaScript theme helper (light/dark mode toggle)
    │   └── package.json                    # Tailwind build scripts (css:build, css:watch)
    ├── OfficeCore.Desktop.Server/          # WPF Server Station — orchestrates API process, LAN IP display, live logs
    │   ├── MainWindow.xaml/cs              # PostgreSQL config UI + API process management
    │   ├── ServerProcessManager.cs         # API lifecycle with stdout/stderr log streaming
    │   ├── ServerConfigService.cs          # Local config persistence
    │   └── NetworkHelper.cs               # LAN IP auto-detection
    ├── OfficeCore.Desktop.Client/          # WPF Client Terminal — lightweight workstation WebView2 shell
    │   ├── MainWindow.xaml/cs              # Server URL input + WebView2 browser host
    │   └── ClientConfigService.cs          # Server address persistence
    ├── OfficeCore.Client.Installer/        # WPF setup wizard for deploying the Client Terminal (Setup-OfficeCore-Client.exe)
    └── OfficeCore.Server.Installer/        # WPF setup wizard for deploying the Server Station (Setup-OfficeCore-Server.exe)
```

---

## 4. Multi-Tenancy Architecture & Scoping Rules

Multi-tenancy is the architectural backbone of OfficeCore. All business data is partitioned by `OrganizationId`.

### Rules for Multi-Tenant Entities:
1. **Implement `ITenantEntity`:** Every workspace-scoped entity must implement the `ITenantEntity` interface:
   ```csharp
   public class WorkSchedule : ITenantEntity
   {
       public Guid Id { get; set; }
       public Guid OrganizationId { get; set; }
       public Organization Organization { get; set; } = null!;
       // ...
   }
   ```
2. **Identity Separation (`ApplicationUser` vs `Employee`):**
   - `ApplicationUser`: Represents the **global person** across the entire system (email, password hash, two-factor settings, global profile picture).
   - `Employee`: Represents the **workspace-specific membership** (job title, daily rate, work schedule, employment status, organization roles).
   - **Never mix IDs:** When referencing records in boards, chat, attendance, or payroll, always link to `Employee.Id`, **not** `ApplicationUser.Id`.
3. **Use `TenantService` in Controllers:**
   - Inject `ITenantService` into controllers to resolve the active organization and current employee:
   ```csharp
   var orgId = _tenantService.GetActiveOrganizationId();
   if (orgId == null) return Unauthorized("No active workspace selected.");
   ```
4. **Mandatory Query Filtering:**
   - Every database query for tenant data **must** filter by `OrganizationId`:
   ```csharp
   var records = await _context.WorkSchedules
       .Where(w => w.OrganizationId == orgId.Value)
       .ToListAsync();
   ```
   - **Never** query or mutate tenant entities without matching `OrganizationId`.

---

## 5. Role-Based Access Control (RBAC) & Claims Matrix

OfficeCore uses a dynamic, tenant-scoped claims system:

### 1. Dynamic Claims Transformation (`OrganizationClaimsTransformation`)
- When a request is authenticated, `OrganizationClaimsTransformation` inspects the active organization cookie (`ActiveOrganizationId`).
- It looks up the user's `Employee` record and assigned `EmployeeRole` entries for that specific organization.
- It dynamically injects `Claim("ModuleAccess", claimValue)` and `Claim(ClaimTypes.Role, roleName)` into the `ClaimsPrincipal`.

### 2. Default Modules vs. Configurable Role Modules
OfficeCore enforces a clear distinction between **Default Modules** (available unconditionally to every authenticated employee in a workspace, housed exclusively under **Main**) and **Configurable Modules** (customizable per role in Role Management under **Finance** and **Administration**):

#### Default Modules (All under "Main" — Always Active for All Workspace Accounts)
These modules are never toggled or revoked in roles; they are automatically granted via `OrganizationClaimsTransformation` and `CookieAuthStateProvider` and require only standard `[Authorize]`:
1. **Dashboard** (`/dashboard`): Workspace summary, personal metrics, team headcount, and activity feeds.
2. **My Attendance** (`/my-attendance`): Personal workstation time clock, punch in/out, shift details, and punch history.
3. **Boards** (`/boards`): Collaborative Kanban boards, task assignments, checklists, and project tracking.
4. **My Leaves** (`/myleaves`): Personal sick and vacation leave filing and approval status.
5. **Advances & Loans** (`/advances`): Personal cash advances and loan requests.

#### Configurable Module Categories (Gated by Policies & Dynamic Role Claims in Role Management)
- **Finance:**
  - `Ledger` (`RequireLedger`): Dynamic allowances and custom employee deductions.
  - `Payroll` (`RequirePayroll`): Batch computation, payroll approval, and payslip distribution.
  - `Invoices` (`RequireInvoices`): Billing, client invoices, and payment tracking.
- **Administration:**
  - `Clients` (`RequireClients` — moved under Administration): Customer CRM directory.
  - `Attendance` (`RequireAttendance` — moved under Administration): Company-wide biometric logs, manual punch adjustments, and device integration.
  - `Users` (`RequireUsers`): Workspace staff directory and invitations.
  - `Roles` (`RequireRoles`): Custom role creation and permission matrices.
  - `LeaveApprovals` (`RequireLeaveApprovals`): Management review and approval of leave requests.
  - `WorkSchedules` (`RequireWorkSchedules`): Shifts, break policies, and overtime configuration.
  - `TimeOff` (`RequireTimeOff`): Workspace holiday calendars and official closures.
  - `Locations` (`RequireLocations`): Physical office locations and geolocation fences.
  - `Organization` (`RequireOrganization`): Workspace profile and settings.
  - `Backups` (`RequireBackups`): Automated and manual PostgreSQL database backups.
  - `AuditLogs` (`RequireAuditLogs`): Security and compliance audit trail.

### 3. ESS vs MSS Architectural Pattern
- **Employee Self-Service (ESS):** Standard employees must always have access to view and manage their own records without needing management module permissions:
  - `GET /api/attendance/my` & `POST /api/attendance/punch` (`[Authorize]`)
  - `GET /api/leaverequest/my` & `POST /api/leaverequest` (`[Authorize]`)
  - `GET /api/advancerequest/my` & `POST /api/advancerequest` (`[Authorize]`)
  - Personal profile and password changes (`[Authorize]`)
- **Management Self-Service (MSS):** Company-wide management and modification endpoints are strictly gated:
  - `GET /api/attendance` & `POST /api/attendance/admin-create` (`[Authorize(Policy = "RequireAttendance")]`)
  - `GET /api/leaverequest` (`[Authorize(Policy = "RequireLeaveApprovals")]`)
  - `POST /api/payroll/generate` (`[Authorize(Policy = "RequirePayroll")]`)
  - `GET /api/client` (`[Authorize(Policy = "RequireClients")]`)

---

## 6. Frontend Guidelines & Blazor WASM Standards

### Component Structure & Lifecycle
Follow this standard order in `.razor` files:
1. **Directives:** `@page "/route"`, `@attribute [Authorize]`, layout specification.
2. **Injections:** `@inject HttpClient Http`, `@inject NavigationManager Navigation`, `@inject ToastService ToastService`, `@inject IJSRuntime JS`.
3. **Template HTML:** Clean semantic markup styled with Tailwind v4 classes.
4. **Code Block:** `@code { ... }` with explicit state variables, lifecycle methods (`OnInitializedAsync`), and event handlers.
5. **State Updates:** Call `StateHasChanged()` after asynchronous API calls or state mutations when outside standard event triggers.

### Layouts
- **`MainLayout.razor`:** Authenticated shell containing the sidebar (`NavMenu.razor`), mobile slide-out drawer, top bar with `ThemeToggle.razor`, and active workspace switcher.
- **`EmptyLayout.razor`:** Minimal centered layout for unauthenticated flows: `/login`, `/signup`, `/join/{token}`.

### Modals & Toast Feedback
- **Toasts:** Inject `ToastService` and call `ToastService.ShowSuccess("Message")` or `ToastService.ShowError("Message")`. Do not use native `alert()`.
- **Confirmation Modals:** Destructive actions (deleting a card, archiving payroll, deleting an organization) must always require confirmation through a modal dialog before invoking API endpoints.

### SignalR Hub Management
- Always check `HubConnection.State == HubConnectionState.Connected` before sending messages.
- Implement `IAsyncDisposable` in Blazor components that subscribe to SignalR events to prevent memory leaks and zombie listeners.

---

## 7. Theming & Styling Rules (Tailwind CSS v4)

OfficeCore features a dual-theme system (Dark Mode by default, Light Mode via `.light` class on `<html>`).

### Critical Styling Rules:
1. **Source of Truth:** **ALWAYS** edit `src/OfficeCore.Web/Styles/app.css`.
   - **NEVER** edit `src/OfficeCore.Web/wwwroot/app.css` directly; it is compiled and overwritten.
2. **Rebuilding CSS:**
   - To compile once: `npm.cmd run css:build` in `src/OfficeCore.Web`.
   - To watch changes: `npm.cmd run css:watch` in `src/OfficeCore.Web`.
3. **Tailwind v4 Variable Tokens:**
   - In Tailwind v4, colors map through CSS variables defined in `@layer theme`.
   - Light mode overrides these variables under `html.light` in `Styles/app.css`:
     - `--color-zinc-950` maps to `#ffffff` (light background)
     - `--color-zinc-900` maps to `#f4f4f5` (light card background)
     - `--color-zinc-800` maps to `#e4e4e7` (light borders)
     - Text colors invert automatically (`text-white` -> `text-zinc-950`).
4. **Preserving Button Contrast (`.keep-white`):**
   - High-contrast buttons (`bg-fuchsia-600`, `bg-red-600`, gradients) must keep their white text in light mode. Use the `.keep-white` CSS class or explicit inline protection.
5. **Visual Styling Tokens:**
   - Card backgrounds: `bg-zinc-900/60 border border-zinc-800 backdrop-blur-md rounded-2xl`
   - Primary accents: Fuchsia (`bg-fuchsia-600 hover:bg-fuchsia-500 text-white`)
   - Inputs: `bg-zinc-950/60 border border-zinc-800 rounded-xl px-4 py-2.5 text-white focus:border-fuchsia-500`

---

## 8. Database & PostgreSQL Query Conventions

The database engine is **PostgreSQL 18** via `Npgsql.EntityFrameworkCore.PostgreSQL`.

### PostgreSQL Guidelines:
1. **Date/Time Handling:**
   - PostgreSQL `timestamp with time zone` (or `timestamp without time zone`) requires UTC `DateTime` values (`DateTime.UtcNow`).
   - When writing date comparisons, ensure timestamps are properly mapped to UTC to avoid Npgsql `Cannot write DateTime with Kind=Unspecified` errors.
2. **High-Precision Decimals:**
   - PostgreSQL provides first-class arbitrary-precision arithmetic via `numeric(precision, scale)`.
   - All financial rates, tax brackets, payroll deductions, and hourly values are explicitly configured in `AppDbContext.cs`:
     - Standard currency fields: `.HasColumnType("numeric(18,2)")`
     - Precision rate / multiplier fields: `.HasColumnType("numeric(18,4)")`
3. **Database Seeding (`DbSeeder.cs`):**
   - Automatically migrates and seeds the database on API startup via `await context.Database.MigrateAsync()`.
   - Default seeded credentials:
     - **Email:** `demo@officecore.com`
     - **Password:** `Password123!`
     - **Workspace:** "Demo Corp" with preconfigured Admin and Employee roles, work schedules, and a sample Kanban board.

---

## 9. Environment & Windows CLI Commands

On this machine (Windows PowerShell), Node.js tools require `.cmd` extensions to avoid PowerShell execution policy blocks:

| Action | Command | Working Directory |
|---|---|---|
| **Build Tailwind CSS** | `npm.cmd run css:build` | `src/OfficeCore.Web` |
| **Watch Tailwind CSS** | `npm.cmd run css:watch` | `src/OfficeCore.Web` |
| **Run API Backend** | `dotnet run` | `src/OfficeCore.Api` |
| **Run Web Client** | `dotnet run` | `src/OfficeCore.Web` |
| **Watch Web Client** | `dotnet watch` | `src/OfficeCore.Web` |
| **Run All Services** | `npm.cmd start` | Repository Root |
| **Add EF Migration** | `dotnet ef migrations add <Name> --project src/OfficeCore.Api` | Repository Root |
| **Update Database** | `dotnet ef database update --project src/OfficeCore.Api` | Repository Root |

### Local Port Bindings
- **API Backend:** `http://localhost:5026` / `https://localhost:7174` (Bound to `0.0.0.0` for LAN access)
- **Web Frontend:** `http://localhost:5261` (Bound to `0.0.0.0` for LAN access)

---

## 10. Agent Rules of Engagement & Code Hygiene

When performing tasks on OfficeCore, all AI agents must follow these non-negotiable rules:

1. **MANDATORY: Check agent.md & ROADMAP.md on Every Ideation or Planning Session:**
   - Before designing, scoping, or planning features, always review `agent.md` for architecture rules and `ROADMAP.md` for existing pipeline status.
   - Update `ROADMAP.md` whenever new ideas or features are proposed, keeping priority tiers and statuses organized.
2. **Never Break Multi-Tenancy:**
   - Any new entity must have `OrganizationId` and implement `ITenantEntity`.
   - Never write an API endpoint that updates, retrieves, or deletes records without scoping by `OrganizationId`.
3. **Never Break Theme Parity:**
   - Whenever you create or modify a page, modal, or component, verify that it renders legibly and beautifully in **both** Dark Mode and Light Mode.
4. **Always Rebuild Tailwind CSS:**
   - If you introduce new Tailwind utility classes, run `npm.cmd run css:build` in `src/OfficeCore.Web` so the classes are compiled into `wwwroot/app.css`.
5. **Preserve Code Comments & Architecture:**
   - Maintain existing docstrings, entity configurations, and comments. Never delete working functionality unless explicitly requested by the user.
6. **MANDATORY: Update CHANGELOG.md on Every Task & Session:**
   - At the completion of every task or feature update, immediately update `CHANGELOG.md` under `## [Unreleased]`.
   - **STRICT ADD-ONLY POLICY:** When updating `CHANGELOG.md`, **ONLY ADD NEW ENTRIES OR EXPAND RECENT ENTRIES**.
   - **NEVER** delete, truncate, remove, or overwrite existing changelog entries, previous dates, or legacy project milestones. Every piece of historical documentation must be preserved permanently.
7. **Mark Completed Items in ROADMAP.md:**
   - Whenever a task or roadmap item is finished, update its checkbox and status badge in `ROADMAP.md` to ✅ **Completed**.
8. **Occasional Git Commits:**
   - Commit working changes to Git frequently and whenever completing a feature, bug fix, or milestone.
   - Do not leave large sets of uncommitted modifications accumulated over multiple chats.
   - Use clean, conventional commit messages (`feat:`, `fix:`, `refactor:`, `chore:`) that accurately describe the changes made.

