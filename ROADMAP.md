# OfficeCore — Planning, Roadmap & Feature Pipeline

> **MANDATORY OPERATING DIRECTIVE FOR AI AGENTS:**
> 1. **Always check [agent.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/agent.md) and this [ROADMAP.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/ROADMAP.md)** before engaging in any ideation, scoping, architectural design, or new feature planning.
> 2. **Always update this file** whenever brainstorming, receiving user feature requests, creating backlog tasks, or adjusting priorities.
> 3. **When a feature is implemented**, update its status here to ✅ **Completed** and record detailed technical notes in [CHANGELOG.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/CHANGELOG.md).

---

## 📌 Status Legend
- 🟡 **In Planning:** Requirements or design currently being drafted.
- 🟢 **Ready for Implementation:** Scoped and ready to be built.
- 🔵 **In Progress:** Actively under development.
- 🟣 **Backlog / Ideas:** Future considerations, awaiting prioritization.
- ✅ **Completed:** Finished and documented in [CHANGELOG.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/CHANGELOG.md).

---

## 🎯 1. Immediate Focus & Office Test-Run Triad

These features form the **Office Test-Run Foundation** to safely operate OfficeCore on-premises in a live office environment:

| Priority | Feature / Task | Target Area | Status | Notes |
| :--- | :--- | :--- | :---: | :--- |
| **P1** | **Automated Database Backup & 1-Click Restore** | Server Station & Web API | ✅ Completed | Automated scheduled & manual PostgreSQL dumps, safe restore wizard with pre-restore snapshot, retention policy, download archives, and Desktop Server Station controls |
| **P1** | **Audit Trail Logging System** | Core Web API & Admin | ✅ Completed | Immutable audit ledger tracking who created, updated, or deleted payroll, salary rates, attendance punches, leaves, and system settings |
| **P1** | **Workspace Owner Visibility & Hierarchical RBAC** | Identity, Admin & Payroll | ✅ Completed | Main Admin (workspace creator) visible in directory, included in payroll when IsEmployee, with strict hierarchical RBAC protecting owner from assigned admins |
| **P1** | **Attendance & Payroll/Payslip Exporter** | Payroll Engine & Attendance | ✅ Completed | Printable PDF/HTML individual employee payslips, comprehensive payroll summary Excel/CSV, and DTR / punch summary logs |
| **P1** | **Board Access Security & Owner-Restricted Membership** | Kanban Boards | ✅ Completed | Security policy enforcing that only Workspace Owner or Admins can add/remove members; non-admin board owners cannot add members; clean UX notice |
| **P2** | **Chat Mentions & Task/Board Cross-Linking** | Chat System | ✅ Completed | Autocomplete `@user` mentions with alerts; `#task` and `#board` inline badges linking directly to cards and boards |
| **P2** | **Dashboard Assigned Task Due Date Urgency Colors** | Dashboard & Kanban | ✅ Completed | Color-coded alert badges (overdue, due today, upcoming) on dashboard task cards matching board card urgency styling |
| **P2** | **Desktop Host Navigation Controls (Back/Forward)** | Desktop Host Windows | ✅ Completed | Previous (Back) and Next (Forward) history buttons embedded in Desktop Client and Server Station top header bars |
| **P2** | **Server Installer Silent PostgreSQL Check/Install** | Server Installer | 🟡 In Planning | Setup wizard checks if PostgreSQL is installed; if missing, runs silent embedded setup |
| **P2** | **In-App Auto-Updater for Server & Client** | Desktop Apps | 🟣 Backlog | Background version check against GitHub releases or API endpoint |
| **P3** | **Software Licensing & Seat Management** | Server Station & Licensing | 🟣 Backlog | Hardware fingerprinting, offline signed keys, seat tiers *(Deferred until after initial test runs)* |
| **P4** | **General Ledger & Double-Entry Accounting** | Accounting Module | 🟣 Backlog | Chart of Accounts, Journal Vouchers, Accounts Payable/Receivable, and BIR Tax Forms (Long-term vision) |

---

## 🚀 2. Feature Pipeline by Module

### 🏢 Multi-Tenancy & Administration
- [x] **Workspace Owner Visibility & Headcount Reconciliation:** Show creator in user administration, include in payroll calculations, lock owner roles from modification.
- [x] **Hierarchical Administrator Access Control:** Main Admin can manage assigned admins; assigned admins cannot modify owner or peers.
- [x] **Default Modules Configuration:** Dashboard, My Attendance, My Leaves, Advances unconditionally active for all authenticated users.
- [x] **Audit Log Explorer:** Administrative ledger (`/admin/audit-logs`) tracking sensitive actions (salary edits, role changes, organization settings).
- [x] **Multi-Use Time-Expiring Invite Codes:** Made generic invite links unlimited use but expire after 7 days to match user expectations (like Discord invite links).
- [ ] **Simplified Invite Code UX:** Remove the full URL invite link from User Management, displaying only the clean, portable Invite Code (e.g. `OC-7X9B2K`) designed for native desktop client entry.
- [ ] **Custom Organization Branding:** Allow workspaces to upload custom company logos, favicon, and brand accent colors.
- [ ] **Single Sign-On (SSO):** Google Workspace and Microsoft Entra ID (Azure AD) OAuth2 login integration.

