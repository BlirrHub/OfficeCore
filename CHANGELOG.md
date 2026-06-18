# Changelog

All notable changes to this project will be documented in this file.

---

## [Unreleased]

### Added — Phase 6: Member Roles (Multi-Tenant RBAC) - [2026-06-18]
- **Isolated Roles:** Migrated Role-Based Access Control (RBAC) to be fully multi-tenant, replacing global `AppRole` with `OrganizationRole` and `OrganizationRoleClaim`.
- **Employee Mapping:** Roles are now assigned to `EmployeeRole` mapping table, correctly connecting the tenant-specific user (`Employee`) to their tenant-specific roles.
- **Dynamic Provisioning:** Automatically provisions baseline `Admin` and `Member` roles upon organization creation, assigning the creator the `Admin` role.
- **Invitations Default Role:** New users accepting invitations are automatically placed in the `Member` role.
- **Authorization Context:** Rewrote `/api/auth/me` to dynamically resolve module claims based on the user's roles within their active organization, removing the temporary "grant all" hack.
- **UI Refactoring:** Updated Role Management and User Management admin pages to seamlessly interact with tenant-scoped roles.

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
- **Employee Management**: "Add Employee" replaced with "Invite Member" in `UserManagement.razor`. A link-generator modal produces a shareable invite URL. Admins can no longer edit `FirstName`/`LastName` on employee records (those are personal global user fields).

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
