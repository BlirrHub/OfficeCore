# Changelog

All notable changes to this project will be documented in this file.

---

## [Unreleased]

### Added — [2026-09-26] (Master Planning & Feature Roadmap Pipeline)

- **Dedicated Planning & Roadmap System (`ROADMAP.md`, `agent.md`):**
  - Created `ROADMAP.md` as the centralized workspace tracking document for upcoming features, implementation plans, architectural improvements, and brainstormed ideas.
  - Organized by prioritization (P1/P2/P3), module pipelines (Desktop, Kanban, Attendance/Biometrics, Leaves, Payroll, Chat, Multi-Tenancy), and an instant-idea scratchpad.
  - Added **Database Backup & Disaster Recovery System** as immediate focus (P1) covering manual/automated PostgreSQL dumps, retention policies, and safety-verified restore wizards.
  - Added **Software Licensing & Product Key System** as a documented future concept (hardware fingerprinting, offline Ed25519 signature verification, seat tiers) explicitly marked for later implementation.
  - Linked `ROADMAP.md` within `agent.md` documentation hierarchy.
- **Master System Documentation (`README.md`, `agent.md`):**
  - Created root `README.md` providing a comprehensive system overview, module breakdown, architectural stack, quickstart guide, and cross-references.
  - Linked `README.md` within `agent.md` file hierarchy.


### Added & Updated — [2026-09-26] (Standalone Installers Suite & Device AutoStartup on Boot)

- **Device AutoStartup Option in Workstation Installer (`OfficeCore.Client.Installer`):**
  - Added "Start OfficeCore Workstation automatically when device turns on" (`AutoStartupCheckBox`) in `MainWindow.xaml`.
  - Configures the Windows user startup registry (`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`) with the installed executable path on installation.
  - Enhanced binary discovery with fallback scanning across debug, release, and portable distribution directories.
  - Integrated uninstaller cleanup (`App.xaml.cs`) to remove the auto-startup registry value upon removal.

- **Dedicated Server Host Installer (`OfficeCore.Server.Installer`):**
  - Introduced new dedicated setup wizard project `OfficeCore.Server.Installer` (`Setup-OfficeCore-Server.exe`) in `OfficeCore.slnx`.
  - Features Server Station purple branding (`#C026D3`), custom installation path (`%LocalAppData%\Programs\OfficeCore Server`), and Desktop & Start Menu shortcut generation.
  - Includes "Start OfficeCore Server Station automatically when device turns on" option, allowing host machines to automatically launch Server Station on boot and combine with backend auto-start for zero-touch server orchestration.
  - Includes uninstaller integration in Windows Programs & Features with complete registry and shortcut cleanup.

- **Automated Packaging Script & Documentation (`publish-installers.ps1`, `RUNNING.md`, `agent.md`):**
  - Added `publish-installers.ps1` script to automate publishing both portable versions (`OfficeCore-Client-Portable`, `OfficeCore-ServerStation-Portable`) and standalone setup installers (`Setup-OfficeCore-Client.exe`, `Setup-OfficeCore-Server.exe`) to `dist/`.
  - Configured single-file compilation with embedded payloads and tracked the two standalone installers in `dist/` for direct deployment from the production branch.
  - Updated developer documentation in `RUNNING.md` and `agent.md`.


### Added & Fixed — [2026-09-26] (OfficeCore Server Station Desktop App: Navigation Fallthrough Fix, Auto-Start Setting & Manual Launch Prompt)

- **Fixed "Server Not Running" Dialog Fallthrough & Connection Error (`MainWindow.xaml.cs`):**
  - Resolved an issue in `LaunchLocalApp_Click` where selecting **"No"** on the "Server Not Running. Would you like to start it now?" prompt still proceeded to load `http://localhost:5026` in WebView2, causing an ungraceful `ERR_CONNECTION_REFUSED` screen and error popup.
  - Selecting "No" now returns cleanly and leaves the user on the Server Station host dashboard.
  - Selecting "Yes" flags `_pendingLaunchWhenLive = true` and starts the backend asynchronously, deferring WebView navigation until the server passes health check verification and transitions to `RUNNING (LIVE)`.

- **Auto-Start Server on Application Launch Setting (`ServerConfigService.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`):**
  - Added persistent `AutoStartServer` boolean setting stored in `server_config.json` (`%LocalAppData%\OfficeCore\server_config.json`).
  - Added a modern themed CheckBox toggle (`AutoStartServerCheckBox`) with dark styling (`ModernCheckBox`) under the Server Action panel.
  - When enabled, the Server Station automatically spins up the unified backend upon opening without requiring manual intervention.

- **Prompt to Launch OfficeCore on Manual Server Start (`MainWindow.xaml.cs`):**
  - When the user manually clicks "▶ Start Server", the app tracks the manual start state (`_isManualServerStart`).
  - Once the server status transitions to `RUNNING (LIVE)` via the health check probe, a confirmation dialog pops up asking: *"OfficeCore server is now live and healthy on http://localhost:5026. Would you like to launch OfficeCore now? [Yes] [No]"*.
  - Clicking "Yes" transitions to the embedded Web App view, while "No" keeps the user on the Live Server Console logs.
  - Auto-start launches do not display this prompt, ensuring silent background startup.


### Added & Changed — [2026-09-26] (Dashboard Redirection Fixes & Multi-Person Kanban Task Assignment)

- **Comprehensive Dashboard Link & Redirection Harmonization (`Dashboard.razor`, `DashboardController.cs`, `UserManagement.razor`):**
  - **Fixed Misconnected Cards & Buttons:**
    - Corrected the top header button from mismatched "+ New Task" text (which previously redirected to the board directory) to "Kanban Boards" with a board icon, routing directly to the workspace's primary board or board directory.
    - Fixed the **Team Headcount** metric card which was incorrectly redirecting users to `/clients`; it now directs managers and authorized personnel straight to `/admin/users` (User Management).
    - Fixed the **Pending Approvals** card which previously always routed to `/admin/leave-approvals` even when only advance requests were pending; now intelligently routes to `/ledger` when pending advances are present and leaves are zero, while providing direct clickable filter chips for both Leaves and Advances.
    - Updated **Workspace Tasks** and **Task & Sprint Velocity** cards to navigate directly to the primary active Kanban board.
    - Added **Team Users** (`/admin/users`) and **Clients** (`/clients`) shortcuts into the Management Quick Actions panel.
    - Made **Live Team Activity** rows interactive and clickable, routing directly to Attendance, Leave Approvals, Ledger, or Kanban Boards.
    - Fixed **Tasks Assigned to Me** rows in the Personal ESS dashboard: each task is now a clickable card that routes directly to `/boards/{task.BoardId}` where the card is hosted.
    - Updated `UserManagement.razor` authorization from role-based `Roles = "Admin"` to policy-based `Policy = "RequireUsers"`, allowing managers with user management claims to access employee accounts seamlessly.