### 💾 Database Backup & Disaster Recovery System
- [x] **One-Click Manual Backup:** Button in Server Station and Admin portal to immediately dump PostgreSQL database into timestamped archive (`.sql` / `.dump`).
- [x] **Scheduled Automated Backups:** Configurable recurring background schedule (Daily, Weekly, Custom cron) with automatic status logging.
- [x] **Backup Retention Policies:** Configurable auto-prune rules to optimize disk usage (keep last N backups).
- [x] **Safe Database Restore Wizard:** Interactive restore tool with pre-flight database connection validation, safety confirmation barrier, and automatic pre-restore safety snapshot.
- [x] **Server Station Backup Health Card:** Visual dashboard indicator showing last backup timestamp, file size, storage space, and status alerts.
- [ ] **Custom Storage Paths & Secondary Locations:** Option to save backups to secondary hard drives, NAS shares, or external media for physical redundancy.
- [ ] **Tenant-Level Data Export:** Organization admins can export their workspace's discrete records as an encrypted or portable JSON/ZIP package.

### 💰 Payroll & Compensation
- [x] **Printable & PDF Payslip Generation:** One-click bulk and individual PDF payslip generator with company logo and signature lines.
- [x] **Payroll & Attendance Summary Exporter:** Comprehensive summary export (Excel/CSV) for payroll cutoffs, employee earnings, deductions, and attendance punch logs.
- [ ] **Configurable Tax & Statutory Deductions:** Support for regional tax tiers, social security, health insurance, and provident fund formulas.
- [ ] **Automated Payslip Distribution:** Send encrypted PDF payslips to employee email addresses upon payroll finalization.
- [ ] **Bank Disbursement Batch Export:** Export payroll totals into standardized CSV/Excel formats accepted by commercial banks.

### ⏱️ Attendance, Biometrics & Field Tracking
- [x] **Standalone My Attendance Module:** Dedicated ESS attendance page (`/my-attendance`) with digital clock, live punch status, metrics, and SignalR live updates.
- [x] **Attendance Management Consolidation:** Company-wide punch management (`/admin/attendance`) restricted to management roles.
- [x] **Attendance Log & DTR Exporter:** Export Daily Time Records (DTR), punch summaries, and tardiness reports to Excel/CSV.
- [x] **Real-Time Attendance Sync & In-Page Refresh:** Live punch reflection on `/admin/attendance` when staff punch in/out, plus an in-page `↻ Refresh` button for immediate re-fetching without browser/host reload.
- [ ] **Geofenced Mobile Clock-In:** Verify mobile punch coordinates are within authorized office location radiuses.
- [ ] **Facial Recognition Punch Option:** Optional browser webcam-based face recognition alongside the AS608 fingerprint scanner.
- [ ] **Shift Rotation & Roster Scheduler:** Visual calendar schedule builder for rotating night/day employee shifts.
- [ ] **Rider Route Playback:** Timeline scrubber on `Locations.razor` to replay historical coordinates for any field staff member.

### 🌴 Leave Management (PTO) & Advances / Ledger
- [x] **Real-Time Live Ledger & Advances Sync:** Live SignalR event broadcasting when employees file Cash Advance (CA) or eLoan requests so admin Ledger updates instantly; live update when admin approves/disburses.
- [x] **Real-Time Leave Approvals & My Leaves Sync:** Live SignalR update when leave requests are filed (notifying admins) and when approved/rejected (updating employee balance and status live).
- [x] **Dedicated In-Page Refresh Buttons:** Add consistent `↻ Refresh` button controls to Ledger, Advances, Leave Approvals, and My Leaves headers for rapid single-click data re-fetching.
- [ ] **Leave Accrual & Balance Policies:** Configurable annual vacation/sick leave credits that accrue monthly or annually.
- [ ] **Multi-Level Approval Chains:** Route approval to designated team leads before escalating to HR.
- [ ] **Company Leave Calendar:** Organization-wide monthly calendar showing approved leaves to prevent understaffing.
- [ ] **Advance Auto-Caps:** Enforce maximum loan advance limits based on employee tenure or monthly salary ratio.

