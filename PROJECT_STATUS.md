# OfficeCore - Master Project Status & Architecture Overview

## 🎯 1. System Overview & Architecture
**OfficeCore** is a modern, monolithic internal company management and HR platform. It centralizes employee management, automated biometric attendance, time-tracked payroll, Kanban project management, real-time messaging, and client invoicing into a single, cohesive ecosystem.

### Tech Stack
- **Backend Framework:** C# .NET 9 LTS Web API (C# 13)
- **Database:** PostgreSQL 18 accessed via `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0 (with native `numeric(18,2)` / `numeric(18,4)` column precision)
- **Web Hosting:** Unified single-port hosting (`OfficeCore.Api` serves REST API, SignalR WebSockets, and Blazor WebAssembly client files on port `5026`)
- **Real-Time Communication:** SignalR WebSockets (`BoardHub`, `AttendanceHub`, `ChatHub`)
- **Frontend Framework:** Blazor WebAssembly (Served via API fallback to `index.html`)
- **Desktop Client:** Windows WPF Client (`OfficeCore.DesktopClient`) with Edge WebView2, supporting dual-role POS-style deployment:
  - **Server Station Mode:** Manages local PostgreSQL connection, starts `OfficeCore.Api`, displays LAN IP broadcast for office workstations, streams live console output, and provides a direct local WebView2 client.
  - **Client Terminal Mode:** Lightweight workstation client that connects to the host server IP/URL over the local office network.
- **Styling & Theming:** Tailwind CSS v4 with dual-theme system (Dark default + Light mode via `.light` CSS variables)
- **Hardware Integrations:** ESP32 + Optical Fingerprint Scanner (AS608) via Web Serial API (USB) and TCP Socket Listener (Wi-Fi on port 9000)

---

## ✅ 2. What Has Been Done (Current Capabilities)

### Multi-Tenancy & Access Control
- [x] Multi-tenant workspace isolation for boards, chat, payroll, employees, and settings.
- [x] Multi-tenant Role-Based Access Control (RBAC) supporting custom organization-scoped roles (`OrganizationRole`, `OrganizationRoleClaims`).
- [x] Workspace invitation system allowing users to join organizations via secure tokens (By Link or By Email) with email verification.
- [x] Dynamic claims transformation (`OrganizationClaimsTransformation`) resolving role-scoped module permissions for the active workspace.
- [x] Password-protected organization deletion verifying Identity password before purging workspace.

### Authentication & Identity
- [x] Cookie-based secure authentication (`OfficeCoreAuth`).
- [x] Default password provisioning and forced "Must Change Password" barrier screen upon first login.
- [x] Pre-seeded demo account (`demo@officecore.com` / `Password123!`) and "Demo Corp" workspace via `DbSeeder.cs`.

### Employee Management
- [x] Complete CRUD interface for employee records scoped to the active workspace.
- [x] Distinction between "System Users" and "Employees" via an `IsEmployee` toggle.
- [x] Configuration of base pay rates, fixed weekly schedules, and assignment of workspace-specific roles.

### Attendance & Biometrics
- [x] Real-time clock-in / clock-out interface with date and employee filters.
- [x] Hybrid biometric fingerprint integration (ESP32 + AS608):
  - **Wired:** Direct browser connection over Web Serial API at 9600 baud with auto-reconnect.
  - **Wi-Fi:** Remote scanner connection via TCP client to `FingerprintDeviceService` on port 9000.
- [x] Biometric enrollment with mismatch retry loops and clean cancellation commands.
- [x] Manual attendance adjustment records with source tracking (`Device` vs `ManualAdjustment`).
- [x] Work schedule shift enforcement with break rules, auto-deduction thresholds, and overtime policies.

### Leave Management (PTO)
- [x] Employee Self-Service (ESS) leave requests with file attachments (medical certificates, proof) saved to disk (`/uploads/leaves`).
- [x] Management Self-Service (MSS) dashboard (`/admin/leave-approvals`) to review, approve, or reject employee leaves.
- [x] Admin direct leave creation override with automatic approval.
- [x] Automated integration of approved paid leaves into the Payroll Computation Engine.

### Advances & E-Loans
- [x] Employee cash advance and installment loan request submission and review.
- [x] Balance tracking and automated amortization deduction starting strictly on the next cutoff following approval.

### Payroll Engine
- [x] Automated batch generation of payroll periods (Weekly, Bi-Weekly, Monthly) across Draft, Computed, Finalized, and Paid statuses.
- [x] **Hourly Rate Mode**: Calculates pay strictly multiplied by tracked attendance hours.
- [x] **Weekly Rate Mode**: Bypasses attendance logs and calculates base pay automatically based on scheduled work days.
- [x] Statutory Philippine government deductions (SSS, PhilHealth, Pag-IBIG) with customizable cycle multipliers (Full Month 100%, Semi-Monthly 50%, Weekly 25%, None 0%).
- [x] Engine calculates dynamic additions (allowances) and deductions (taxes/benefits/loans).
- [x] Detailed Employee Payslip viewer modal (`PayslipModal.razor`).

### Kanban Project Boards
- [x] Real-time collaborative Trello-style boards using SignalR (`BoardHub`).
- [x] Role-based Board permissions (Owner, Member, Viewer).
- [x] Native HTML5 Drag-and-Drop for moving cards and reordering lists.
- [x] Board entity framework relationships (Lists, Cards, Assigned Users, Labels, Checklists, Comments, Attachments).
- [x] Multipart file upload and deletion API for card attachments (`/api/boards/cards/{id}/attachments`).

### Real-Time Chat
- [x] Floating global chat widget with organization-isolated channels and direct messaging.
- [x] SignalR WebSocket delivery with typing indicators and optimistic UI rendering.

### Settings, UI & Theming
- [x] Unified Settings page (`/settings`) consolidating Personal Profile and Organization settings.
- [x] Dual-theme engine (Dark Mode default + complete Light Mode via Tailwind CSS v4 CSS variables).
- [x] Anti-FOUC zero-flicker script in `index.html` syncing theme preferences instantly before render.
- [x] Responsive layout with desktop collapsible sidebar and mobile off-canvas drawer.

### On-Premises Desktop Hub & Deployment (POS-Style)
- [x] Startup mode selection screen allowing users to choose **Server Station** or **Client Terminal** with option to remember selection.
- [x] **Server Station Dashboard**:
  - Automatically identifies local non-virtual LAN IPv4 address (e.g., `192.168.1.X`) and generates workstation connection URL (`http://<IP>:5026`).
  - Embedded PostgreSQL connection tester and connection string configuration.
  - Background child process manager for `OfficeCore.Api` with real-time stdout/stderr log streaming.
  - Health check verification polling `http://localhost:5026` until active.
  - "Launch OfficeCore Locally" button embedding WebView2 directly within the host application.
