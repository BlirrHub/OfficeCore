# OfficeCore — Planning, Roadmap & Feature Pipeline

> **Purpose:** This document tracks planned features, architectural improvements, backlog tasks, and ideas for OfficeCore. Update this file whenever planning new features, scoping implementations, or brainstorming enhancements.

---

## 📌 Status Legend
- 🟡 **In Planning:** Requirements or design currently being drafted.
- 🟢 **Ready for Implementation:** Scoped and ready to be built.
- 🔵 **In Progress:** Actively under development.
- 🟣 **Backlog / Ideas:** Future considerations, awaiting prioritization.
- ✅ **Completed:** Finished and moved to [CHANGELOG.md](file:///d:/Apps/Antigravity/Projects/OfficeCore/CHANGELOG.md) & [PROJECT_STATUS.md](file:///d:/Apps/Antigravity/Projects/OfficeCore/PROJECT_STATUS.md).

---

## 🎯 1. Immediate Focus & Office Test-Run Triad

These 3 features form the **Office Test-Run Foundation** to safely operate OfficeCore on-premises in a live office environment:

| Priority | Feature / Task | Target Area | Status | Notes |
| :--- | :--- | :--- | :---: | :--- |
| **P1** | **Automated Database Backup & 1-Click Restore** | Server Station & Web API | 🟢 Ready | Automated scheduled & manual PostgreSQL dumps, safe restore wizard with pre-restore snapshot, retention policy, and download archives |
| **P1** | **Audit Trail Logging System** | Core Web API & Admin | 🟢 Ready | Immutable audit ledger tracking who created, updated, or deleted payroll, salary rates, attendance punches, leaves, and system settings |
| **P1** | **Payroll Export & Individual Payslip Generation** | Payroll Engine & Web | 🟢 Ready | Printable PDF/HTML individual employee payslips (with company branding & signature lines) and complete Payroll Run Excel/CSV export |
| **P2** | **Server Installer Silent PostgreSQL Check/Install** | Server Installer | 🟡 In Planning | Setup wizard checks if PostgreSQL is installed; if missing, runs silent embedded setup |
| **P2** | In-App Auto-Updater for Server & Client | Desktop Apps | 🟣 Backlog | Background version check against GitHub releases or API endpoint |
| **P3** | **Software Licensing & Seat Management** | Server Station & Licensing | 🟣 Backlog | Hardware fingerprinting, offline signed keys, seat tiers *(Deferred until after initial test runs)* |
| **P4** | **General Ledger & Double-Entry Accounting** | Accounting Module | 🟣 Backlog | Chart of Accounts, Journal Vouchers, Accounts Payable/Receivable, and BIR Tax Forms (Long-term vision) |

---

## 🚀 2. Feature Pipeline by Module

### 🖥️ Desktop & Native Workstations
- [ ] **Background Auto-Update Mechanism:** Seamless update notification and silent in-place updater for `OfficeCore.Client` and `OfficeCore.ServerStation`.
- [ ] **System Tray Minimization:** Option to minimize Server Station and Workstation to the Windows Notification Area / System Tray instead of taskbar.
- [ ] **LAN Auto-Discovery (mDNS / UDP Broadcast):** Allow workstation client to automatically discover the host Server Station IP on the local subnet without typing the URL.
- [ ] **Offline Cache & Resilient Sync:** Cache recent dashboard and board state locally in case of temporary LAN disconnection.

### 📋 Kanban Boards & Project Management
- [ ] **Subtasks & Checklist Progress:** Add checklists to cards with completion percentage bars on column previews.
- [ ] **Card Comments & Activity Stream:** Real-time conversation thread and audit history on individual task cards.
- [ ] **File Attachments on Cards:** Drag-and-drop document and image uploads attached directly to tasks.
- [ ] **Board Swimlanes:** Group board cards horizontally by assignee, priority, or epic/sprint.
- [ ] **Milestone & Sprint Tracking:** Group cards into time-boxed sprints with burndown metrics.

### ⏱️ Attendance, Biometrics & Field Tracking
- [ ] **Geofenced Mobile Clock-In:** Verify mobile punch coordinates are within authorized office location radiuses.
- [ ] **Facial Recognition Punch Option:** Optional browser webcam-based face recognition alongside the AS608 fingerprint scanner.
- [ ] **Shift Rotation & Roster Scheduler:** Visual calendar schedule builder for rotating night/day employee shifts.
- [ ] **Rider Route Playback:** Timeline scrubber on `Locations.razor` to replay historical coordinates for any field staff member.

### 🌴 Leave Management (PTO) & Advances
- [ ] **Leave Accrual & Balance Policies:** Configurable annual vacation/sick leave credits that accrue monthly or annually.
- [ ] **Multi-Level Approval Chains:** Route approval to designated team leads before escalating to HR.
- [ ] **Company Leave Calendar:** Organization-wide monthly calendar showing approved leaves to prevent understaffing.
- [ ] **Advance Auto-Caps:** Enforce maximum loan advance limits based on employee tenure or monthly salary ratio.

### 💰 Payroll & Compensation
- [ ] **Configurable Tax & Statutory Deductions:** Support for regional tax tiers, social security, health insurance, and provident fund formulas.
- [ ] **PDF Payslip Generation:** One-click bulk PDF payslip generator with company logo and signature lines.
- [ ] **Automated Payslip Distribution:** Send encrypted PDF payslips to employee email addresses upon payroll finalization.
- [ ] **Bank Disbursement Batch Export:** Export payroll totals into standardized CSV/Excel formats accepted by commercial banks.

### 💬 Chat & Internal Collaboration
- [ ] **Department & Project Channels:** Dedicated group chat channels alongside direct 1-on-1 messages.
- [ ] **Rich Media & File Sharing:** In-line image previews and file downloads within chat threads.
- [ ] **Read Receipts & Typing Indicators:** Live typing bubbles and delivered/seen timestamps via SignalR.
- [ ] **@Mentions & Push Notifications:** Desktop sound and visual badge alert when mentioned in chat or assigned to a task.

### 🏢 Multi-Tenancy & Administration
- [ ] **Audit Log Explorer:** Administrative ledger tracking all sensitive actions (salary edits, role changes, organization settings).
- [ ] **Custom Organization Branding:** Allow workspaces to upload custom company logos, favicon, and brand accent colors.
- [ ] **Single Sign-On (SSO):** Google Workspace and Microsoft Entra ID (Azure AD) OAuth2 login integration.

### 💾 Database Backup & Disaster Recovery System
- [ ] **One-Click Manual Backup:** Button in Server Station and Admin portal to immediately dump the PostgreSQL database into a timestamped archive (`.sql` / compressed `.dump`).
- [ ] **Scheduled Automated Backups:** Configurable recurring background schedule (Daily, Weekly, Custom cron) with automatic status logging.
- [ ] **Backup Retention Policies:** Configurable auto-prune rules to keep disk usage optimized (e.g. keep last 7 daily, 4 weekly, auto-delete older archives).
- [ ] **Custom Storage Paths & Secondary Locations:** Option to save backups to secondary hard drives, NAS shares, or external media for physical redundancy.
- [ ] **Safe Database Restore Wizard:** Interactive restore tool with pre-flight database connection validation, safety confirmation barrier, and automatic pre-restore safety snapshot.
- [ ] **Tenant-Level Data Export:** Organization admins can export their workspace's discrete records (employees, attendance logs, payroll, boards) as an encrypted or portable JSON/ZIP package.
- [ ] **Server Station Backup Health Card:** Visual dashboard indicator showing last backup timestamp, file size, storage space remaining, and success/failure alerts.

### 🔑 Software Licensing & Product Key System *(Future Concept — Not Implemented Soon)*
> **Note:** Long-term architectural consideration for commercial on-premises deployments. Explicitly postponed from near-term implementation.
- [ ] **Hardware Fingerprint Generation:** Server Station generates a unique host machine identifier based on CPU ID, motherboard UUID, or network interface MAC address.
- [ ] **Cryptographic License Verification:** Offline public/private key verification (Ed25519 / RSA signature) so servers do not require an active external internet connection to validate licenses.
- [ ] **Tiered License Capabilities:**
  - **Community / Starter Tier:** Free tier capped at a maximum employee count (e.g. up to 10 staff) with basic modules.
  - **Professional Tier:** Unlimited employees, active hardware biometric integration, advanced payroll.
  - **Enterprise Tier:** Multi-organization hosting, audit logs, priority update channel, custom branding.
- [ ] **License Management UI:** Clean registration screen in Server Station showing active tier, registered company name, expiration/renewal date, and seat utilization meters.
- [ ] **Grace Period & Non-Destructive Warnings:** Graceful warnings before expiration without abrupt lockouts or risk of business data loss.

---

## 💡 3. Quick Ideas & Brainstorming Scratchpad

> Use this section to quickly drop unrefined thoughts, user feedback, or feature ideas before formalizing them into the pipeline.

- *Idea:* Add a dark/light mode toggle shortcut directly in the desktop top bar.
- *Idea:* Export Kanban boards to Excel/CSV for client reporting.
- *Idea:* Quick-punch barcode / QR badge scanning via workstation webcam as a fallback for fingerprint sensor.
- *Idea:* Real-time hardware health indicator on Server Station showing ESP32 socket connectivity status.
