# OfficeCore - Master Project Status & Architecture Overview

## 🎯 1. System Overview & Architecture
**OfficeCore** is a modern, monolithic internal company management and HR platform. It centralizes employee management, automated time-tracking, payroll generation, and project collaboration into a single, cohesive ecosystem.

### Tech Stack
- **Backend Framework:** C# .NET 10 Web API
- **Database:** PostgreSQL (accessed via Entity Framework Core)
- **Real-Time Communication:** SignalR WebSockets (used extensively for Kanban Boards)
- **Frontend Framework:** Blazor WebAssembly (Standalone/Client-Side)
- **Styling:** Custom Vanilla CSS utilizing utility classes (Tailwind methodology)
- **Hardware Integrations:** HTML5 MediaDevices API (Webcam Facial Capture), Geolocation API (GPS Tracking)

---

## ✅ 2. What Has Been Done (Current Capabilities)

### Multi-Tenancy & Access Control
- [x] Multi-tenant workspace isolation for boards, chat, payroll, employees, and settings.
- [x] Multi-tenant Role-Based Access Control (RBAC) supporting custom organization-scoped roles (`OrganizationRole`, `OrganizationRoleClaims`).
- [x] Workspace invitation system allowing users to join organizations via secure tokens (By Link or By Email) with email verification.
- [x] Claims transformation dynamically resolving role-scoped module permissions for the active workspace.

### Authentication & Identity
- [x] Cookie-based secure authentication.
- [x] Default password provisioning and forced "Must Change Password" barrier screen upon first login.
- [x] Profile management (including global user fields like names and avatar uploads).

### Employee Management
- [x] Complete CRUD interface for employee records scoped to the active workspace.
- [x] Distinction between "System Users" and "Employees" via an `IsEmployee` toggle.
- [x] Configuration of base pay rates, fixed weekly schedules, and assignment of workspace-specific roles.

### Attendance & Time Tracking
- [x] Web-based Time In / Time Out interface.
- [x] Biometric verification via Webcam Facial Capture.
- [x] Location verification via Browser GPS coordinates.
- [x] Automated parsing of attendance logs into Regular Hours vs. Overtime Hours based on the employee's fixed schedule.

### Payroll Engine
- [x] Automated batch generation of payroll periods (Weekly, Bi-Weekly, Monthly).
- [x] **Hourly Rate Mode**: Calculates pay strictly multiplied by tracked attendance hours.
- [x] **Weekly Rate Mode**: Bypasses attendance logs and calculates base pay automatically based on the number of scheduled work days for the period.
- [x] Engine calculates dynamic additions (allowances) and deductions (taxes/benefits).
- [x] Generation and viewing of detailed Employee Payslips.

### Kanban Project Boards
- [x] Real-time collaborative Trello-style boards using SignalR.
- [x] Role-based Board permissions (Owner, Member, Viewer).
- [x] Native HTML5 Drag-and-Drop for moving cards and reordering lists.
- [x] Smart debouncing for SignalR to prevent UI flickering for the active user.
- [x] Board entity framework relationships (Lists, Cards, Assigned Users, Labels, Checklists, Comments).

---

## 🚧 3. What is Missing (Features To Build Next)

### High Priority (Incomplete Systems)
1. **Board Card Attachments API**: The Kanban UI supports the concept of attachments, but there is no backend blob storage or upload API to actually save and retrieve files.
2. **Leave Management (PTO)**: Employees currently have no way to request Vacation, Sick Leave, or Unpaid Time Off. HR needs a dashboard to approve/deny these, and the Payroll Engine must automatically process approved leaves.
3. **Payroll / Payslip Exports**: Missing the ability to export Payroll Summaries to CSV/Excel for bank wire uploads, and missing PDF generation for printable payslips.

### Medium Priority (Enhancements)
4. **Department Management**: Missing the ability to group employees into specific "Departments" or "Teams" and assign Department Managers.
5. **Reporting & Analytics Dashboard**: The system lacks a high-level visual dashboard for Admin/HR to view monthly payroll expenses, tardiness trends, and project velocity.
6. **System Notifications**: Missing an in-app notification bell and email alerts (e.g., "You have been assigned to a card", "Your payslip is ready", "Your leave was approved").

---

## 🔧 4. What Needs to be Fixed (Technical Debt & Known Issues)

### Backend & Architecture
1. **Attendance Image Storage**: Facial capture images might be heavily impacting database payload sizes if stored as Base64 strings. They need to be migrated to a dedicated File Storage system (e.g., Azure Blob Storage, AWS S3, or local disk storage) and referenced by URL.
2. **SignalR Connection Resiliency**: The frontend needs robust reconnect logic. If a user loses internet briefly, they must seamlessly reconnect to the SignalR hub and fetch any missed Kanban board events without manually refreshing.
3. **Payroll Scaling**: Payroll generation is currently processed synchronously in memory. If the company scales to thousands of employees, this HTTP request will timeout. It must be refactored into a background worker queue (e.g., Hangfire) that updates a "Processing..." status.
4. **Strict API Authorization**: Ensure all endpoints strictly validate if a user actually belongs to a board before allowing edits. (Current implementation relies heavily on UI hiding buttons; backend must be bulletproof).

### Frontend & UI
1. **Mobile Drag & Drop**: Native HTML5 Drag-and-Drop does not work consistently on touch devices (iOS/Android). A touch-polyfill or custom touch event handler must be implemented for the Kanban boards to be mobile-friendly.
2. **Pagination / Infinite Scroll**: Employee lists and Attendance logs currently fetch all records at once. As data grows, this will cause significant UI lag. Server-side pagination must be implemented.
