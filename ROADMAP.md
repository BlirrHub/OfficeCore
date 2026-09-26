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

## 🎯 1. Immediate Focus & Next Up

| Priority | Feature / Task | Target Area | Status | Notes |
| :--- | :--- | :--- | :---: | :--- |
| **P1** | Auto-Updater for Desktop Apps | Desktop Client / Server | 🟣 Backlog | Background version check against GitHub releases or API endpoint |
| **P1** | Card File Attachments & Comments | Kanban Boards | 🟣 Backlog | Upload attachments & rich activity feed on board cards |
| **P2** | Multi-Tier Leave Approval Workflows | Leave Management | 🟣 Backlog | Direct Supervisor -> Department Head -> HR Manager review flow |
| **P2** | Direct Payslip PDF Export & Emailing | Payroll Engine | 🟣 Backlog | Generate printable payslips & auto-email to employees |
| **P3** | Live Field Staff Path History | Field Operations | 🟣 Backlog | Replay rider/field staff location trails on the Leaflet map |

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
- [ ] **Database Backup & Restore Manager:** One-click PostgreSQL automated database backup and point-in-time restore from Server Station.

---

## 💡 3. Quick Ideas & Brainstorming Scratchpad

> Use this section to quickly drop unrefined thoughts, user feedback, or feature ideas before formalizing them into the pipeline.

- *Idea:* Add a dark/light mode toggle shortcut directly in the desktop top bar.
- *Idea:* Export Kanban boards to Excel/CSV for client reporting.
- *Idea:* Quick-punch barcode / QR badge scanning via workstation webcam as a fallback for fingerprint sensor.
- *Idea:* Real-time hardware health indicator on Server Station showing ESP32 socket connectivity status.