- [x] **Client Terminal View**:
  - Clean server connection screen for client PCs to input the Server's IP address or hostname.
  - Automatic connection validation and instant routing into full-screen WebView2 workstation view.
- [x] **Collapsible Full-Screen Web Controls**:
  - Auto-hides top desktop window header when viewing the web application to prevent double-header clutter.
  - Consolidated single-row navigation bar (Dashboard / Disconnect, Reload, Switch Mode, live URL indicator).
  - Collapsible mini floating tab (`[ 🖥️ Host Controls ▾ ]` / `[ 💻 Terminal Controls ▾ ]`) minimizing toolbar to 22px to maximize screen space for the application.

---

## 🚧 3. What is Missing (Features To Build Next)

### High Priority
1. **Payroll / Payslip Exports**: Export Payroll Summaries to CSV/Excel for bank wire uploads, and generate printable/PDF payslips.
2. **Payroll Scaling**: Refactor synchronous in-memory payroll calculation to a background worker queue for enterprise scale.

### Medium Priority
3. **Department Management**: Group employees into specific "Departments" or "Teams" with assigned Department Managers.
4. **Reporting & Analytics Dashboard**: High-level visual dashboard for Admin/HR to view monthly payroll expenses, tardiness trends, and project velocity.
5. **System Notifications**: In-app notification bell and email alerts (card assignments, payslip ready, leave approval).

---

## 🔧 4. What Needs to be Fixed (Technical Debt & Known Issues)

### Backend & Architecture
1. **SignalR Connection Resiliency**: Automatic reconnection and missed event catch-up if connection drops temporarily.
2. **Strict API Authorization**: Continue auditing all controller endpoints to ensure explicit board/tenant permission checks beyond UI button visibility.

### Frontend & UI
1. **Mobile Drag & Drop**: Implement touch polyfill / touch event handlers for Kanban cards on mobile screens.
2. **Pagination / Infinite Scroll**: Server-side pagination for Attendance logs and Employee lists as datasets grow.