- **Multi-Person Task Assignment on Kanban Boards (`BoardCardAssignee.cs`, `BoardsController.cs`, `BoardView.razor`, `AppDbContext.cs`, Migration `AddBoardCardAssignees`):**
  - **Database & Architecture:**
    - Created `BoardCardAssignee` tenant-scoped join entity and database table with foreign keys to `BoardCards` and `Employees`.
    - Added EF Core migration `AddBoardCardAssignees` with automatic execution on startup via `DbSeeder.SeedAsync`.
    - Maintained full backward compatibility with single `AssignedToUserId` while allowing arbitrary multiple assignees per task card.
  - **Backend API & Real-Time Sync:**
    - Updated `GET /api/boards/{id}` to include `Assignees` with populated employee profiles (name, avatar, ID), with fallback synthesis for legacy single-assignee cards.
    - Updated `CreateCardRequest` to accept `AssigneeIds` list on card creation (`POST /api/boards/lists/{listId}/cards`) and card editing (`PUT /api/boards/cards/{cardId}`).
    - Updated `DashboardController` personal task queries to check both `AssignedToUserId` and `Assignees.Any(...)` so cards with multiple assigned team members appear on every assigned member's dashboard.
  - **Interactive Kanban UI (`BoardView.razor`):**
    - Card preview columns now display stacked avatar bubbles for all assigned members with tooltip names, hover zoom, and overflow counters (`+N`).
    - Redesigned the card modal sidebar with a multi-assignee picker featuring selected member chips with instant removal ('x'), and an interactive member roster with avatars and toggle checkmarks.

### Added & Changed — [2026-09-26] (Locations Real-Time Hub, Solution Build Fix, Live Notification Center, Role-Adaptive Dashboard & Opaque Popover)

- **Opaque Notification Center & Layout Fix (`NotificationCenter.razor`):**
  - Eliminated translucency and `backdrop-blur` artifacts that allowed underlying page buttons (like `+ New Task`) to bleed through the notification dropdown.
  - Applied solid, opaque surfaces (`#18181b` in Dark Mode, `#ffffff` in Light Mode) across header, notification list rows, and footer with crisp borders and elevated shadow (`shadow-2xl`).
  - Added dedicated scoped styling (`.oc-notif-dropdown`, `.oc-notif-header`, `.oc-notif-item`, `.oc-notif-footer`) ensuring 100% opacity parity across all browsers and WebView2 rendering engines.