### 📋 Kanban Boards & Project Management
- [x] **Boards as Default Core Module:** Available for team project collaboration across workspaces.
- [x] **Board Member Provisioning & Visibility Alignment:** Enforced Workspace Owner and Admin exclusivity for board member addition/removal, with clean explanatory notice for regular board owners and secured API endpoints.
- [ ] **Fine-Grained Board Access Controls & Roles:** Granular permission matrix defining who can add members, who can delete/archive the board, who can manage columns, and board-level admin privileges.
- [x] **Subtasks & Checklist Progress:** Add checklists to cards with completion percentage bars on column previews.
- [x] **Card Comments & Activity Stream:** Real-time conversation thread and audit history on individual task cards.
- [x] **File Attachments on Cards:** Drag-and-drop document and image uploads attached directly to tasks.
- [ ] **Board Swimlanes:** Group board cards horizontally by assignee, priority, or epic/sprint.
- [ ] **Milestone & Sprint Tracking:** Group cards into time-boxed sprints with burndown metrics.

### 📊 Dashboard & Work Overview
- [x] **Assigned Task Due Date Alert Highlighting:** Color-coded urgency styling (e.g. red for overdue, amber for due today/tomorrow, emerald for upcoming) on "My Tasks" cards in the main dashboard matching the board card tags.
- [ ] **Quick Punch Clock Widget:** Quick clock-in/out button directly accessible on the main dashboard header without navigating to My Attendance.

### 🖥️ Desktop & Native Workstations
- [x] **Two-Tier Native Application Architecture:** Host Server Station (`OfficeCore.Desktop.Server`) and Client Terminal (`OfficeCore.Desktop.Client`).
- [x] **Standalone 1-Click Installers:** `publish-installers.ps1` script generating `Setup-OfficeCore-Server.exe` and `Setup-OfficeCore-Client.exe`.
- [x] **Host Window Navigation Controls:** Add browser-style Previous (Back) and Next (Forward) navigation buttons to the native desktop host header bar for quick page history traversal.
- [ ] **Background Auto-Update Mechanism:** Seamless update notification and silent in-place updater for client and server.
- [ ] **System Tray Minimization:** Option to minimize Server Station and Workstation to the Windows Notification Area / System Tray instead of taskbar.
- [ ] **LAN Auto-Discovery (mDNS / UDP Broadcast):** Allow workstation client to automatically discover host Server Station IP without typing URL.
- [ ] **Offline Cache & Resilient Sync:** Cache recent dashboard and board state locally in case of temporary LAN disconnection.

### 💬 Chat & Internal Collaboration
- [x] **Multi-Line Auto-Expanding Chat Input Box:** Replaced single-line horizontal-scrolling text input with an auto-expanding `<textarea>` supporting word-wrapping, multi-line display (height up to ~5 lines), <kbd>Enter</kbd> to send, and <kbd>Shift</kbd>+<kbd>Enter</kbd> for line breaks.
- [x] **Real-Time Member Discovery on Join:** Broadcast a SignalR `MemberJoined` event to all active chat clients when a new user registers or joins the workspace, immediately adding them to existing online users' direct message list without requiring a refresh.
- [x] **Group User @Mentions:** Tag and notify specific users in group discussions using `@username` autocomplete with visual highlights and desktop alerts.
- [x] **Board & Task References in Chat:** Link boards and cards directly in conversations using `#task` or `#board` pills that link directly to the modal or board view.
- [ ] **Department & Project Channels:** Dedicated group chat channels alongside direct 1-on-1 messages.
- [ ] **Rich Media & File Sharing:** In-line image previews and file downloads within chat threads.
- [ ] **Read Receipts & Typing Indicators:** Live typing bubbles and delivered/seen timestamps via SignalR.

### 🔔 Notification Center
- [x] **Click-Outside-to-Dismiss:** Automatically close the notification dropdown/drawer when clicking anywhere outside the notification panel.
- [x] **Expanded Notification Triggers:** Add system notifications for pending leave requests, leave approvals/rejections, loan/CA requests, disbursements, card assignments, chat @mentions, and task due date alerts.

### 🔑 Software Licensing & Product Key System *(Future Concept — Deferred)*
> **Note:** Long-term architectural consideration for commercial on-premises deployments. Explicitly postponed from near-term implementation.
- [ ] **Hardware Fingerprint Generation:** Server Station generates a unique host machine identifier.
- [ ] **Cryptographic License Verification:** Offline public/private key verification (Ed25519 / RSA signature).
- [ ] **Tiered License Capabilities:** Community / Starter, Professional, Enterprise.
- [ ] **License Management UI:** Clean registration screen in Server Station showing tier and seat utilization.
- [ ] **Grace Period & Non-Destructive Warnings:** Graceful warnings before expiration without abrupt lockouts.

---

## 💡 3. Quick Ideas & Brainstorming Scratchpad

> Use this section to quickly drop unrefined thoughts, user feedback, or feature ideas during ideation sessions before formalizing them into the pipeline.

- *Idea:* Add a dark/light mode toggle shortcut directly in the desktop top bar.
- *Idea:* Export Kanban boards to Excel/CSV for client reporting.
- *Idea:* Quick-punch barcode / QR badge scanning via workstation webcam as a fallback for fingerprint sensor.
- *Idea:* Real-time hardware health indicator on Server Station showing ESP32 socket connectivity status.
