# Changelog

All notable changes to this project will be documented in this file.

---

## [Unreleased]

### Changed — [2026-06-22]
- **Boards Tenant Isolation & Scoping:**
  - Fixed board lists, cards, comments, checklists, checklist items, and attachments to correctly associate the active `OrganizationId` upon creation.
  - Fixed Board member addition to resolve and store the target workspace-scoped `Employee.Id` rather than the global `ApplicationUser.Id`.
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
  - Migrated `WorkSchedules`, `UserManagement`, `RoleManagement`, `Workspaces`, `Join`, and `Profile` components to utilize the new Toast system.
  - Standardized delete actions (`WorkSchedules`, `RoleManagement`) by adding confirmation modals prior to execution.

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