- **Role-Adaptive & Personalized Workspace Dashboard (`DashboardController.cs`, `Dashboard.razor`):**
  - **Employee Self-Service (ESS) Personalization:**
    - Non-managerial staff and employees without administrative permissions now see an employee-centric dashboard rather than irrelevant organization-wide statistics.
    - Personal dashboard highlights **My Attendance Today** (live punch status, clock-in/out timestamps, hours worked), **My Assigned Tasks** (personal board cards and sprint priorities), **My Leave Requests**, and **My Salary Advance** balances.
    - Quick actions and navigation shortcuts are strictly filtered according to each user's authorized modules (e.g. Punch Clock, File Leave, Request Advance, My Kanban Board).
  - **Management & Operational View (MSS):**
    - Users with managerial and administrative module claims (`LeaveApprovals`, `Users`, `Ledger`, `Payroll`, etc.) retain access to organization-wide metrics (Active Team Headcount, Today's Attendance %, Sprint Completion, Approval Queues, Live Field Staff).
    - Added an interactive view toggle on managerial dashboards allowing managers to seamlessly switch between **📊 Team Operations** and **👤 My Personal Status**.

- **Solution Build & Embedded Resource Fix (`OfficeCore.Client.Installer.csproj`):**
  - Added existence condition `Condition="Exists('..\..\dist\OfficeCore-Client-Portable\OfficeCore.Client.exe')"` to the portable client embedded resource.
  - Resolves `CS1566: Error reading resource` failure so clean `dotnet build` succeeds across all 5 projects without requiring manual client pre-publication.

- **Locations Real-Time Tracking & Architecture Fix (`LocationsController.cs`, `Locations.razor`, `mapInterop.js`):**
  - **SignalR Group Harmonization:** Fixed group casing bug where `LocationsController` broadcasted to capital `Org_{orgId}` while `NotificationHub` subscribed clients to lowercase `org_{orgId}`.
  - **Live SignalR Field Tracking:** Connected `Locations.razor` to `NotificationHub` listening to `FieldLocationUpdated`. When riders or field staff ping GPS coordinates, the map updates marker positions dynamically with zero page reload required.
  - **Interactive Marker Pin Selection:** Wired Leaflet marker click events in `mapInterop.js` to invoke `OnStaffSelected` in Blazor via `DotNetObjectReference`, seamlessly revealing the bottom-left staff card and centering the map.
  - **Multi-Tenancy & Entity Separation:** Fixed `LocationsController.PingLocation` to resolve and store the tenant-scoped `Employee.Id` instead of global `ApplicationUser.Id`, resolving foreign key constraints and correctly querying employee roles and names.
  - **Theme Parity for Maps:** Added dynamic theme awareness to `mapInterop.js` responding to `officecore-theme-changed` events, switching from dark-inverted tiles to clean standard OpenStreetMap tiles when Light Mode is active, and styling Leaflet popups cleanly in light mode.

- **In-App Real-Time Notification Center (`NotificationCenter.razor`, `MainLayout.razor`):**
  - Replaced the static, inert notification bell in `MainLayout.razor` with a fully interactive `NotificationCenter` component.
  - Displays a dynamic unread counter badge with glowing highlight and ping animation when new alerts arrive.
  - Connects to `NotificationHub` via SignalR to capture live workspace events across:
    - Leave requests (submissions, managerial reviews, approvals)
    - Salary advances & E-Loan requests (creation, approvals, updates)
    - Field staff live GPS alerts
  - Interactive dropdown popover with categorized icon badges, relative timestamps ("Just now", "5m ago"), unread indicators, "Mark all as read", and "Clear all" actions.
  - Clicking any notification automatically marks it as read and navigates directly to the target module (`/admin/leave-approvals`, `/myleaves`, `/ledger`, `/advances`, `/locations`).

- **Dynamic Reactive Workspace Dashboard (`DashboardController.cs`, `Dashboard.razor`):**
  - Created new `DashboardController` (`GET /api/dashboard/stats`) computing live metrics scoped to the active organization:
    - Active team members count
    - Today's distinct clock-in attendance count and percentage
    - Total sprint board tasks, completed count, and in-progress count
    - Pending leave requests and salary advance requests count
    - Active field personnel count within the last 12 hours
    - Aggregated real-time workspace activity feed (latest clock-ins, leave submissions, advances)
    - Dynamic database engine detection (`PostgreSQL 18 (Npgsql)`)
  - Redesigned `Dashboard.razor` to replace hardcoded placeholder statistics with live reactive data binding, interactive refresh button, completion rate progress bar, and real-time recent activity timeline.

### Added & Changed — [2026-09-25] (Live Field Radar Map, Location Tracking & Avatar Markers)

- **Live Field Radar Map Overhaul (`Locations.razor`, `mapInterop.js`):**
  - Upgraded the Locations radar map with Leaflet.js to always render cleanly with free OpenStreetMap dark-filtered tiles without blank screens or API key dependencies.
  - Implemented 3×3 CSS grid overlay system eliminating overlapping UI controls across all viewport sizes.
  - Moved Leaflet zoom control to `topright` so it never clashes with the bottom-right global chat floating action button (FAB).
  - Integrated location geocoding search bar with instant address search via OpenStreetMap Nominatim.
  - Added interactive staff inspection cards and stacked status legend.

- **Profile Picture & Initials Map Indicator (`Locations.razor`, `mapInterop.js`):**
  - Replaced generic blue location dot indicator with the logged-in user's profile avatar.
  - **With Profile Image:** Renders high-resolution circular profile photo framed with a purple glowing border and pulsing beacon.
  - **Without Profile Image:** Dynamically generates stylized initials avatar (e.g. `DE`) with purple/fuchsia gradient matching the application design system.
  - Injected `AuthenticationStateProvider` and `IConfiguration` into `Locations.razor` to automatically pass authenticated user details into geolocation markers.

- **Desktop Security Lockdown (Production Branch):**
  - Disabled default WebView2 context menus and developer tools (`AreDefaultContextMenusEnabled = false`, `AreDevToolsEnabled = false`) on production builds for desktop client and server hosts.

### Added & Changed — [2026-09-25] (Security Hardening, Real-Time Notifications, Legacy Project Removal)

- **Removed `OfficeCore.DesktopClient` (Legacy Dual-Mode Desktop):**
  - Deleted the legacy `OfficeCore.DesktopClient` project and its source directory, which combined Server Station and Client Terminal into a single monolithic WPF app.
  - The canonical desktop deployment now uses the split `OfficeCore.Desktop.Server` (host station) + `OfficeCore.Desktop.Client` (workstation terminal) projects.
  - Removed project entry from `OfficeCore.slnx`. Updated `RUNNING.md`, `agent.md`, and `PROJECT_STATUS.md` to reflect the current two-project architecture.

- **CORS Security Hardening (`Program.cs`):**
  - Replaced `SetIsOriginAllowed(origin => true)` (allow-all) with `WithOrigins(allowedOrigins)` scoped to known dev ports (`5026`, `5261`, `7174`).
  - Production deployments can add custom origins via `AllowedOrigins` array in `appsettings.json`.

- **Login Rate Limiting & Account Lockout (`Program.cs`, `AuthController.cs`):**
  - Added `.NET 9` `AddRateLimiter` fixed-window policy `LoginPolicy`: **5 attempts per IP per minute**, returns HTTP 429 with a user-friendly message on rejection.
  - Enabled ASP.NET Identity account lockout: **5 failed attempts → 5-minute lockout** (`lockoutOnFailure: true`).
  - `POST /api/auth/login` decorated with `[EnableRateLimiting("LoginPolicy")]`; returns `HTTP 429` with a specific locked-out message when the Identity lockout activates.

- **Real-Time SignalR Notifications for Leave & Advance/ELoan (`NotificationHub`, controllers, Razor pages):**
  - Added new `NotificationHub` (mapped at `/hubs/notifications`). On connect, each user joins an `org_{orgId}` group for org-scoped event delivery.
  - `LeaveRequestController` now broadcasts `LeaveRequestCreated`, `LeaveRequestUpdated`, and `LeaveRequestDeleted` events to the org group after every mutating action (submit, approve/reject, admin-create, delete).
  - `AdvanceRequestController` now broadcasts `AdvanceRequestCreated`, `AdvanceRequestUpdated`, and `AdvanceRequestDeleted` events after every mutating action (create, approve, reject, cancel, admin-create, delete).
  - **`LeaveApprovals.razor` (admin):** Connects to `NotificationHub`; auto-reloads the approvals table and shows a toast notification when any employee submits a new leave request. Also reloads on status changes and deletions from other sessions.
  - **`MyLeaves.razor` (employee):** Connects to `NotificationHub`; status badge updates live (no page refresh needed) when an admin approves or rejects the employee's leave request.
  - **`Ledger.razor` (admin):** Connects to `NotificationHub`; advance/loan approvals table refreshes live when employees submit, cancel, or modify requests.
  - **`Advances.razor` (employee):** Connects to `NotificationHub`; request status updates live when an admin approves or rejects.
  - All hub connections use `WithAutomaticReconnect()` and graceful error handling so pages remain functional if SignalR is unavailable.

- **Work Schedule & Unpaid Break Configuration (`DbSeeder.cs`):**
  - Standardized the default organization schedule to **8:00 AM – 5:00 PM** across Monday through Friday (8 working hours/day) with Saturday and Sunday designated as rest days.
  - Configured a 1-hour unpaid lunch break from **12:00 PM – 1:00 PM** (`WorkScheduleBreak`) and assigned all employees to this schedule.
- **Multi-Role Employee Accounts & Distinct Capabilities (`DbSeeder.cs`):**
  - Expanded company roster to 3 distinct test employees with tailored roles and capabilities:
    1. **Alex Staff** (`EMP-002`, `employee@officecore.com`): Daily rate ₱800.00 (₱100.00/hr), assigned standard **Employee** role with ESS self-service claims (`Dashboard`, `Boards`, `MyLeaves`, `Advances`).
    2. **Beatriz Ramos** (`EMP-003`, `beatriz.ramos@officecore.com`): Daily rate ₱1,200.00 (₱150.00/hr), assigned new **HR Manager** role with operational & managerial claims (`Attendance`, `Users`, `LeaveApprovals`, `TimeOff`, `WorkSchedules`, `Advances`, etc.).
    3. **Carlos Mendoza** (`EMP-004`, `carlos.mendoza@officecore.com`): Daily rate ₱1,000.00 (₱125.00/hr), assigned new **Finance Specialist** role with financial & accounting claims (`Payroll`, `Ledger`, `Invoices`, `Advances`, `MyLeaves`).
  - Ensured all 3 employees have `EmployeeGovernmentRecord` entries with deduction eligibility enabled (`IsEligibleForDeductions = true`) and default `GovernmentDeductionSettings` (SSS, PhilHealth, Pag-IBIG).
- **1-Week Attendance Scenarios for Payroll Generation Testing (`DbSeeder.cs`):**
  - Seeded 1 full work week of attendance logs (Monday Sept 21 to Friday Sept 25, 2026) testing three specific payroll edge cases:
    - **Scenario 1 (Alex Staff - Early Bird / Perfect Attendance):** Punched in early every morning (between 7:30 AM and 7:50 AM) and punched out at or shortly after 5:00 PM across all 5 working days (40.53 net hours, 5 days present).
    - **Scenario 2 (Beatriz Ramos - Irregular Attendance / Absent & Mid-Day Lunch Punch):** Late arrivals (9:30 AM on Monday, 8:45 AM on Thursday), mid-day punch at 12:45 PM during lunch break (Tuesday) with auto-overlap deduction, and 1 day absent on Wednesday (25.75 net hours, 4 days present).
    - **Scenario 3 (Carlos Mendoza - Overtime & Undertime Variations):** Extended evening overtime until 7:30 PM on Monday and 6:00 PM on Friday, early departure / undertime at 3:00 PM on Tuesday, and late+early departure on Thursday (40.50 net hours, 5 days present).
  - Seeded ready-to-compute Draft `PayrollPeriod` for September 21–27, 2026 with 25% weekly deduction multiplier (`DeductionMultiplier: 0.25`).
- **Payroll Computation Engine Timezone & Pre-Shift Enhancements (`PayrollComputationService.cs`):**
  - Fixed timezone clipping bug: Converted local `period.StartDate` and `period.EndDate` to UTC bounds so early morning punches in positive UTC offsets (e.g. UTC+8) are never clipped at 00:00 UTC.
  - Implemented pre-shift cap: Capped early morning clock-ins before scheduled shift start time (8:00 AM) when `IncludeTimeTrackedBeforeScheduledStartTime` is false, ensuring early arrivals are counted as on-time standard attendance rather than unapproved overtime.


### Added & Changed — [2026-09-23] (Attendance UI Bug Fixes, Tab Isolation & Multi-Account Role Testing)
- **Attendance UI Razor Bug Fix & Strict Tab Isolation (`Attendance.razor` & `AttendanceSettings.razor`):**
  - Resolved Razor template rendering issue where `else if` statements separated by HTML comments lacked the `@` prefix, causing raw C# code (`else if (_canManageAttendance && _activeTab == "company") {`) to be rendered directly into the DOM and causing all tabs (My Attendance, Company Records, Device Settings) to leak and render simultaneously.
  - Converted tab sections to isolated, independent `@if` blocks (`@if (_activeTab == "my")`, `@if (_canManageAttendance && _activeTab == "company")`, and `@if (_canManageAttendance && _activeTab == "settings")`), strictly isolating UI views based on the active tab and user permissions.
  - Cleaned up the embedded `AttendanceSettings.razor` component by removing redundant outer `max-w-7xl mx-auto p-6` containers and duplicate `<h1>` headers for a seamless fit under the Device Settings tab.
  - Adjusted shift text display on the Scheduled Shift metric card (`text-base font-bold` with tooltip) to prevent truncated shift schedules (`09:00 AM - 06:00 PM`).
- **Multi-Role Demo Accounts & Fast Demo Access (`DbSeeder.cs` & `Login.razor`):**
  - Provisioned standard Employee demo account (`employee@officecore.com` / `Password123!`) assigned to "Alex Staff" (`EMP-002`, standard Employee/Member role, flexible work schedule).
  - Updated the Login page (`Login.razor`) with dual Quick Demo Access buttons:
    - `👑 Admin Demo` (populates `demo@officecore.com` with administrative privileges).
    - `👤 Employee Demo` (populates `employee@officecore.com` with standard employee privileges).
  - Verified role-based UI security in Chrome: Standard employees see only the personal Workstation Time Clock hub and their personal attendance log, with administrative tabs (`Company Records`, `Device Settings`) and the `+ Manual Entry` button completely hidden.

### Added & Changed — [2026-09-23] (Tiered Attendance System & Employee Self-Service)
- **Employee Self-Service (ESS) Personal Attendance Hub (`AttendanceController.cs` & `Attendance.razor`):**
  - Implemented `GET /api/attendance/my` under standard `[Authorize]` allowing all active employees to access their personal attendance history, shift schedule, today's elapsed hours, and monthly stats without needing administrative claims.
  - Implemented `POST /api/attendance/punch` allowing employees to clock in and clock out directly from their workstation or web browser with automatic punch-type detection (Clock In vs Clock Out) and SignalR broadcast.
  - Added `Web` value to `AttendanceSource` enum to distinguish browser-based workstation punches from hardware fingerprint scanner scans and manual adjustments.
  - Redesigned `/attendance` as a unified role-aware hub:
    - **Personal Attendance Hub (For all employees):** Live digital clock with date, real-time clock-in status badge (with animated green pulse when active), scheduled shift indicator, big responsive 1-click punch button, 4 quick stat cards (Hours Worked Today, Scheduled Shift, Hours This Month, Days Present), and personal read-only attendance log table with date filtering.
    - **Management Hub (For users with `Attendance` module claim):** Top tabs to toggle between "My Attendance", "Company Records" (searchable, filterable company logs with manual entry modal and deletion modal), and "Device Settings" (USB COM & TCP Wi-Fi scanner management).
- **Attendance Multi-Tenancy Hardening & Security Scoping (`AttendanceController.cs`):**
  - Fixed cross-tenant data leak by strictly scoping `GET /api/attendance` queries to `Where(a => a.OrganizationId == orgId.Value)`.
  - Fixed identity mapping in `POST /api/attendance/admin-create` to resolve against `_context.Employees` (instead of `_context.Users`) and explicitly assign `OrganizationId`.
  - Restricted company records, manual adjustments, deletions, and biometric scanner endpoints under `[Authorize(Policy = "RequireAttendance")]`.
- **Role Permissions & Navigation Alignment (`DbSeeder.cs` & `NavMenu.razor`):**
  - Updated `DbSeeder.cs` to restrict the management `Attendance` claim to Admins and Managers, removing it from default standard `Member`/`Employee` roles while ensuring employees have access to personal attendance.
  - Updated `NavMenu.razor` to ensure the "Attendance" navigation link under "Main" is universally accessible to all authenticated workspace members.

### Added & Changed — [2026-09-23] (PostgreSQL 18 Migration & POS-Style Desktop Hub)
- **Database Migration to PostgreSQL 18 (`OfficeCore.Api`):**
  - Reverted database engine from SQLite to PostgreSQL 18 via `Npgsql.EntityFrameworkCore.PostgreSQL` (`v9.0.3`) for robust enterprise concurrency, multi-client LAN stability, and scalability.
  - Configured PostgreSQL connection string in `appsettings.json` (`Host=localhost;Port=5432;Database=OfficeCoreDb;Username=postgres;Password=password;Include Error Detail=true`).
  - Switched `Program.cs` to `options.UseNpgsql(...)`.
  - Updated `AppDbContext.cs` entity model mappings: Replaced SQLite `.HasColumnType("TEXT")` with native PostgreSQL high-precision arbitrary arithmetic types: `.HasColumnType("numeric(18,2)")` for currencies/balances and `.HasColumnType("numeric(18,4)")` for rates and percentage multipliers.
  - Generated clean initial migration `20260923014612_InitialPostgreSql` and executed `dotnet ef database update`, creating `OfficeCoreDb` on PostgreSQL 18 and establishing all 40+ tables, relations, and indexes.
  - Verified `DbSeeder.cs` runs cleanly against PostgreSQL on API startup, auto-migrating and populating default accounts (`demo@officecore.com`), "Demo Corp" organization, roles, work schedule days, and sample boards.
- **Unified Single-Port Web Hosting (`OfficeCore.Api` & `OfficeCore.Web.Client`):**
  - Configured `OfficeCore.Api` to reference `OfficeCore.Web.Client.csproj` and serve Blazor WebAssembly static assets directly on unified port `5026` via `app.UseBlazorFrameworkFiles()` and `app.MapFallbackToFile("index.html")`.
  - Enables workstation terminals to connect to `http://<SERVER_IP>:5026` for both the application interface and REST/SignalR APIs without running a separate web server process.
- **POS-Style Desktop Hub & Terminal Workstations (`OfficeCore.Desktop.Server` + `OfficeCore.Desktop.Client`):**
  - Transformed the WPF desktop client into an on-premises deployment hub supporting two startup modes:
    - **Server Station (Primary Host):** Automatically discovers the server PC's primary non-virtual LAN IPv4 address (via `NetworkHelper.cs`), tests PostgreSQL connection on port `5432`, launches and manages `OfficeCore.Api` as a child process (via `ServerProcessManager.cs`), displays real-time stdout/stderr log stream, and features a "Launch OfficeCore Locally" button embedding WebView2.
    - **Client Terminal (Workstation):** Lightweight workstation mode for other office PCs to input the host server's LAN IP or URL, validate connectivity, and render the application in a dedicated full-screen desktop WebView2 frame.
  - Added `DesktopConfigService.cs` to persist startup mode preferences, connection strings, and server URLs in `desktop_config.json`.
  - Redesigned `MainWindow.xaml` with modern dark theme styling (`#0C0A09` / `#121215`) matching OfficeCore aesthetics.
  - **Collapsible WebView Controls & Screen Real-Estate Optimization:**
    - Automatically hides the main window header bar (`WindowTopHeader`) when viewing the web application to eliminate double-header clutter.
    - Consolidated web navigation into a single ultra-slim (34px) bar with `← Server Dashboard` / `← Disconnect`, `🔄 Reload`, `⚙️ Switch Mode`, and live connection indicator.
    - Added a collapsible mini floating tab (`[ 🖥️ Host Controls ▾ ]` / `[ 💻 Terminal Controls ▾ ]`) that reduces toolbar footprint to 22px when minimized, maximizing workspace space for the web application.
- **Process Resilience & Socket Hardening:**
  - Added port collision detection in `ServerProcessManager.cs` to seamlessly adopt already-running `OfficeCore.Api` instances on port `5026` rather than crashing on duplicate socket binds.
  - Added premature process exit detection in `PollHealthAsync` and external process cleanup in `StopServer()`.
  - Hardened `FingerprintDeviceService.cs` with graceful `try/catch` exception handling on TCP listener port 9000 to prevent startup crashes in case of port contention.
  - Added `-y` flag to `npx concurrently` in root `package.json` for non-interactive runner execution.

### Added & Changed — [2026-09-23]
- **Documentation Synchronization (`PROJECT_STATUS.md` & `RUNNING.md`):**
  - Updated `PROJECT_STATUS.md` to accurately document the current system architecture: C# .NET 9 LTS Web API, SQLite (`officecore.db`) with high-precision financial decimal mapping, Blazor WebAssembly with Tailwind CSS v4, hybrid ESP32 fingerprint biometric scanner (Web Serial at 9600 baud + TCP socket on port 9000), and WPF Edge WebView2 desktop client.
  - Formally documented the completion of the Leave Management (PTO) system (Employee Self-Service requests with medical/proof attachments, Management Self-Service review/approval dashboard, and automatic integration with the Payroll computation engine).
  - Overhauled `RUNNING.md` to remove legacy PostgreSQL instructions and replace them with zero-friction SQLite setup, npm start concurrently runner, port bindings, and ESP32 hardware connection guide.
- **Board Card Attachments API Hardening & Scoping (`BoardsController.cs`):**
  - Refactored `POST /api/boards/cards/{cardId}/attachments` to strictly enforce multi-tenant isolation (`c.OrganizationId == orgId.Value`), board membership verification (excluding Viewers from uploads), file size validation (20MB limit), and safe path sanitization via `IWebHostEnvironment`.
  - Refactored `DELETE /api/boards/attachments/{id}` to enforce tenant isolation and authorize deletions strictly to the original uploader or board Owners.
  - Standardized JSON responses to return clean DTO projections matching `BoardCardAttachmentDto` (`Id`, `FileName`, `FilePath`, `FileSize`, `UploadedAt`).

### Added & Changed — [2026-09-22]
- **UI/UX Modernization, Unified Settings & Light Mode System:**
  - **Unified Settings Page (`Settings.razor`):** Consolidated separate `Profile.razor` and `CompanyProfile.razor` into a single `/settings` page supporting `/settings`, `/profile`, `/organization`, and query-string tabs (`?tab=profile`, `?tab=company`). Features role-aware tab access, profile avatar uploads, password changes, organization branding, address settings, dynamic phone number management, and workspace deletion.
  - **Sidebar Navigation Optimization:** Removed the redundant "Company Profile" button from the main sidebar navigation. All personal and organizational settings are now consolidated in the sidebar footer user profile card.
  - **Full Light Mode System:**
    - Implemented a complete Light Mode theme cascading across the entire application via Tailwind CSS v4 CSS variables (`--color-zinc-950`, `--color-zinc-900`, etc.) mapped under `html.light` in `Styles/app.css`.
    - Zero-flicker anti-FOUC script in `index.html` synchronously reading `localStorage` before page rendering.
    - Added JavaScript theme interop service (`themeInterop.js`) for theme toggling, storage, and system color-scheme preference syncing.
    - Created `ThemeToggle.razor` with animated SVG Sun/Moon icons, integrated beside the notification bell in `MainLayout`, and in the top headers of `Home.razor`, `Login.razor`, `SignUp.razor`, and `Workspaces.razor`.
    - Preserved high-contrast button styling (`.keep-white`) across primary buttons and gradients.
  - **Responsive Shell & Typography:** Upgraded mobile layout with an off-canvas slide-out navigation drawer in `MainLayout.razor`, integrated Google Font Inter typography, and refined glassmorphic cards (`.glass-card`).

- **SQLite EF Core Compatibility, High-Precision Decimals & Demo Account:**
  - **EF Core SQLite Compatibility Fixes:** Resolved query translation issues specific to SQLite across `AdvanceRequestController`, `AttendanceController`, `BoardsController`, `ChatController`, and `UserController` (avoiding unsupported LINQ date/time operations).
  - **High-Precision Decimal Mappings:** Mapped financial rates, loan balances, and government deduction fields to SQLite `TEXT` columns in `AppDbContext` to avoid floating-point inaccuracies (`20260922124918_FixDecimalPrecision`).
  - **Policy Enforcement:** Corrected authorization policies on `LeaveRequestController`, `PayrollSettingsController`, and `FingerprintDeviceController`.
  - **Automated Demo Account & Workspace Seeding:** Enhanced `DbSeeder.cs` to automatically provision a preconfigured demo account (`demo@officecore.com` / `Password123!`), a "Demo Corp" organization workspace, standard roles (`Admin`, `Employee`), default work schedule with break rules, and a sample Kanban board.
  - **Desktop Client UI Polish:** Added a centered glassmorphic server URL connection screen that auto-hides once connected to the Web API.

- **SQLite Database Migration, .NET 9 LTS Standardization & Desktop Client:**
  - **Database Migration to SQLite:** Migrated primary database from PostgreSQL to SQLite (`Microsoft.EntityFrameworkCore.Sqlite`), providing a zero-dependency, self-contained local database file (`officecore.db`) with a unified initial migration.
  - **.NET 9 LTS Standardization:** Downgraded target framework across all projects from .NET 10 preview to .NET 9 LTS (`net9.0`) for long-term support and tooling stability.
  - **Desktop Client (`OfficeCore.Desktop.Server` + `OfficeCore.Desktop.Client`):** Created two dedicated WPF applications — a Server Station host and a Client Terminal — each wrapping Blazor WebAssembly via Microsoft Edge WebView2.

### Added & Changed — [2026-08-17]
- **Organization Profile Enhancements & Secure Deletion:**
  - **Organization Logo & Branding:** Added logo upload capabilities with physical disk storage in `Uploads/` and database URL association.
  - **Address & Dynamic Phone Management:** Added `Address` to `Organization` and introduced `OrganizationPhone` entity supporting multiple contact numbers (Phone, Mobile, Fax, Toll-Free) with primary designation.
  - **Password-Protected Organization Deletion:** Secured `DELETE /api/organizations/{id}` to require the active admin's account password verification via ASP.NET Identity before purging the workspace.
  - **Company Profile UI:** Rich company profile editor with logo dropzone, phone number manager, address inputs, and dangerous zone deletion modal with password prompt.

### Added — [2026-06-26]
- **Startup Cheat Keys:**
  - Created VS Code `tasks.json` enabling the start of Tailwind CSS, API, and Web frontend either individually or all together via the integrated terminal (`Ctrl+Shift+B`).
  - Created a root `package.json` for NPM with `start:css`, `start:api`, `start:web`, and a `start` script using `concurrently` to run all services simultaneously.
  - Fixed syntax error in `AttendanceController` preventing API compilation.

### Changed — [2026-06-24]
- **Fingerprint Scanner TCP & Wired Priority Fixes:**
  - Added dynamic cache-busting to `webserial.js` import to ensure UI changes (like Disconnect Wi-Fi) bypass browser cache.
  - Implemented TCP reconnect cooldown in ESP32 firmware to prevent blocking Serial communication when Wi-Fi/TCP drops, prioritizing Wired mode responsiveness.
  - Added UI button and JS bindings to completely erase Wi-Fi credentials (`RESET_WIFI`) and switch the device back to purely Wired USB Mode.

### Changed — [2026-06-22]
- **Attendance Settings UI Overhaul:**
  - Redesigned the Settings tab to match the professional minimalist style of other modules (dark `zinc-900` cards, standard inputs, no flashy gradients).
  - Restructured layout to a single combined card with a 2-column inner grid: **Device Connection settings** on the left and **Direct USB Connection** on the right, eliminating excess whitespace.
  - The overall settings page now uses a 2-column layout (`xl:grid-cols-2`) pairing the Device card with the Biometric Enrollment card side by side.
  - Removed the scanning toast notification that fired on every fingerprint scan.
- **Fingerprint Enrollment Fixes:**
  - **Fixed critical enrollment 500 error:** The `EnrollSuccess` API endpoint was incorrectly looking up `_context.Users` by Employee ID. Fixed to use `_context.Employees.FindAsync()` since `EmployeeFingerprint.EmployeeId` is a FK to `Employee.Id`. Also set `OrganizationId` on the new fingerprint record (was missing, causing multi-tenant constraint failure).
  - **Fixed wrong ID sent for enrollment:** The employee dropdown was using `User.Id` (ASP.NET Identity ID) as the option value. Fixed to use `Employee.EmployeeId` (the `Employee` table PK) which is the correct FK for `EmployeeFingerprint`. Added `EmployeeId` field to the `EmployeeDto` class.
  - **Fixed fingerprint mismatch retry:** Updated `ESP32_Fingerprint_Device.ino` to loop back to first-scan when fingerprints don't match (`INFO: Fingerprints did not match. Please try again.`) instead of exiting enrollment mode. Also added CANCEL command handling in the second-scan loop.
  - **Added enrollment cancellation:** Added a "Cancel" button and `CancelEnrollment()` method that sends a `CANCEL` command to the ESP32 over USB/TCP, cleanly halting the enrollment process.
- **Web Serial (USB) Reliability Fixes:**
  - **Fixed "port already open" error on navigation:** When navigating from Attendance Records back to Settings, the component re-mounted and tried to re-open an already-open serial port. Fixed `initSerial` in `webserial.js` to detect if the port is already open (`savedPort.readable`) and re-attach to it without calling `port.open()` again.
  - **Fixed baud rate mismatch:** `webserial.js` auto-connect was using `57600` baud while the ESP32 `Serial.begin()` uses `9600`. Fixed both auto-connect and manual connect paths to use `9600`.
  - **Auto-reconnect on page revisit:** `initSerial` now calls `navigator.serial.getPorts()` on mount to automatically reconnect to a previously approved USB scanner without requiring the user to click "Connect" every time.
  - **Fixed DotNetObjectReference disposed error:** The component no longer disposes `_dotNetRef` on navigation away. The JS module retains the reference for ongoing attendance scan callbacks; it is replaced with a fresh reference on the next component mount.
- **ESP32 Firmware (`ESP32_Fingerprint_Device.ino`):**
  - Refactored `enrollFingerprint()` to wrap the full scan cycle in an outer `while(isEnrollMode)` loop, enabling automatic retry on fingerprint mismatch without re-entering enrollment mode.
  - Added CANCEL command checks inside the second-scan polling loop (both USB Serial and TCP Wi-Fi paths).
  - Sends `INFO: Fingerprints did not match. Please try again.` and loops back instead of sending `ERROR:` and aborting.
- **Fingerprint Scanner & Attendance (initial setup):**
  - Migrated fingerprint enrollment and device management from User Management to a dedicated "Settings" tab in the Attendance module.
  - Implemented a Hybrid Device Architecture:
    - **Wired Connection:** Added Web Serial API support allowing browsers to connect directly to local USB scanners for immediate scanning and enrollment.
    - **Wi-Fi Connection:** Refactored backend service from local `SerialPort` to `TcpListener` (Option A), allowing remote cloud scanners to push logs as TCP Clients.
  - Device settings are now fully multi-tenant, managed via a new `FingerprintDeviceController` and `FingerprintDevices` table.
- **Boards Tenant Isolation & Scoping:**
  - Fixed board lists, cards, comments, checklists, checklist items, and attachments to correctly associate the active `OrganizationId` upon creation.
  - Fixed Board member addition to resolve and store the target workspace-scoped `Employee.Id` rather than the global `ApplicationUser.Id`.
- **Payroll & Ledger Multi-Tenancy & Logic Fixes:**
  - Fixed a critical multi-tenancy bug where default Government Deduction Settings and General Payroll Settings were created without an `OrganizationId`, causing foreign key constraint errors.
  - Implemented proportional Government Deductions calculation (SSS, PhilHealth, Pag-IBIG) using a 28-day base month.
  - Added a "Gov. Deduction Cycle" setting to the Payroll Period creation modal, allowing the selection of Full Month (100%), Semi-Monthly (50%), Weekly (25%), or No Deductions (0%).
  - Fixed an identity mapping error in Admin Leave Creation and E-Loan/Cash Advance Creation where the global `UserId` was incorrectly saved instead of the workspace `Employee.Id`, preventing foreign key failures.
  - Resolved a timezone shifting issue where leave request dates were shifting a day backward upon UTC serialization by locking the selected date accurately using `DateTime.SpecifyKind`.
  - Updated E-Loan payroll computation to automatically skip the current payroll period and begin deductions strictly on the next payroll period following the loan's approval date.
- **Client Management Scoping:**
  - Set missing `OrganizationId` on `Business` sub-entities during creation and updates to ensure complete multi-tenant scoping.
- **Real-Time Global Chat Optimizations:**
  - Implemented optimistic UI rendering on the global chat widget to display outgoing messages immediately without waiting for server response.
  - Added message ID verification in the SignalR message handler to prevent duplicate entries of optimistically-added messages.
  - Fixed a missing `User` join in `ChatController` that was throwing a silent `NullReferenceException` and preventing real-time messages from being broadcasted via SignalR.
- **Work Schedules Fixes:**
  - Fixed an issue where Auto Deductions were incorrectly tied to the Overtime Rules toggle, causing standard breaks to be cleared and ignored if overtime was disabled.
  - Improved error handling in the API and UI to prevent silent failures when saving schedules.
- **Global UI Feedback System (Toasts):**
  - Implemented a centralized `ToastService` and global `ToastContainer` (positioned at the top right) to replace hardcoded, silent, or localized error/success messages.
  - Migrated `WorkSchedules`, `UserManagement`, `RoleManagement`, `Workspaces`, `Join`, `Profile`, `BoardView`, `Clients`, `TimeOff`, `Ledger`, `Boards`, `MyLeaves`, `LeaveApprovals`, `Advances`, `PayrollList` and `PayrollDetails` components to utilize the new Toast system.
  - Standardized delete/archive actions (`WorkSchedules`, `RoleManagement`, `BoardView`, `Clients`, `TimeOff`, `LeaveApprovals`, `Advances`, `PayrollList`, `PayrollDetails`) by adding confirmation modals prior to execution.
- **UI Enhancements:**
  - Upgraded `UserManagement` table headers to include interactive sorting functionality with proper visual cues and SVG icons.
  - Refactored `Clients` page from a grid of cards to a functional data table, introduced a search filter, and added the ability to edit assigned businesses inline.

### Changed — [2026-06-19]
- **Standardized UI Headers:** Standardized page headers (titles and descriptions) across all Blazor pages (`Workspaces`, `CompanyProfile`, `Invoices`, `Locations`, `Dashboard`, `MyLeaves`, `LeaveApprovals`, `PayrollSettings`, `Profile`, and `PayrollList`) to use consistent premium CSS classes (`text-3xl font-bold tracking-tight text-white` and `mt-1 text-zinc-400 text-sm`).
- **Improved Invitation Logic:**
  - Added email trimming and database verification in the API.
  - Checked that the recipient email belongs to an existing user in the database.
  - Ensured the recipient is not already a member of the target workspace.
  - Prevented duplicate pending invitations.
  - Improved standard response error messaging.
- **Invitation Modal UX Enhancements:** Updated `UserManagement.razor` modal tab switching to clear generated links, success messages, and validation errors when switching between "By Link" and "By Email" tabs. Also ensured submit button disabling states are tab-sensitive.

### Added — Phase 6: Member Roles (Multi-Tenant RBAC) - [2026-06-18]
- **Isolated Roles:** Migrated Role-Based Access Control (RBAC) to be fully multi-tenant, replacing global `AppRole` with `OrganizationRole` and `OrganizationRoleClaim`.
- **Employee Mapping:** Roles are now assigned to `EmployeeRole` mapping table, correctly connecting the tenant-specific user (`Employee`) to their tenant-specific roles.
- **Dynamic Provisioning:** Automatically provisions baseline `Admin` and `Member` roles upon organization creation, assigning the creator the `Admin` role.
- **Invitations Default Role:** New users accepting invitations are automatically placed in the `Employee` role.
- **Default Role Rename:** Renamed the default role generated upon organization creation from `Member` to `Employee` to align with the system's global Employee terminology.
- **Authorization Context:** Rewrote `/api/auth/me` to dynamically resolve module claims based on the user's roles within their active organization, removing the temporary "grant all" hack.
- **UI Refactoring:** Updated Role Management and User Management admin pages to seamlessly interact with tenant-scoped roles.
- **User Management Modal:** Refactored layout to move Resign/Remove buttons to the footer, updated headers for a premium aesthetic, and fixed padding/alignment issues. Government Deductions toggle now correctly defaults to off.

### Fixed — Phase 6
- **Module Synchronization:** Fixed a discrepancy in the module registries between the backend and frontend that was completely hiding the `LeaveApprovals` and `MyLeaves` modules from the Sidebar and Role Management assignment list.
- **Port Conflict Crashes:** Resolved a severe local dev issue where ghost processes holding port 5261 were causing `dotnet watch` to crash and serve corrupt, 404-ing WebAssembly files.

### Added — Phase 4: Multi-Tenant Feature Isolation
- **TenantService** (`Services/TenantService.cs`): New scoped DI service that centralizes org-context resolution by reading the `ActiveOrganizationId` cookie and resolving the current user's `Employee` record for that org.
- **`IHttpContextAccessor`** registered in `Program.cs` to support `TenantService`.

### Changed — Phase 4: Multi-Tenant Feature Isolation
- **ChatController**: All queries now scoped by `OrganizationId`. New `ChatRoom` and `ChatMessage` records get `OrganizationId` on creation. `GET /api/chat/users` returns only employees of the active org, now includes `ApplicationUserId` so the frontend can identify the current user. DM endpoint (`POST /api/chat/direct/{employeeId}`) now takes `Employee.Id`, not `ApplicationUser.Id`.
- **ChatHub**: SignalR group names changed from `user_{userId}` to `org_{orgId}_employee_{employeeId}` for complete cross-organization real-time isolation. `JoinRoom` now verifies room membership before subscribing.
- **BoardsController**: All board queries scoped by `OrganizationId`. New `Board`, `BoardMember`, and `BoardList` records created with `OrganizationId`. Membership checks use `Employee.Id`.
- **UserController** (`GET /api/user`): Now filtered by the active `OrganizationId` — only returns employees of the current organization.
- **GlobalChatWidget.razor**: Switched from `ApplicationUser.Id` to `Employee.Id` (`_currentEmployeeId`) for "isMine" message detection and room member filtering. Resolves self-identity via `ApplicationUserId` field returned by the users endpoint.

---

### Added — Phase 3: Invitation System & Employee Management
- **Invitation System**: `OrganizationInvitation` model, EF migration `AddOrganizationInvitations`, and `POST /api/organizations/{id}/invite` endpoint to generate unique token-based invitation links.
- **Join Organization Flow**: `POST /api/organizations/join` backend endpoint + `/join/{token}` Blazor page allowing users to accept an invitation and instantly join an organization.
- **GetCurrentOrganization API**: `GET /api/organizations/current` returns the currently active org's name and ID.

### Changed — Phase 3
- **Employee Management**: "Add Employee" replaced with "Invite Member" in `UserManagement.razor`. A new two-tab modal produces a shareable invite URL (By Link) or sends it directly to a user's workspace (By Email). Admins can no longer edit `FirstName`/`LastName` on employee records (those are personal global user fields).
- **Join Organization Flow**: `Join.razor` now acts as a standalone landing page (using `EmptyLayout`) displaying invitation details before explicitly requiring the user to click "Accept". Redirects to `/workspaces` upon success.
- **Workspaces Dashboard**: Displays the logged-in user's profile and includes a new "Pending Invitations" section for direct email invites.
- **Login**: Added a "Remember me" checkbox that auto-fills the last successfully authenticated email.

---

### Added — Phase 2: Auth Redesign & Landing Page
- **Landing Page**: `Home.razor` with Hero section, Features list, and Call-to-Action.
- **Workspaces Dashboard**: `/workspaces` page for org selection, creation, and invitation acceptance.
- **SignUp Page**: New `/signup` registration flow capturing `FirstName`, `LastName`, `Email`, `Password`. Auto-signs in and redirects to workspaces.
- **Organization Switcher**: Dropdown in `MainLayout.razor` top bar to seamlessly switch between orgs.
- **Organizations API**: `GET /api/organizations/my`, `POST /api/organizations`, `POST /api/organizations/{id}/select`.

### Changed — Phase 2
- Refactored old `Home.razor` → `Dashboard.razor` at `/dashboard`.
- Redesigned `Login.razor` with modern UI, links to sign-up and Google Auth placeholder.
- `AuthController` now includes `/register` endpoint with auto sign-in.
- `ActiveOrganizationId` persisted via `HttpOnly` secure cookie on the backend when an org is selected.

---

### Added — Phase 1: Multi-Tenant Architecture
- **Multi-Tenant Database Schema**: `ApplicationUser` (global), `Organization`, `Employee` (junction with org-specific fields). All domain entities implement `ITenantEntity` with `OrganizationId`.
- **Global Chat System**: Real-time floating chat bubble widget supporting 1-on-1 DMs, unread badges, typing indicators, and SignalR delivery.
- **Member Avatars**: Kanban boards display profile pictures for top members in board summary cards.
- **Board Profile Pictures**: Individual Kanban board view shows profile pictures for board members.
- **Profile Avatars**: Users can upload a custom profile picture, visible in the sidebar and profile page.
- **Profile Redesign**: "My Profile" page with dynamic avatar upload dropzone.
- **Board Pinning/Starring**: Users can star boards to keep them at the top of the list.
- **Card Drag-and-Drop**: Precise card reordering within and across lists.
- **Due Date Indicators**: Color-coded due dates (Red = overdue, Amber = tomorrow, Zinc = future).
- **Checklist Deletion**: Trash icon to delete checklists from cards.
- **Board Manual Refresh**: Refresh button on the Boards index page.
- **Boards Module (Kanban)**: Full Trello-style Kanban with HTML5 drag-and-drop, lists, cards, labels, checklists, attachments, comments.
- **Role-Based Board Access**: Owner / Member / Viewer roles per board.
- **Board Deletion**: Board Owners can permanently delete boards.

### Changed — Phase 1
- `Home.razor` renamed/refactored to `Dashboard.razor`.
- Redesigned `Login.razor` with modern deep-dark aesthetic.

### Added
- Created `OrganizationClaimsTransformation` to dynamically inject organizational roles and module access into ClaimsPrincipal based on the active workspace.
- Added `EmployeeRole`, `OrganizationRole`, and `OrganizationRoleClaim` entities for multi-tenant RBAC.
- Added LAN access capabilities by binding to `0.0.0.0` in `launchSettings.json` and dynamically resolving API base URL in the Blazor WebAssembly frontend.

### Fixed
- Resolved 403 Forbidden errors when navigating between workspaces by correctly populating `ModuleAccess` claims via claims transformation.
- Fixed UI layout issue where `Advances & Loans` page was unclickable due to an invisible empty state covering the viewport (`absolute inset-0`).
- Fixed large empty padding gap at the top of all pages in `MainLayout.razor` by removing unnecessary `pt-20` class.
- Fixed `OperationCanceledException` crashes when switching pages rapidly while SignalR `HubConnection` is still starting up.

### Fixed — Phase 1
- Kanban event bubbling: card drag no longer triggers parent list drag.
- Viewer role enforcement: "Add Card" button and drag hidden for Viewers.
- Board member role serialization bug (Viewer/Member incorrectly elevated to Owner).
- Board flash/flicker during SignalR updates when actively dragging.
- `IsEmployee` toggle in User Management to exclude non-employee accounts from payroll.
- Weekly Rate pay mode for Fixed Work Schedules (hides time inputs, ignores attendance).
- Forced password change on first login (`MustChangePassword` flag).
- Detailed registration error display in User Management modal.

---

## [Previous Milestones]

### Core System
- Configured ASP.NET Core Identity with custom `Employee` and `AppRole` entities
- Implemented Cookie-based Authentication for Blazor WebAssembly
- Created dynamic role-based and claims-based authorization (`NavMenu` protected routes)
- Configured Entity Framework Core with PostgreSQL

### Attendance & Time Tracking
- Real-time Attendance tracking via SignalR (`AttendanceHub`)
- Fingerprint Device Service integration (ZKteco over COM ports)
- Interactive map for remote check-ins
- Work Schedules module (break rules, overtime, auto-deductions, rest days)
- Time-Tracked Payroll Engine: parses ClockIn/ClockOut pairs, deducts breaks, computes `BasicPay` from tracked hours

### Human Resources
- Time Off & Holidays management (Company-wide Holiday Calendar)
- Leave Request system (`MyLeaves` for employees, `LeaveApprovals` for admins)
- User Management dashboard (Create/Edit profiles, roles, government IDs)
- Advances & Loans module (Cash Advances and E-Loans)
- Ledger module for employee deductions and additions

### Finance & Payroll
- Payroll Settings (SSS, PhilHealth, Pag-IBIG config tables)
- Clients and Invoices tracking modules
- Global minimum rate configuration
- Leave Pay column restored in Payroll Details table

### Removed
- `CutoffDate` property dropped from Payroll (Start/End dates serve this purpose).
- Separate Undertime/Late Deduction fields deprecated (raw time-tracking handles this implicitly).

### UI / UX
- Collapsible sidebar with grouped module categories
- Custom dark-themed scrollbars globally
- Standardized premium deep-dark aesthetic across Advances, Ledger, WorkSchedules, TimeOff
- Dynamic sidebar hides empty categories based on user module access
