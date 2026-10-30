# Changelog

All notable changes to this project will be documented in this file.

---

## [Unreleased]
### Fixed & Tested — [2026-10-30] (Payroll and Notification Regression Coverage)

- Added a separate API service/component test project exercising actual payroll, tenant membership and notification group routing with isolated EF in-memory stores; no existing database or hardware is accessed.
- Reproduced seven failing regression cases before fixes: fixed-rate weekday boundaries, leave spanning a cutoff, foreign workspace notification cookies and inactive notification memberships.
- Corrected payroll to use office calendar days for fixed-rate schedules and leave clipping, and UTC-midnight date labels for leave/overtime queries; attendance remains filtered by actual UTC instants. Added an optional timezone constructor argument for deterministic tests while retaining the server-local default.
- NotificationHub now validates active membership before subscribing to a selected workspace and aborts unauthorized connections; missing selection can fall back only to active membership.
- Expanded coverage to midnight attendance, positive/negative UTC offsets, breaks, overtime, incomplete punches, deduction proration, remaining advance balances, tenant isolation, recomputation and locked payroll. All 26 API and 14 existing authorization/deployment tests passed.
- Documented test execution and limits in tests/OfficeCore.Api.Tests/README.md. PostgreSQL/browser/hardware/recovery testing and the existing NU1903 dependency advisory remain outstanding.

### Fixed — [2026-10-30] (Branch Release Isolation & Synchronization)

- Embedded the branch deployment database in Server Station assembly metadata for Debug and Release, including installers and Git worktrees.
- Kept production configuration compatibility while isolating each development profile's saved settings; custom configured database names are preserved and other profiles' reserved database names are rejected.
- Restricted development demo seeding to its dedicated database before schema migrations; removed production demo-login buttons and credentials on acba and aligned its source configuration with production.
- Synchronized shared authorization foundation, assigned role deletion, role editor and chat popup fixes into development without adding ACBA hardware modules.
- Documented branch promotion, validation and release expectations in BRANCHING.md.
- Validation: Release solution builds and all 14 authorization/deployment tests passed on each maintained branch; verified database defaults from compiled Server Station assemblies. Existing NU1903 advisory for Microsoft.AspNetCore.Components.WebAssembly.Server 9.0.0 remains open; no live database, hardware or installer smoke test was performed.

### UI & UX Polish — [2026-10-14] (Role Modal Overhaul & Chat Dropdown Positioning)

- **Role Management Modal Redesign & Dark Mode Fix**:
  - Eliminated hardcoded `bg-white` and `dark:` overrides that caused the modal to render bright white in dark mode. Modal now uses canonical dark-first surface tokens (`bg-zinc-950`, `bg-zinc-900/60`, `border-zinc-800`, `text-white`), honoring the system theme seamlessly.
  - Expanded modal layout from cramped 512px (`max-w-lg`) to a spacious 768px (`max-w-3xl`) container.
  - Replaced the inner nested 30vh scroll container with a single, smooth `overflow-y-auto custom-scrollbar` modal body, eliminating double scrollbars and cutoffs.
  - Applied `items-start` alignment across module cards so expanding fine-grained action checklists no longer stretches adjacent cards or creates awkward dark empty spaces.
  - Organized modules by category (`Finance`, `Administration`, `Main`) with clear category dividers, permission count badges (`All N` or `X/Y active`), chevron toggles, and global "Select All" / "Clear All" convenience actions.
  - Refined role identity fields, descriptive toggle for "Show as Primary Title", and theme-safe Delete Confirmation dialog.
- **Global Chat Widget Dropdown & Reaction Picker Fixes**:
  - Fixed popup direction:
    - Sent messages (`isMine`): 3-dots action dropdown and emoji reaction capsule open to the **left** (`right-0`), expanding into spacious open room and pointing to the button without obscuring message bubbles or overflowing right.
    - Received messages (`!isMine`): 3-dots action dropdown and emoji reaction capsule open to the **right** (`left-0`), expanding away from the sender bubble.
  - Aligned action menus and emoji reaction capsule directly **on top / floating above** (`bottom-full mb-2` or `top-full mt-2` if near top edge) matching Facebook Messenger design.
  - Updated reaction emojis to canonical Messenger set: `❤️`, `😆`, `😮`, `😢`, `😡`, `👍` with enlarged interactive pills and micro-scale animations.
  - Removed faulty horizontal JS bounding calculation in favor of deterministic `isMine` layout with dynamic vertical boundary detection (`getChatMenuPlacement`).

### Security & Configuration — [2026-10-14] (Local Secrets & Visual Studio Gitignore)

- Added standard Visual Studio `.gitignore` definitions alongside OfficeCore rules, ensuring temporary files, build outputs, and local developer configs remain untracked.
- Added support for gitignored `appsettings.Local.json` and `appsettings.{Environment}.Local.json` in API configuration for safe local credentials.
- Added optional production SMTP configuration fields in Desktop Server Station settings (`ServerConfig`) injected as environment variables upon API launch.
- Blanked plaintext SMTP application password in `appsettings.json` to prevent credential exposure.

### Added & Security — [2026-10-10] (`acba-dev` Action-Level RBAC Foundation)

- Added a canonical action-permission catalog covering role lifecycle, employee role assignment, audit viewing/export, payroll operations, attendance adjustment, approvals, administration, and board membership.
- Roles now persist explicit `Permission` claims alongside module navigation claims; existing module-only roles remain compatible through deterministic module-to-permission expansion.
- Added action-level authorization policies to API and Blazor clients, with workspace owners automatically receiving all canonical permissions.
- Added granular permission controls beneath each selected module in Role Management.
- Enforced specific permissions for role viewing/creation/editing/deletion, Audit Trail viewing/export, and board-member administration.
- Added audit records for role creation, permission changes, module changes, and deletion.
- Added permission-catalog validation and legacy-expansion tests.

### Fixed — [2026-10-10] (`acba-dev` Assigned Role Deletion)

- Made custom role deletion atomically remove employee-role assignments before deleting the role, allowing assigned roles to be removed without foreign-key failures or manual database cleanup.
- Updated the confirmation dialog to warn that affected employees lose access from the deleted role and surfaced API error messages instead of a generic failure toast.

### Fixed — [2026-10-10] (`acba-dev` Role Editing and Audit Trail Access)

- Fixed role updates failing with `DbUpdateConcurrencyException` by replacing module claims with a transactional database-side delete and insert.
- Aligned Audit Trail API authorization with the shared `AuditLogs` module policy, allowing custom roles granted Audit Trail access instead of requiring hard-coded `Admin` or `Owner` role names.
- Validated the active organization membership for Audit Trail list, summary, and export operations.

### Added — [2026-10-10] (`acba-dev` Quick Demo Login)

- Added Admin Demo and Employee Demo buttons to the login page for one-click ACBA development access.
- Connected the buttons to the seeded `demo@officecore.com` owner and `adrian@officecore.com` standard employee accounts using the development-only demo password.

### Fixed & Security Hardening — [2026-10-10] (RBAC Foundation and Tenant Authorization)

- Added the shared `OfficeCore.Shared` authorization catalog so API policies, Blazor policies, default access, owner access, role management, and seed data consume one canonical module definition.
- Prevented inactive employees from resolving a tenant context, selecting a workspace, receiving transformed authorization claims, or appearing authenticated through `/api/auth/me` for that workspace.
- Closed role-escalation paths by limiting role creation, modification, deletion, and employee role assignment to the immutable workspace owner relationship.
- Added validation for selected role names and module identifiers, rejecting unknown grants instead of silently accepting or dropping them.
- Aligned organization update and logo authorization with the active validated tenant and owner/module permissions; restricted workspace deletion to the workspace owner.
- Consolidated role detail and module changes into a single atomic API operation and removed the role-management UI's per-role module request pattern.
- Protected assigned roles from deletion and added duplicate role-name checks within a workspace.
- Added the first automated authorization test project with module catalog, normalization, duplicate-removal, and invalid-grant coverage.

### Fixed & Security Hardening — [2026-10-06] (Core Stabilization, Multi-Tenancy Scoping, LAN-Safe CORS & Branch Separation)

- **Dedicated Database for `acba-dev` & Desktop Server Auto-Detection (`officecoreacbadevdb`):**
  - Provisioned a dedicated PostgreSQL database `officecoreacbadevdb` specifically for the `acba-dev` branch with complete EF Core migrations and simulated seed employees.
  - Updated `ServerConfigService.cs` in `OfficeCore.Desktop.Server` with branch auto-detection so that launching the Desktop Server Station on `acba-dev` automatically targets `officecoreacbadevdb`, matching the behavior of `development` (`officecoredevdb`) and production (`OfficeCoreDb`).
  - Updated `appsettings.json` and `appsettings.Development.json` on `acba-dev` to point to `officecoreacbadevdb`.

- **Branch Separation & Test Seed Data Removal (`acba` vs `acba-dev`):**
  - Created new branch `acba-dev` dedicated to development and feature testing, keeping the demo organization and the 13 simulated employees safely seeded without truncating on reboot.
  - Purged all demo/test seed data from the primary `acba` branch (`DbSeeder.cs`), ensuring new deployments start with a clean migration baseline and default Identity roles rather than hardcoded mock accounts.
  - Removed destructive `TRUNCATE TABLE ... RESTART IDENTITY CASCADE` SQL block entirely from both branches, protecting PostgreSQL production data from being wiped on server or process restart.

- **Cross-Tenant Data Scoping in Payroll Engine (`PayrollComputationService.cs`):**
  - Added strict `OrganizationId == period.OrganizationId` filters to `AttendanceRecords`, `LeaveRequests`, and `AdvanceRequests` queries during payroll calculation runs, stopping cross-organization database bleeds.

- **Tenant Membership Validation & Spoofing Prevention (`TenantService.cs`, `WorkScheduleController.cs`, `OrganizationsController.cs`):**
  - Added `GetValidatedOrganizationIdAsync()` in `TenantService` to ensure requests only resolve organizations where the authenticated user is an active employee or workspace creator.
  - Protected `GetWorkSchedules`, `GetWorkSchedule`, and `GetCurrentOrganization` against cookie spoofing.

- **LAN-Safe CORS Policy for Multi-Device Network Access (`Program.cs`):**
  - Replaced wildcard `.SetIsOriginAllowed(_ => true)` with a dynamic private network validator that permits `localhost`, private RFC 1918 subnets (`192.168.*`, `10.*`, `172.16-31.*`), and `.local` mDNS domains.
  - Guarantees seamless connectivity for office workstation terminals and mobile devices across the local subnet while blocking untrusted external origins from issuing credentialed requests.

- **Identity & Authentication Hardening (`UserController.cs`, `AuthController.cs`):**
  - Enforced `MustChangePassword = true` for new employees created with the default password.
  - Removed `debugCode` from `SendOtp` API responses, preventing verification code leakage in network payloads.

- **Timezone Punch Alignment (`AttendanceController.cs`):**
  - Updated attendance queries in `GetMyAttendance`, `Punch`, and `LogScanEvent` to calculate daily boundaries relative to the local host/organization UTC offset rather than UTC midnight, preventing morning punches before 8:00 AM (in UTC+8) from being omitted or misattributed.

- **High-Performance Composite Query Indexes (`AppDbContext.cs`):**
  - Added composite indexes on `AttendanceRecords(OrganizationId, EmployeeId, Timestamp)`, `AttendanceRecords(OrganizationId, Timestamp)`, `Payslips(OrganizationId, PayrollPeriodId)`, and `LeaveRequests(OrganizationId, EmployeeId, Status)`.

### Enhanced & Unified — [2026-10-05] (UI Component Library Standardization: Empty State Design & Icon-Only Header Refresh Buttons)

- **Standardized Empty State Component (`EmptyState.razor`):**
  - Upgraded `EmptyState.razor` to serve as the unified empty state design system component representing the sleek `Boards` aesthetic across the entire app.
  - Replaced the previous `size-14` rounded square container with a prominent `size-20` circular container (`bg-zinc-900 rounded-full flex items-center justify-center border border-zinc-800/80 shadow-inner`) and `size-10` icons.
  - Aligned typography to `text-xl font-bold text-white mb-2` for the title and `text-sm text-zinc-400 max-w-sm mb-6 text-center` for description.
  - Added built-in parameters `ActionText`, `OnAction`, and `ActionIcon` rendering the subtle secondary dark button style (`inline-flex items-center gap-2 rounded-lg bg-white/10 hover:bg-white/20 px-4 py-2 text-sm font-medium text-white transition-all cursor-pointer shadow-sm`) instead of loud solid primary fuchsia buttons.
  - Added `Card` parameter (`bool Card = true`) wrapping in `bg-zinc-950 border border-zinc-800 rounded-2xl py-20 px-4 text-center` by default, or allowing `Card="false"` for seamless embedding inside existing table or card containers without double borders.

- **Icon-Only Header Refresh Button Alignment (`Boards.razor`, and Workspace-Wide Pages):**
  - Updated `Boards.razor` header refresh button from text (`Refresh`) to the compact icon-only refresh button (`p-2 text-zinc-400 hover:text-white bg-zinc-900 border border-zinc-800 rounded-lg transition-colors cursor-pointer` with spinning SVG during loading), matching `MyLeaves` and `Advances`.
  - Added consistent icon-only refresh buttons to `RoleManagement.razor`, `Clients.razor`, `Payroll/PayrollList.razor`, `UserManagement.razor`, `Attendance.razor`, `MyAttendance.razor`, `WorkSchedules.razor`, and `TimeOff.razor`.

- **Comprehensive Workspace Empty State Audit & Adoption:**
  - `MyLeaves.razor`: Removed outer `<AppCard>` wrapper and adopted standardized `<EmptyState>` with `ActionText="Request Leave"` and subtle secondary button styling.
  - `Advances.razor`: Removed outer `<AppCard>` wrapper and adopted standardized `<EmptyState>` with `ActionText="Request Now"` and subtle secondary button styling.
  - `RoleManagement.razor`: Resolved empty void screen when no roles exist by displaying `<EmptyState Title="No roles yet" Description="..." ActionText="Create a Role" OnAction="..." />`.
  - `Clients.razor`: Replaced ad-hoc empty state with `<EmptyState Card="false">` for both empty and filtered states.
  - `Payroll/PayrollList.razor`: Replaced custom empty cards with `<EmptyState>` for empty and no-match states.
  - `UserManagement.razor`: Replaced plain italic text with `<EmptyState Card="false" Title="No employees found" ... />`.
  - `Attendance.razor` & `MyAttendance.razor`: Replaced plain italic text with `<EmptyState Card="false">` for workspace records and personal attendance history.
  - `LeaveApprovals.razor`: Standardized filtered empty state with `<EmptyState Card="false">`.
  - `Ledger.razor`: Standardized approvals and expenses empty states with `<EmptyState>`.
  - `WorkSchedules.razor`: Standardized unselected schedule state with `<EmptyState Card="false">`.
  - `TimeOff.razor`: Standardized empty leave policies and unselected calendar states with `<EmptyState Card="false">`.
  - `Invoices.razor`: Replaced "under construction" plain box with a polished `<EmptyState>` for module in development.

### Fixed — [2026-10-05] (Workspace Deletion Foreign Key Constraint Violation on BoardCards and Chat)

- **Workspace Deletion Cascade Resolution (`OrganizationsController.cs`, `AppDbContext.cs`, Migration):**
  - Resolved `PostgresException 23503` where deleting a workspace failed with foreign key violation `FK_BoardCards_Employees_AssignedToUserId` when referencing employees being deleted in the workspace.
  - Updated `DeleteOrganization` in `OrganizationsController.cs` to execute inside a database transaction: explicitly nulls out `BoardCards.AssignedToUserId` for the target workspace and deletes workspace-scoped chat messages, reactions, and room memberships prior to removing the organization, preventing constraint conflicts with `Employees`.
  - Configured `BoardCard.AssignedToUser` with `DeleteBehavior.SetNull` and `ChatRoomMember.User` with `DeleteBehavior.Cascade` in `AppDbContext.cs`.
  - Created EF Core migration `FixBoardCardAndChatRoomMemberCascade` updating `FK_BoardCards_Employees_AssignedToUserId` to `ON DELETE SET NULL` and `FK_ChatRoomMembers_Employees_UserId` to `ON DELETE CASCADE`.

### Enhanced & Fixed — [2026-10-05] (Admin Leave Approvals & Ledger Advance Cancellation Workflow; Zero Hard Deletes)

- **Leave Approvals Admin Cancellation Workflow (`LeaveApprovals.razor`, `LeaveRequestController.cs`):**
  - Removed all hard "Delete" actions and modals from Admin Leave Approvals, ensuring full historical preservation of leave records.
  - Replaced the action with a "Cancel" action: administrators can now cancel pending (`Status == 0`) or previously approved (`Status == 1`, including manually admin-created leaves) leave requests.
  - Replaced the "Delete Leave" modal with a "Cancel Leave" confirmation dialog ("Go Back" and "Yes, Cancel").
  - Hidden cancel actions for already finalized (`Rejected` or `Cancelled`) records so they remain immutable in historical view.
  - Updated `UpdateStatus` in `LeaveRequestController.cs` to accurately record cancellation in the audit trail, broadcast live SignalR updates, and send a targeted `"Leave Request Cancelled"` notification to the affected employee.

- **Ledger Advance / Loan Admin Cancellation Workflow (`Ledger.razor`, `AdvanceRequestController.cs`):**
  - Removed all "Delete" buttons and hard-deletion confirmation modals from the Ledger Advance Approvals table.
  - Implemented `PUT /api/advancerequest/{id}/admin-cancel` in `AdvanceRequestController.cs` (protected by `RequireLedger` policy), transitioning advance/loan records to `AdvanceStatus.Cancelled`, logging audit events, and notifying the employee.
  - Added an admin "Cancel" button on Ledger rows for `Pending` and `Approved` advances with a "Cancel Request" confirmation modal, preserving advance/loan history.

### Fixed & Enhanced — [2026-10-05] (Leave Request Status Typing & Workspace Selector Logo Display)

- **Leave Status Display & Projection Alignment (`MyLeaves.razor`, `LeaveRequestController.cs`):**
  - Updated `GetMyRequests` in `LeaveRequestController.cs` to explicitly project DTO properties (`Id`, `Type`, `StartDate`, `EndDate`, `Reason`, `AttachmentPath`, `Status`, `CreatedAt`, `ActionTakenAt`) matching `GetAllRequests` projection structure.
  - Defined explicit `LeaveRequestStatus` enum in `MyLeaves.razor` (`Pending = 0`, `Approved = 1`, `Rejected = 2`, `Cancelled = 3`) and updated `LeaveRequestDto` to strongly type `Status`, ensuring cancelled leave requests reliably evaluate to `Cancelled` rather than falling into incorrect branch evaluations.
  - Added `Cancelled` pill badge styling in `NotificationCenter.razor`.

- **Workspace Selector Logo Display (`MainLayout.razor`):**
  - Updated `OrganizationDto` in `MainLayout.razor` to include `LogoUrl`.
  - Updated the workspace switcher header trigger button and dropdown list items to check for `LogoUrl` and render `<img src="@(MediaUrlHelper.Resolve(..., Configuration))" ... />` when configured, falling back gracefully to uppercase initials avatars when no logo is set.

### Enhanced & Fixed — [2026-10-05] (Leave Request Cancellation Confirmation & History Preservation; Chat Action Menu Cleanup)

- **Leave Request Cancellation Confirmation & History Retention (`MyLeaves.razor`, `LeaveRequestController.cs`, `LeaveRequest.cs`):**
  - Added a confirmation modal popup ("Cancel Request" with "Go Back" and "Yes, Cancel" buttons) in `MyLeaves.razor` before cancelling a pending leave request, matching the UX pattern in Advances & Loans.
  - Added `Cancelled` status (`3`) to the `LeaveRequestStatus` enum in `LeaveRequest.cs`.
  - Updated `CancelRequest` in `LeaveRequestController.cs` to set `Status = LeaveRequestStatus.Cancelled` and set `ActionTakenAt = DateTime.UtcNow` instead of hard-deleting the record, preserving leave history for employees and administrators.
  - Updated `MyLeaves.razor` to render a `Cancelled` status badge with resolution timestamp and retain cancelled leaves in the history table.
  - Updated `LeaveApprovals.razor` to support viewing and filtering by `Cancelled` status with matching badge styling and audit details.
  - Updated `DashboardController.cs` to render `Cancelled` leaves with appropriate `zinc` badge styling in personal and workspace recent activity feeds.
  - Updated `NotificationsController.cs` to exclude self-cancelled leave and advance requests from resolution notifications.

- **Chat Action Menu Cleanup (`GlobalChatWidget.razor`):**
  - Removed the `Remove` (Unsend) action button from the sent message "More Actions" dropdown menu in `GlobalChatWidget.razor` per user request, keeping only `Forward` and `Pin` / `Unpin`.

### Fixed — [2026-10-05] (Messenger-Style Floating Action Menus & Long Received Message Layout Fix)

- **Messenger-Style Floating Message Dropdown Menus (`GlobalChatWidget.razor`):**
  - Redesigned the "More Actions" dropdown menu (`Forward`, `Pin`/`Unpin`, `Remove`/`Report`) to float above message action buttons with an authentic Messenger-inspired downward pointer beak (`-bottom-1.5 size-3 rotate-45 border-b border-r`) aimed directly at the 3-dots button.
  - Added smart vertical fallback (`top-full mt-2.5` with upward pointer beak) for top-most messages (`i <= 1`) to prevent collision with the chat header.
  - Fixed horizontal clipping by directing menus inward toward chat canvas space: sent messages (`isMine`) use `left-0` with beak on the left extending rightward, while received messages (`!isMine`) use `right-0` with beak on the right extending leftward, guaranteeing zero container overflow.
  - Added `Remove` (Unsend) action for sent messages and `Report` action for received messages to match Messenger action specifications.

- **Long Received Message Bubble Containment & Text Wrapping (`GlobalChatWidget.razor`):**
  - Resolved an issue where long received messages (e.g. task mentions like "Review employee attendance logs") expanded horizontally to the full container width, cramming the action pill against the right border.
  - Implemented responsive max-width containment on `chat-msg-bubble-wrap` (`max-w-[calc(100%-80px)] sm:max-w-[230px]`), ensuring chat bubbles wrap text cleanly and reserve at least 80px of horizontal breathing room for the action buttons pill on any screen size.
  - Updated task and board mention links in `FormatMessageContent` to use `inline-flex items-start gap-1.5 flex-wrap break-words`, ensuring long mention titles wrap naturally across lines without forcing unbreakable single-line expansion.

### Fixed — [2026-10-05] (Chat Message Action Buttons Overlap & Dropdown Menu Stacking Fix)

- **Chat Message Action Pill Overlap Resolution (`GlobalChatWidget.razor`):**
  - Resolved an issue where attachment cards on sent messages overlapped and partially covered the hover action toolbar pill (3 dots, reply, reaction smiley).
  - Increased `chat-msg-bubble-group` max width to `max-w-[92%] sm:max-w-[88%]` and added `max-w-full` containment to prevent card flex overflow.
  - Adjusted attachment card maximum width to a compact `max-w-52` (208px) with `truncate` on the subtitle, ensuring attachment cards fit cleanly alongside the action pill without clipping or crowding.
  - Elevated `chat-msg-actions` to `z-20` relative layer so action buttons are never obscured behind message bubbles.

- **Message More Actions Dropdown Stacking Context & Alignment Fix (`GlobalChatWidget.razor`):**
  - Resolved an issue where opening the "More Actions" dropdown (Forward, Pin) caused subsequent message bubbles in the chat to render on top of the dropdown menu.
  - Applied dynamic stacking context elevation (`z-30`) to the active message row (`chat-msg-row`) whenever its action menu or reaction picker is toggled open, guaranteeing the menu always renders above all following messages.
  - Corrected horizontal menu alignment direction: aligned dropdown to `right-0` for sent messages (expanding leftward into open chat space rather than over the message bubble) and `left-0` for received messages (expanding rightward into open chat space).

### Enhanced & Fixed — [2026-10-05] (Workspace Recent Activity Replacement & Personal Status Activity Isolation)

- **Isolation of Personal Activity for Admins/Owners (`DashboardController.cs`, `Dashboard.razor`):**
  - Updated `DashboardController.GetStats` to strictly isolate `PersonalRecentActivities` to the authenticated caller's employee profile (`employee.Id`), querying their own attendance punches, leave requests, and advance/loan requests.
  - Resolved an issue where Workspace Owners and Admins viewing "My Personal Status" (ESS tab) saw organization-wide activities from other employees under "My Recent Activity".
  - "My Recent Activity" in the Personal tab now consistently displays only the user's direct actions (e.g., "You clocked IN", "You requested Leave", "You requested Cash Advance").

- **Dedicated "Workspace Recent Activity" Card in Team Operations (`DashboardController.cs`, `Dashboard.razor`):**
  - Removed the static "Workspace Engine" card from the right column of the "Team Operations" management tab.
  - Replaced it with a real-time "Workspace Recent Activity" card that aggregates company-wide events (staff clock-ins/outs, leave requests, cash advance/loan requests) with role-colored badges, category icons, timestamps, and direct navigation links.
  - Cleaned up the "Task & Sprint Velocity" card on the left by removing the awkwardly nested activity feed and adding dedicated summary stat boxes for To Do, In Progress, and Completed task counts.

### Enhanced — [2026-10-05] (Custom Date Support in Ledger Create Advance / E-Loan)

- **Date Field for Admin Advance / E-Loan Creation (`Ledger.razor`, `AdvanceRequestController.cs`):**
  - Added a dedicated Date input field (`<InputDate @bind-Value="_newAdvance.RequestDate" />`) inside the "Create Advance or Loan" modal on the Ledger page, defaulting to the current date (`DateTime.Today`).
  - Extended `AdminCreateAdvanceRequestDto` with `public DateTime? RequestDate { get; set; }` in both `Ledger.razor` and `AdvanceRequestController.cs`.
  - Updated `AdminCreateRequest` endpoint in `AdvanceRequestController.cs` to respect the specified `RequestDate` (handling UTC conversion safely for PostgreSQL/Npgsql) instead of hardcoding `DateTime.UtcNow`.
  - Updated the Ledger table to display the customized request date for historical and record-keeping accuracy.

### Enhanced & Fixed — [2026-10-05] (Chat Pin & Reaction Emotes Real-Time Sync & Toast Cleanup)

- **Persistent & Real-Time Synchronized Chat Message Pins (`ChatMessage.cs`, `AppDbContext.cs`, `ChatController.cs`, `GlobalChatWidget.razor`):**
  - Upgraded `ChatMessage` entity model with `IsPinned`, `PinnedAt`, and `PinnedById` fields, fully mapped to PostgreSQL via EF Core migration `20261005003750_AddChatPinsAndReactions`.
  - Replaced client-only browser `localStorage` pin storage with server-side database persistence and live SignalR sync via `MessagePinUpdated(roomId, messageId, isPinned)` broadcast to all room members.
  - Pinned state is now shared across all users and persists across devices and page refreshes.
  - Enhanced `GetMessages` query to automatically include all active pinned messages in the room, guaranteeing pinned banners and drawer lists always have access to top-pinned content.

- **Persistent & Real-Time Synchronized Emoji Reactions (`ChatMessageReaction.cs`, `AppDbContext.cs`, `ChatController.cs`, `GlobalChatWidget.razor`):**
  - Created `ChatMessageReaction` entity with tenant-scoping, user foreign key, and cascade deletion when parent messages are unsent.
  - Added `POST /api/chat/messages/{id}/react` endpoint with support for toggling reactions (adding, changing emoji, or removing reaction on repeat click).
  - Wired real-time SignalR `MessageReactionUpdated(roomId, messageId, userId, emoji)` broadcasts so all members viewing the conversation see reaction pills and counters update instantaneously.
  - Hydrated message reactions directly in `GetMessages` API endpoint and `OpenRoom` initialization.

- **Elimination of Unnecessary & Disruptive Toasts (`GlobalChatWidget.razor`, `NotificationCenter.razor`):**
  - Removed intrusive toast popups for micro-actions that already have immediate visual feedback in the UI:
    - Removed "Message pinned" and "Message unpinned" popups.
    - Removed "Message unsent." toast (message disappearing immediately communicates the action).
    - Removed unused `ToastService` injection from `NotificationCenter.razor`.

### Fixed & Enhanced — [2026-10-05] (Chat Input Textarea Word-Wrap, Flex Containment & Smooth 1-to-3 Line Auto-Expansion)

- **Chat Input Width Containment & Word-Wrap (`GlobalChatWidget.razor`, `index.html`):**
  - Resolved an issue where long unbroken strings (e.g., continuous characters, long URLs, code tokens) caused the chat box to stretch horizontally past the right edge of the chat container and push the purple Send button off-screen.
  - Added flexbox child width containment (`min-w-0`) to the input wrapper `<div class="flex-1 min-w-0 ...">` and `<form class="... w-full min-w-0">`.
  - Removed `field-sizing: content;` which caused Chromium browsers to resize the textarea's width to fit unbroken text instead of wrapping it.
  - Enforced `overflow-wrap: anywhere; word-break: break-word; white-space: pre-wrap; overflow-x: hidden;` ensuring all text wraps at the right border of the input box.

- **Clean 1-to-3 Line Vertical Auto-Expansion (`index.html`):**
  - Fixed textarea vertical layout where horizontal overflow previously inflated `scrollHeight` with scrollbar metrics, causing single lines to sit at the bottom of a taller empty box with an unwanted vertical scrollbar.
  - Refined `resizeChatTextarea` to accurately measure content height and toggle `overflow-y: hidden` for 1 to 3 lines (38px up to 82px) and enable `overflow-y: auto` only when text exceeds 3 lines.
  - Maintained instant 1-line snapback (`38px`) upon sending messages via `resetChatTextarea`.

### Enhanced & Refactored — [2026-10-05] (Audit Trail Database Decoupling & Targeted Real-Time Notification Center)

- **Audit Trail Database Operations & Backups Decoupling (`AuditService.cs`, `AuditLog.cs`, `BackupController.cs`, `AuditController.cs`, `AuditLogs.razor`):**
  - Removed database operation logs, snapshot creation, and restore entries from workspace-level audit logs to ensure host-level server maintenance never pollutes tenant activity ledgers.
  - Removed `LogBackupAsync` and `AuditModules.Backup` from `IAuditService`, `AuditService`, and `AuditLog.cs`.
  - Updated `AuditController.cs` to eliminate `Backup` and `Restore` from security metrics and high-impact critical queries.
  - Refactored `AuditLogs.razor` UI: removed the "Backups & DB" filter tab and repurposed Metric Card 4 as "Security & Roles" (`Roles, access & permissions`).

- **Targeted Notification Architecture & Removal of Intrusive Toasts (`NotificationHub.cs`, `NotificationsController.cs`, `NotificationPayload.cs`, `LeaveApprovals.razor`, `Ledger.razor`, `Advances.razor`, `MyLeaves.razor`):**
  - Removed disruptive global pop-up toasts for new leave requests, advance/e-loan requests, and status modifications in `LeaveApprovals.razor`, `Ledger.razor`, `Advances.razor`, and `MyLeaves.razor`.
  - Upgraded `NotificationHub.cs` with smart connection grouping: `user_{userId}` for targeted user alerts, and `org_{orgId}_admins` for workspace administrator/approver notices.
  - Created `NotificationsController.cs` providing `GET /api/notifications/recent` to seamlessly hydrate user notification center trays on initial load and across browser refreshes.

- **Admin & Requester Event Targeting (`LeaveRequestController.cs`, `AdvanceRequestController.cs`):**
  - **For Workspace Admins:** Submissions of new Leave Requests or Cash Advance / E-Loan requests are dispatched to `org_{orgId}_admins` with actor name, type, dates, and amounts.
  - **For Requesting Employees:** Request approval or rejection decisions are dispatched directly to `user_{employee.UserId}` with the explicit outcome (Approved or Rejected), leave dates, or approved/rejected loan amounts.

- **Task Assignment & Due Date Alerts (`BoardsController.cs`, `NotificationsController.cs`):**
  - Dispatches targeted `Task Assigned` alerts directly to assigned employees when board cards are created or when new assignees are added to existing tasks.
  - Added automated due date checks flagging tasks due within 48 hours ("Task Closing Due Date") and overdue tasks ("Task Overdue") with board navigation links.

- **Modernized Notification Center UI (`NotificationCenter.razor`):**
  - Redesigned dropdown with category-specific iconography (Leave, Advance, Task, Location, System) and color-coded status badges: Emerald for Approved, Rose for Rejected, Indigo for Assigned, Amber for Due Soon, and Red for Overdue.
  - Added "Mark all as read", "Clear all", click-to-navigate action handling, and responsive empty and loading states.

### Enhanced & Refactored — [2026-10-05] (Server Station Disaster Recovery Console & Workspace Module Decoupling)

- **Decoupled Database Backups from Workspace Admin (`NavMenu.razor`, `RoleManagement.razor`, `Program.cs`, `DbSeeder.cs`, `AuthController.cs`):**
  - Completely removed the `/admin/backups` module from the Web application's navigation drawer (`NavMenu.razor`), Role Management configurable permission matrices (`RoleManagement.razor`), and default authorization policies.
  - Eliminated the multi-tenant risk where restoring a whole-database dump from within a single workspace admin menu would inadvertently reset other workspaces and user accounts.
  - Hardened `BackupController.cs` to strictly authorize loopback requests originating from the local Server Station host machine (`X-OfficeCore-Station: ServerStationOrchestrator`), blocking remote LAN workstation terminals from initiating whole-system database dumps or drops.

- **Native Server Disaster Recovery Console (`OfficeCore.Desktop.Server`):**
  - Built a dedicated, first-class Disaster Recovery & Database Snapshots console inside `MainWindow.xaml` and `MainWindow.xaml.cs` of the Desktop Server Station WPF host.
  - Integrated real-time health indicator cards: PostgreSQL engine status (`pg_dump.exe` detection & path display), Total Snapshots count & breakdown (manual, automatic, safety pre-restore), Compressed storage footprint (MB/GB), and Active daily schedule routine.
  - Implemented 1-Click Snapshot Creation with custom labels, 1-Click Safety-Restoration with pre-restore snapshot generation, External `.dump` import & upload wizard, Direct folder access in Windows Explorer (`Open Folder`), and Copy/Export actions.
  - Added an inline collapsible Schedule Settings panel allowing server operators to toggle daily automated backups, adjust run time (HH:mm), customize retention count (auto-pruning old snapshots), and set custom PostgreSQL bin paths.
  - Created `DesktopBackupService.cs` providing unified API client orchestration with graceful local disk fallback when the API process is stopped.

### Enhanced — [2026-10-05] (Emoji Reactions Positioning, Per-User Reaction Overwriting & Dynamic Textarea Auto-Sizing)

- **Top-Anchored Emoji Reaction Bar (`GlobalChatWidget.razor`):**
  - Repositioned the emoji reaction bar (`👍`, `❤️`, `😂`, `😮`, `😢`, `🙏`) directly on top of the chat bubble (`bottom-full mb-1.5`) rather than protruding outward from the action pill.
  - Set dynamic horizontal alignment (`right-0` for sent messages, `left-0` for received messages) with `whitespace-nowrap` to ensure reactions always extend inward over the message and never face to the right or clip past the chat window edge.
  - Added upper headroom padding (`pt-8`) to the scrollable messages container so even messages at the top of the history have ample clearance for the reaction bar.

- **Single Reaction Enforcement & Overwriting (`GlobalChatWidget.razor`):**
  - Updated reaction state tracking to map `[MessageId -> [UserId -> Emoji]]` (`_messageUserReactions`).
  - Enforced a 1-reaction-per-user rule: selecting a new reaction overwrites any previous emoji reaction the user previously chose on that message; clicking the same reaction un-reacts/removes it.
  - Updated reaction badge grouping below chat bubbles to accurately reflect aggregated counts per emoji with current user reaction highlighting.

- **Auto-Expanding Chat Input Box (1 to 3 Lines Max) (`GlobalChatWidget.razor`, `index.html`):**
  - Configured chat `<textarea>` to start with a compact single line (`rows="1"`, `min-h-[38px]`, `height: 38px;`).
  - Added native CSS `field-sizing: content;` and max height restriction (`max-h-[84px]`), allowing smooth expansion up to 3 lines max as text is typed or new lines are entered, after which an internal custom scrollbar engages.
  - Implemented delegated input auto-resize listener and `resetChatTextarea` JavaScript interop in `index.html` to guarantee cross-browser auto-resizing and instant reset back to 1 line upon sending a message.

### Enhanced — [2026-10-04] (Message Bubble Hover Scoping & Menu Simplification)

- **Bubble-Scoped Hover Actions & Timestamps (`GlobalChatWidget.razor`):**
  - Implemented CSS `:has()` parent-aware selector (`.chat-msg-bubble-group:has(.chat-msg-bubble-wrap:hover)`) ensuring actions and timestamps reliably appear on sent and received messages when hovering directly on the message bubble or card.
  - Added visibility grace period and pointer-events handling so hovering empty space never prematurely reveals actions, while moving between bubble and action buttons remains smooth and flicker-free.
  - Positioned the 3-dot context menu and emoji reaction bar dynamically: opens above (`bottom-full mb-1.5`) with inward alignment (`left-0` for sent, `right-0` for received) to eliminate side clipping, and automatically shifts downward (`top-full mt-1.5`) when messages are at the very top of the chat to avoid clipping under the header bar.

- **Simplified Message Dropdown Actions (`GlobalChatWidget.razor`):**
  - Removed "Unsend" and "Report" actions from both outgoing and incoming message 3-dot dropdown menus, keeping only "Forward" and "Pin" / "Unpin" for a clean, distraction-free experience.

- **Action Bar Hitbox Pill Box & Bridge (`GlobalChatWidget.razor`):**
  - Enclosed message action icons (More, Reply, React) in a solid, glassmorphic pill box (`rounded-full bg-zinc-900 border border-zinc-700/80 shadow-md backdrop-blur-md px-1 py-0.5`).
  - Added an invisible `::before` hitbox bridge (`inset: -8px -12px`) bridging the action buttons to the message bubble so hovering anywhere within the box or moving between buttons never causes actions to prematurely close or disappear.
  - Locked action visibility and interaction when any dropdown or reaction picker is actively open (`is-active`).

- **Chat Selection Forward Message Modal (`GlobalChatWidget.razor`):**
  - Replaced basic clipboard copying with a full Meta Messenger-style Forward Modal dialog overlay.
  - Displays original message excerpt/attachment preview, searchable list of all direct and group conversations, and instant "Send" action buttons.
  - Automatically posts the forwarded content/attachment to the selected room and transitions button to "Sent" state with real-time toast feedback.

### Enhanced — [2026-10-04] (Meta Messenger-Style Chat: Hover Actions, Hover Timestamps, Time Dividers, Message Pinning)

- **Removed Hover Pin from Room List (`GlobalChatWidget.razor`):**
  - Cleaned up the conversation list on the left side by removing the hover pin icon button on chat rooms, preserving a neat, unencumbered room list.

- **Messenger-Style Message Hover Actions & 3-Dot Menu (`GlobalChatWidget.razor`):**
  - Added Meta Messenger-style hover action controls alongside chat bubbles:
    - **More Options (`⋮`)**: Opens a Messenger popup menu with "Unsend", "Forward", "Pin" / "Unpin", and "Report".
    - **Reply (`↶`)**: Prepares an inline reply bar above the composer with the author name and message excerpt.
    - **React (`☺`)**: Opens a quick reaction bar with standard emojis (👍, ❤️, 😂, 😮, 😢, 🙏) and displays reaction badges on the message.
  - Aligned action controls dynamically: to the left of outgoing messages (`isMine`) and to the right of incoming messages (`!isMine`).

- **Hover-Only Message Timestamps (`GlobalChatWidget.razor`):**
  - Individual message timestamps beneath message bubbles are now hidden by default and smoothly revealed only when hovering over the message bubble container.

- **Dynamic Messenger Time & Date Dividers (`GlobalChatWidget.razor`):**
  - Centered subtle timestamp and day dividers are rendered between messages when 20+ minutes have elapsed or when crossing a new date boundary.
  - Matches Messenger formats: `3:12 PM` for today, `Wed 2:46 PM` for earlier this week, and full date strings for older messages.

- **Message Pinning & Unsend Functionality (`GlobalChatWidget.razor`, `ChatController.cs`):**
  - Implemented message-level pinning persisted per room with visual pin indicators and a pinned message banner in the chat header.
  - Added pinned messages list to the Chat Info drawer with single-click unpin capability.
  - Implemented `DELETE /api/chat/messages/{messageId}` endpoint in `ChatController.cs` and client-side unsend support with SignalR `MessageDeleted` real-time broadcasting.

### Fixed & Enhanced — [2026-10-04] (Role Management Redesign, Workspace Owner Full Access, Ledger & Advances Modal Conversion)

- **Workspace Owner Default Full Access:**
  - `OrganizationClaimsTransformation.cs` now detects the workspace owner and automatically grants all module claims (`Dashboard`, `MyAttendance`, `MyLeaves`, `Advances`, `Boards`, `Clients`, `Attendance`, `Ledger`, `Payroll`, `Invoices`, `Users`, `Roles`, `LeaveApprovals`, `WorkSchedules`, `TimeOff`, `Locations`, `Organization`, `Backups`, `AuditLogs`), eliminating the dependency on seeded roles.
  - `AuthController.cs` `/me` endpoint mirrors this logic so the frontend receives all module claims on login for workspace owners.
  - Owner title display: `AuthController.cs` now defaults to `Owner` instead of `Employee` for workspace creators, while still allowing custom roles (like those with `ShowAsTitle = true`) to override it.
  - `OrganizationsController.cs` no longer seeds hardcoded `Admin` / `Employee` roles on new organization creation — owners use Role Management to create custom roles as needed.

- **Global Dark Mode Adjustments:**
  - Added `color-scheme: dark;` to `html.dark` in `index.html`.
  - Applied `filter: invert(1) brightness(0.7);` to `-webkit-calendar-picker-indicator` for `date`, `time`, and `datetime-local` inputs in dark mode. This completely fixes the invisible/black calendar icon issue across all date pickers in the application.

- **Global Chat Widget Upgrades (`GlobalChatWidget.razor`):**
  - **Universal Chat Info:** The Chat Info drawer is now available for both Group Chats and Direct Messages, rather than being restricted just to groups.
  - **Message Search:** Added a "Search Chat" input within the Info drawer that instantly filters the active conversation's messages and attachments.
  - **Media & Files History:** Added a "Shared Media & Files" gallery in the Info drawer that lists all previously sent images and documents in the current chat, with quick-click access to the attachment viewer.
  - Added dynamic avatar/header rendering in the Info drawer to distinguish between Direct Messages and Group Chats.

- **MyLeaves Modal Standardization (`MyLeaves.razor`):**
  - Completely replaced the `<AppModal>` component usage with the standard inline modal layout (`max-h-[90vh] flex flex-col overflow-hidden`, `shrink-0` header/footer, `overflow-y-auto flex-1` body) to perfectly match the visual consistency of the Ledger, Advances, and Role Management modals.

- **Role Management Redesign (`RoleManagement.razor`, `RoleController.cs`):**
  - Removed `Organization` from the `Administration` module category in the Role Management UI — Organization access is reserved for the workspace owner only via claims transformation.
  - Removed all hardcoded `Admin`-role protections: role name is now fully editable, the Delete button shows for all roles, and `RoleController.cs` no longer blocks renaming or deleting any role.
  - Role modal redesigned with `max-h-[90vh] flex flex-col overflow-hidden` layout — fixed header, scrollable body, fixed footer action row.

- **Ledger Expense Form → Modal (`Ledger.razor`):**
  - Converted the inline "Log Company Expense" card (rendered inside the Expenses tab) into a proper centered modal dialog (`fixed inset-0`, `max-h-[90vh] flex flex-col overflow-hidden`, scrollable body, pinned footer).
  - Updated the "Create Advance or Loan" modal to use `flex flex-col max-h-[90vh] overflow-hidden` with a scrollable body.

- **Advances Request Form → Modal (`Advances.razor`):**
  - Converted the inline "Request Advance or E-Loan" card into a proper centered modal dialog with `max-h-[90vh]`, scrollable content body, and pinned action footer.

- **LeaveApprovals Create Leave Modal (`LeaveApprovals.razor`):**
  - Applied `flex flex-col max-h-[90vh] overflow-hidden` with `shrink-0` header and `overflow-y-auto flex-1` body to the Create Leave admin modal.



- **OPC (One Person Corporation) Business Type:**
  - Added `OPC (One Person Corporation)` to the business type options in the Client module ([Clients.razor](file:///d:/Apps/Antigravity/Projects/OfficeCore/src/OfficeCore.Web/Pages/Clients.razor)), expanding registration types alongside Sole Proprietorship, Partnership, Corporation, and Cooperative.

- **Business Active / Inactive Status Management:**
  - Added `public bool IsActive { get; set; } = true;` to the `Business` entity ([Business.cs](file:///d:/Apps/Antigravity/Projects/OfficeCore/src/OfficeCore.Api/Models/Business.cs)) and generated/applied EF Core migration `20261004110438_AddBusinessIsActive` with default value `true`.
  - Added an intuitive Active vs Inactive status selector in the Business modal, enabling operators to mark client businesses as active or inactive when creating or editing them.
  - Added status badge indicators (`Active` in emerald, `Inactive` in neutral zinc) to each business card in the client table, with inactive businesses styled with subtle muted opacity.
  - Updated `BusinessDto` and client edit mappings in `Clients.razor` to preserve and persist `IsActive` state on business updates.

- **Modal UI Viewport Fitting & Internal Scrollability:**
  - Fixed modal height clipping across both the Business Form Modal and Client Form Modal in [Clients.razor](file:///d:/Apps/Antigravity/Projects/OfficeCore/src/OfficeCore.Web/Pages/Clients.razor).
  - Configured `max-h-[90vh]` flex column layout with `shrink-0` header and footer, and `overflow-y-auto custom-scrollbar flex-1` on the modal body.
  - Guaranteed that modal headers, action buttons (Save / Cancel), and inputs remain visible and cleanly accessible on all screen resolutions without overflowing off-screen.

### Fixed & Enhanced — [2026-10-04] (Board Card Labels Real-Time Sync & Due Date Urgency Indicators)

- **Board Card Labels Real-Time Synchronization:**
  - Configured `OfficeCore.Web.Client.Auth.CookieHandler` on `HttpMessageHandlerFactory` in `BoardView.razor`, `Dashboard.razor`, and `NotificationCenter.razor`, ensuring browser cookies and authentication headers are sent with SignalR WebSocket negotiation requests in Blazor WebAssembly.
  - Updated `BoardHub.cs` so connections join board groups based on board GUID existence even if the active organization cookie is not present during WebSocket negotiation.
  - Added an automatic fallback in `NotificationHub.cs` to resolve organization ID directly from the employee database record if cookie is absent.
  - Updated `BoardsController.AddLabelToCard` to assign `OrganizationId` and enforce idempotency check to avoid primary key collisions.
  - Updated `BoardsController.NotifyBoardUpdated` to broadcast `BoardUpdated` and `TaskUpdated` to both board-specific and organization-wide hub groups and clients.
  - Updated `BoardsController.GetBoard` to safely project card labels with fallback to board labels dictionary, preventing null reference errors.
  - Updated `BoardView.razor` label rendering with safe null fallback and added `StateHasChanged()` to `ToggleLabel` for instant local feedback.

- **Due Date Urgency Indicator Colors (Board & Dashboard):**
  - Updated deadline color indicators across Kanban Board cards ([BoardView.razor](file:///d:/Apps/Antigravity/Projects/OfficeCore/src/OfficeCore.Web/Pages/BoardView.razor)) and Dashboard Assigned Tasks ([Dashboard.razor](file:///d:/Apps/Antigravity/Projects/OfficeCore/src/OfficeCore.Web/Pages/Dashboard.razor)):
    - **3 days or less before deadline (including overdue):** Styled in urgent Red (`text-red-400 bg-red-400/10 border border-red-500/20` / `bg-red-500/10 text-red-400 border border-red-500/20`).
    - **7 days or less before deadline (4–7 days remaining):** Styled in warning Orange (`text-orange-400 bg-orange-400/10 border border-orange-500/20` / `bg-orange-500/10 text-orange-400 border border-orange-500/20`).
    - **More than 7 days remaining:** Neutral standard styling (`bg-zinc-800 text-zinc-400`).
    - **Completed Tasks ("Done"):** Excluded from urgency colors, styled in emerald or neutral complete indicator.
    - Updated Dashboard personal task status dot to match the due date urgency color palette.

### Fixed & Enhanced — [2026-10-04] (Installer .gitignore, E-Loan/Advance & Leave Request/Action Timestamps, Board Member Permissions & Card Attachments Fix)

- **Installers Ignored in Git:**
  - Updated `.gitignore` across branches to ignore `dist/` and `Setup-*.exe` binaries, preventing large generated installer deliverables from bloating git commits and branch history.

- **Request & Action Taken Timestamps for E-Loans, Advances, and Leaves:**
  - **Leaves:** Extended `LeaveRequest` entity with `public DateTime? ActionTakenAt { get; set; }`. Generated and applied EF Core migration `20261004101244_AddLeaveActionTakenAt` to PostgreSQL.
  - Automatically set `ActionTakenAt = DateTime.UtcNow` on approve, reject, and admin creation in `LeaveRequestController.cs`.
  - Projected `ActionTakenAt` in leave queries and updated `MyLeaves.razor` and `LeaveApprovals.razor` to display formatted request date/time and resolution timestamp on approved/rejected items.
  - **Advances & E-Loans:** Updated `AdvanceRequestController.cs` to project `ResolutionDate` alongside `RequestDate`.
  - Updated `Advances.razor` and `Ledger.razor` to render both request date/time and action taken timestamp (approved, rejected, or cancelled) across employee and admin views.

- **Board Member Permissions & Kanban Card Security:**
  - Enforced strict authorization in `BoardsController.cs` and `BoardView.razor` so regular board members cannot modify card titles, descriptions, due dates, or assignees.
  - Card Title `<input>` and Description `<textarea>` are rendered in read-only mode for non-admin members, with modal header displaying "Card Details" instead of "Edit Card", and the "Save Card" button hidden in favor of a clean "Close" button.
  - Restricted Checklist Item addition to Board Admins and Owners (`_canEditBoardStructure`); members can still toggle completion on checklist items they work on.
  - Restricted Label creation, board label deletion, and card label assignment/toggling to Board Admins and Owners (`_canEditBoardStructure`).

- **Card Attachments Fix & Collaborator Access:**
  - Resolved card attachments failing to upload: removed unintentional 403 Forbidden check on `BoardRole.Member` in `BoardsController.AddAttachment`, allowing board members to collaborate and upload task deliverables.
  - Added `[FromForm]` attribute to `AddAttachment`, configured 25MB file upload limit, added ContentType header in multipart payload, and implemented upload loading indicator and error toast reporting.
  - Updated `DeleteAttachment` to allow deletion by either the original file uploader or Board Admins/Owners.
  - Updated `GetBoardById` and `AddAttachment` responses to project `UserId`, enabling granular uploader-only delete controls on the frontend.



### Fixed & Enhanced — [2026-10-03] (Chat Attachment Override Bug, Client Business Rich Form & 4 Business Types, Real-Time Dashboard Live Sync & Page Layout Parity)

- **Chat Attachment Override Visual Bug:**
  - Resolved an issue in `GlobalChatWidget.razor` where sending an attachment or an empty text message with a file would override or visually replace the previous message's attachment.
  - Added explicit `IsOptimistic` boolean flag to `ChatMessageDto`. Updated the SignalR `ReceiveMessage` event handler to only perform optimistic deduplication against messages with `IsOptimistic == true`.
  - Added `@key="_inputFileKey"` on `<InputFile />` and incremented key in `ClearPendingAttachment()` to force DOM state re-initialization so subsequent file selections work reliably without stale state.
  - Added attachment state cleanup on `OpenRoom()` and `CloseRoom()` to prevent cross-room file leakages.

- **Client Business Rich Data Fill-up Form & Tax Classification:**
  - Extended `Business` domain entity in `OfficeCore.Api` with `TinId` (`[MaxLength(50)]`) and `BusinessType` (`[MaxLength(100)]`) alongside existing `IsVatRegistered` status.
  - Created and executed EF Core migration `20261003115635_AddBusinessTinAndType` updating PostgreSQL database schema.
  - Enriched `Clients.razor` with updated `BusinessDto` model, supporting search filtering by TIN and Business Type.
  - Standardized Business Type selection to the 4 business structures:
    - `Sole Proprietorship`
    - `Partnership`
    - `Corporation`
    - `Cooperative`
  - Added interactive VAT status toggle cards (`VAT Registered` vs `Non-VAT`) with dynamic visual feedback.
  - Enhanced client business card UI in `Clients.razor` displaying Business Type badges (`bg-indigo-950/50 text-indigo-300`), VAT/Non-VAT status badges, and formatted monospace TIN pills.

- **Real-Time Dashboard Live Sync:**
  - Injected `IHubContext<NotificationHub>` into `BoardsController.cs` and `AttendanceController.cs`.
  - Added real-time notifications (`BoardUpdated`, `TaskUpdated`, `AttendanceUpdated`) broadcasted to tenant-scoped groups (`org_{orgId}`).
  - Subscribed `Dashboard.razor`'s SignalR connection to `BoardUpdated`, `TaskUpdated`, `TaskAssigned`, `AttendanceUpdated`, `EmployeeUpdated`, and `MemberJoined` events to reload stats and assigned tasks without needing a manual page refresh.

- **UI Layout & Horizontal Spacing Parity:**
  - Resolved layout inconsistency where `Dashboard.razor`, `Backups.razor`, and `AuditLogs.razor` had excessive margins and blank space on the left and right compared to `UserManagement`, `Attendance`, and `Clients`.
  - Removed restrictive `max-w-7xl mx-auto` containers in favor of standard responsive `<div class="space-y-6">` layouts consistent across the entire application.


### Fixed & Enhanced — [2026-10-03] (Dark Mode Date Pickers, Notification Outside-Click Dismissal, Map Stacking Context & Boards Default Module Architecture)

- **Dark Mode Calendar & Time Picker Visibility:**
  - Added global WebKit calendar picker indicator CSS styling in `src/OfficeCore.Web/Styles/app.css` for `input[type="date"]`, `input[type="time"]`, and `input[type="datetime-local"]`.
  - Configured `color-scheme: dark;` and `filter: invert(0.9) brightness(1.2)` on dark mode date/time inputs so native calendar popover indicators are bright and clearly visible on dark input backgrounds (`MyLeaves.razor`, `Attendance.razor`, etc.).
  - Added seamless `color-scheme: light; filter: none;` parity for light mode themes.
- **Notification Dropdown Outside-Click Dismissal:**
  - Resolved issue where clicking outside the notification dropdown failed to dismiss the menu unless clicked inside the 56px top header area.
  - Eliminated `backdrop-blur-md` on the top `<header>` element in `MainLayout.razor` which previously created an isolated containing block trapping fixed viewport overlays (`fixed inset-0`) inside the header.
  - Added outside-click dismissal overlay for the workspace switcher dropdown in `MainLayout.razor`.
- **Location Map Search Bar Z-Index Overlap:**
  - Isolated the Leaflet map and overlay grid stacking context in `Locations.razor` (`.loc-map-card { isolation: isolate; z-index: 1; }` and `.loc-page { position: relative; z-index: 1; }`).
  - Elevated `<header>` z-index to `z-40`, guaranteeing the notification center (`z-[100]`) and header dropdowns always float strictly above the floating "Search location…" input and Leaflet controls.
- **Boards Default Module Architecture Alignment:**
  - Formally classified `Boards` as an unconditional Default Core Module housed exclusively under **Main** navigation (`/dashboard`, `/my-attendance`, `/boards`, `/myleaves`, `/advances`).
  - Changed authorization attributes in `Boards.razor` and `BoardView.razor` from `[Authorize(Policy = "RequireBoards")]` to standard `[Authorize]` to match all other default modules.
  - Updated `agent.md` documentation to reflect that all Default Modules reside under **Main**, and configurable modules are strictly partitioned into **Finance** and **Administration** in Role Management.

- **Chat Mentions & Preview Rendering:**
  - Resolved user mentions rendering raw markdown links (e.g., `[Beatriz Ramos](@user-...)`) in message bubbles and room list previews (`GlobalChatWidget.razor`).
  - Fixed nested box glitch in user mentions where loose mention regex was erroneously double-matching inside previously formatted mention spans.
  - Redesigned mention pill aesthetics to match enterprise standards: clean frosted white pill (`bg-white/20 text-white font-bold px-1.5 py-0.5 rounded-md`) in outgoing bubbles and sleek tinted pill (`text-fuchsia-300 bg-fuchsia-500/15 font-medium px-1.5 py-0.5 rounded-md`) in incoming bubbles.
  - Updated `GetPreviewText` in `GlobalChatWidget.razor` to strip `[Name](@user-id)` into clean `@Name` snippets in the room list preview.
- **Real-Time Board & Task Chat Link Access:**
  - Resolved issue where mentioning newly created boards or tasks showed as disabled (`cursor-not-allowed`) until the page was manually reloaded.
  - Made board and task cross-links always render as active, clickable hyperlinks (`<a href="...">`) that navigate directly to `/boards/{id}` or `/boards/{id}?taskId={taskId}`.
  - Implemented dynamic cache updates for `_myBoardIds` in `GlobalChatWidget.razor` during tag search (`SearchTagsAsync`), tag insertion (`InsertTag`), message formatting, room navigation (`OpenRoom`), and widget opening (`ToggleChat`).
- **Enterprise SVG Icon Standardization:**
  - Removed informal Unicode emojis (`⏱️`, `🌴`, `📌`) from `DashboardController.cs` and `Dashboard.razor`, replacing raw emoji icons in the "Live Team Activity" feed with modern, high-contrast SVG category icon pills matching the design system in `NotificationCenter.razor`.
  - Replaced informal text character close buttons (`✕`) across `GlobalChatWidget.razor`, `AuditLogs.razor`, `Backups.razor`, `Locations.razor`, and `TimeOff.razor` with sleek SVG cross icons.
  - Replaced unicode checkmark `✓ Copied` in `UserManagement.razor` with an animated SVG checkmark icon.
  - Recompiled Tailwind CSS v4 assets (`wwwroot/app.css`).

### Added & Enhanced — [2026-10-02] (Board Labels & Security Fixes)

- **Kanban Board Customization & Security:**
  - Implemented custom color-coded Board Labels, allowing board owners and admins to create labels with specific hex colors and attach them to cards.
  - Implemented List Color Customization, allowing users to customize the background and header color of individual lists/columns via a color picker (`ColorHex` field and EF Core migration included).
  - Visually hid all board structure modification elements (add/delete lists, add/delete cards) for users with `Member` and `Viewer` roles to prevent confusion.
  - Enforced strict frontend restrictions blocking `Member` and `Viewer` roles from removing assignees from cards.
  - Fixed a backend user lookup bug in `BoardsController` where `Employee.Id` mapping was rejected in favor of `ApplicationUser.Id`, allowing correct addition of board members.
  - Added the missing `Admin` role option in the board member management dropdown for existing members.
  - Dynamically disabled due date modification and assignee selection for users without structural edit permissions.
- **Employee Requests:**
  - Implemented Request Cancellation for employees, allowing them to instantly cancel their own *Pending* Cash Advances, E-Loans, and Leave requests directly from `MyLeaves.razor` and `Advances.razor`.
- **Chat Improvements:**
  - Implemented an invisible plain-text mapping logic in `GlobalChatWidget.razor` that hides raw markdown tags (e.g., `[@DemoAdmin](...)`) in the chat text box, allowing users to type and see perfectly clean `@` mentions and `#` tags while still rendering correct markdown behind the scenes upon submission.

### Added & Enhanced — [2026-10-01] (Real-Time Live Sync, Chat Mentions & Board Cross-Linking, Unlimited Invites)

- **Real-Time Data Sync (Leaves, Advances, Ledger, Dashboard):**
  - Resolved SignalR payload mismatch causing silent failures in real-time entity updates.
  - Controllers (e.g., `LeaveRequestController`, `AdvanceRequestController`, `CompanyExpenseController`) now broadcast parameterless refresh events matching client-side expectations.
  - Implemented live SignalR data synchronization in `Dashboard.razor`, `Ledger.razor`, `Advances.razor`, and `MyLeaves.razor` automatically updating tables and metric cards without requiring page reloads.
- **Chat Enhancements (@Mentions & Board/Task Cross-Linking):**
  - Added smart `@user` autocomplete dropdown parsing in the chat input textbox (`GlobalChatWidget.razor`).
  - Implemented interactive rendering for user mentions (`@FirstNameLastName`) highlighted with fuchsia text.
  - Implemented intelligent parsing for `#task-XXX` and `#board-XXX` formats, automatically rendering them as clickable buttons linking directly to the project Kanban boards.
- **Invite Code Unlimited Usage:**
  - Modified generic Workspace Invite Links to allow unlimited multi-use access instead of single-use, while maintaining a strict 7-day expiration window (`OrganizationsController.cs`).
  - Updated User Management UI to explicitly read "Unlimited uses, expires in 7 days."
- **Kanban Board Feature Status Validation:**
  - Verified and confirmed that Advanced Kanban features (Subtasks, Checklists, Card Comments, and File Attachments) were already fully implemented on both frontend (`BoardView.razor`) and backend endpoints. Updated `ROADMAP.md` tracking accordingly.
- **Membership Permissions:**
  - Granted Board Owners permission to add/remove board members in `BoardsController.cs`.

### ACBA Production Hardening & Specification Alignment — [2026-09-30]

- **Removed Demo Data Seeder (`DbSeeder.cs`):**
  - Eliminated mock employee accounts, test attendance logs, sample payroll cutoffs, and demo boards on the ACBA branch so databases start completely clean with migration auto-execution and global admin identity role registration.
- **Removed Demo Account Access (`Login.razor`):**
  - Removed quick demo buttons and credentials autofill from the login view.
- **Disabled OTP Simulation (`SignUp.razor`, `EmailOtpService.cs`, `SmsOtpService.cs`):**
  - Removed the simulation OTP display banner and autofill button in registration.
  - Sanitized OTP dispatch services so debug codes are never returned in API payloads, requiring authentic email/SMS codes.
- **Hardened Inspector & DevTools Restrictions (`MainWindow.xaml.cs`, `index.html`):**
  - Disabled right-click context menu and DevTools/inspect element shortcuts (F12, Ctrl+Shift+I/J/C, Ctrl+U) across Desktop Client, Desktop Server, and Web frontends.
- **Preserved Biometric Importer Integration (`BiometricImportHub.razor`, `Attendance.razor`):**
  - Retained dedicated ACBA Biometric Import Hub for USB/standalone scanner XLS parsing alongside core attendance features.

### UI Modernization & Enterprise Cleanup — [2026-09-30] (Replaced Informal Emojis with Professional SVG Icons & Status Badges)

- **Removed Informal Unicode Emojis Across Web UI:**
  - Standardized enterprise styling across `OfficeCore.Web` by replacing informal emoji characters with tailored Heroicons SVGs and sleek status dot indicators.
  - **User Management (`UserManagement.razor`):** Replaced `👑 OWNER` badge with a sleek amber dot status badge (`Owner`) consistent with table badge standards.
  - **Audit Logs (`AuditLogs.razor`):** Removed emoji prefixes across all category filters (`All Categories`, `Payroll`, `Employee & Rates`, `Attendance`, `Leaves`, `Advances`, `Backups & DB`, `Security & Roles`), cleaned table badge renderers, replaced `🔍 Diff Changes` with an SVG search/diff icon, and replaced modal close text with an SVG icon.
  - **Notification Center (`NotificationCenter.razor`):** Replaced emoji avatars (`🚀`, `🌴`, `💵`, `📍`, `🔔`) with dynamic SVG category icons (system, leave, cash advance, location) matching the dark glassmorphic design system.
  - **Dashboard (`Dashboard.razor`):** Replaced section headers (`Team Operations`, `My Personal Status`) and all quick action shortcuts with clean, accessible SVG icons.
  - **Time Off (`TimeOff.razor`):** Replaced empty state clipboard emoji with a document SVG and updated policy leave type badges (`Sick Leave`, `Vacation Leave`) with color-coded dot badges.
  - **Authentication & Onboarding (`Login.razor`, `SignUp.razor`):** Replaced crown and person demo account emojis with clean shield/user SVGs, and replaced simulation OTP emoji with an info icon.
  - **Locations (`Locations.razor`):** Replaced map pin emoji with an SVG pin icon on the Center on Map action button.
- **Tailwind Asset Rebuild:**
  - Successfully recompiled `wwwroot/app.css` using Tailwind CSS v4 CLI.

### Documentation & Agent Directives — [2026-09-30] (Roadmap & Ideation Workflow Consolidation, Obsolete Markdown Pruning)

- **Mandatory Ideation & Planning Protocol (`ROADMAP.md`, `agent.md`, `.agents/rules/operating.md`):**
  - Enforced directive requiring all AI agents and engineers to consult `agent.md` and `ROADMAP.md` before planning, scoping, or designing new features or architectural changes.
  - Added directive requiring `ROADMAP.md` to be continuously updated during brainstorming, ideation, and planning sessions (updating priorities P1–P4, scope, and status tags), and marking items as completed upon delivery.
  - Synchronized workspace operating rules in `.agents/rules/operating.md` using the complete, updated `agent.md` architecture guide for automatic ingestion by Antigravity IDE.
- **Obsolete Documentation Cleanup:**
  - Removed deprecated and redundant files `PROJECT_STATUS.md` and `RUNNING.md`, centralizing architecture, developer setup, and operational guidance directly in `README.md`, `agent.md`, and `ROADMAP.md`.
  - Updated all internal documentation links and repository structure maps across `README.md` and `agent.md`.

### Fixed & Enhanced — [2026-09-30] (Workspace Owner Visibility, Headcount Reconciliation & Hierarchical RBAC)

- **Workspace Owner Visibility & Headcount Alignment:**
  - Resolved discrepancy where workspace dashboard headcount counted 2 users while user administration only displayed 1.
  - Removed explicit `e.UserId != createdByUserId` exclusions in `UserController.cs` (`GetUsers()`) so the workspace owner is visible in the administration table alongside regular users.
  - Removed exclusion filter in `PayrollComputationService.cs` to ensure workspace owners are included in payroll computations whenever marked as an employee (`IsEmployee = true`).
- **Hierarchical Role-Based Access Control (RBAC):**
  - Introduced hierarchical administration rules distinguishing the **Main Admin (Workspace Creator / Owner)** from **Assigned Administrators**:
    - **Main Admin (Owner):** Retains supreme administrative authority. Can edit their own employee profile details (ID, schedule, rates), and can edit other assigned admins and employees. Cannot be deactivated, resigned, deleted, or stripped of admin privileges.
    - **Assigned Admins:** Can manage standard employees, but are strictly prohibited from modifying the Main Admin (Owner) or other administrators.
  - Enforced backend validation checks in `UserController.cs` (`UpdateUser`, `ResignUser`, `DeleteUser`) to block unauthorized edits or removal attempts against workspace owners or peers.
- **User Management UI Modernization (`UserManagement.razor`):**
  - Added a distinct "👑 Owner" indicator badge in the user directory and edit drawer.
  - Conditionally disabled account deactivation toggle, role selection pill buttons, resignation modal, and deletion modal when viewing or editing the workspace owner.
  - Rebuilt Tailwind CSS v4 assets to ensure UI styling consistency.

### Fixed — [2026-09-29] (Git Repository Large Binary Cleanup & GitHub Push Unblocked)

- **Removed Installer Binaries from Git History (All Branches):**
  - `dist/Setup-OfficeCore-Client.exe` (~270MB) and `dist/Setup-OfficeCore-Server.exe` (~300MB) were previously committed to the repository, exceeding GitHub's 100MB file size limit and blocking all pushes to `production` and `ACBA`.
  - Rewrote all branch histories using `git filter-branch` to permanently remove these large binaries from every commit across `master`, `development`, `production`, and `ACBA`.
  - Ran `git gc --aggressive --prune=now` to purge old blob objects and reclaim local disk space.
  - Updated `.gitignore` to unconditionally exclude the entire `dist/` directory — installer binaries are now built locally on demand via `.\publish-installers.ps1` and never tracked in version control.
- **Branch Sync to `production` & `ACBA` (All Changes from `development`):**
  - Merged all `development` commits (security fixes, default modules, navigation hierarchy, Blazor UI component library) into `production` (commit `5d92e97`) and `ACBA` (commit `f6c62c8`).
  - Resolved two conflicts on `ACBA`:
    - `Attendance.razor`: Integrated `production`'s My Attendance link + 3-tab layout (Company Records, Biometric Import ACBA, Device Settings) while preserving `<BiometricImportHub>` and `HandleImportCompleted`.
    - `wwwroot/app.css`: Resolved by rebuilding Tailwind CSS v4 via `npm run css:build`.
- **ACBA Branch Published to GitHub:** `ACBA` is now a new remote branch at `origin/ACBA` — [create a pull request](https://github.com/BlirrHub/OfficeCore/pull/new/ACBA) if needed.
- **All 4 Branches Now Up to Date on GitHub:** `master`, `development`, `production`, and `ACBA` all successfully pushed after history rewrite.



### Enhanced & Unified — [2026-09-29] (Design System Standard & Blazor UI Component Library)

- **Blazor UI Component Library (`Components/UI/`):**
  - Designed and built a reusable, cohesive component library in `src/OfficeCore.Web/Components/UI/` to eliminate fragmented styling and ensure design unity across modules:
    - `PageHeader.razor`: Standardized page title, description, and dynamic `<Actions>` slot.
    - `AppButton.razor`: Consistent buttons supporting variants (`primary`, `secondary`, `danger`, `ghost`, `outline`), size tiers (`sm`, `md`, `lg`), subtle shadow glows, and integrated spinner loading state.
    - `AppCard.razor`: Dark glassmorphism container (`bg-zinc-950/80 border border-zinc-800/80 rounded-2xl shadow-xl`) with header, header actions, and footer slots.
    - `AppModal.razor`: Standard modal overlay with backdrop blur, customizable width constraints, entry animation, and structured header/body/footer.
    - `AppBadge.razor`: Unified status tags with pill borders and colored status dots (`primary`, `success`, `warning`, `danger`, `info`, `default`).
    - `EmptyState.razor`: Consistent empty state placeholder with modern iconography, typography, and call-to-action button slot.
  - Registered `OfficeCore.Web.Client.Components.UI` globally in `_Imports.razor`.
- **Page Modernization & Unification:**
  - Migrated `Advances.razor`, `MyLeaves.razor`, and `RoleManagement.razor` to leverage the new UI components, establishing visual consistency for headers, tables, modal forms, and badges.
- **System & Live Browser End-to-End Verification:**
  - Validated API runtime on `http://localhost:5026` with PostgreSQL 18.
  - Performed live authenticated HTTP session validation on `api/auth/login`, `api/organizations/my`, `api/organizations/{id}/select`, and `/api/auth/me`.
  - Verified backend endpoints: `api/boards`, `api/leaves/my`, `api/advances/my`, `api/attendance/today`, `api/clients`, `api/roles`, and `api/users` all returning 200 OK.
  - Recompiled Tailwind CSS v4 assets and verified Blazor compilation with 0 errors and 0 warnings.

### Enhanced & Reorganized — [2026-09-29] (Default Modules Isolation, Standalone My Attendance & Navigation Hierarchy Realignment)

- **Default Modules Configuration for All Accounts:**
  - Standardized **Dashboard**, **My Attendance**, **My Leaves**, and **Advances & Loans** as **Default Modules** available unconditionally to all authenticated accounts in an organization.
  - Removed `Dashboard`, `MyLeaves`, and `Advances` from `ModuleCategories` in `RoleManagement.razor`, preventing administrators from revoking core employee self-service capabilities.
  - Added an informative notice banner inside `RoleManagement.razor` clarifying default module access for roles.
  - Updated `OrganizationClaimsTransformation.cs` to inject default module claims (`Dashboard`, `MyAttendance`, `MyLeaves`, `Advances`) for all authenticated workspace members.
  - Updated `AuthController.cs` (`/api/auth/me`) to include default modules in the user profile response.
  - Hardened `CookieAuthStateProvider.cs` to ensure default module access claims are always present in the client-side `ClaimsPrincipal`.
  - Updated `Dashboard.razor` and `Advances.razor` page authorization directives from strict module policies to `@attribute [Authorize]`.
- **Standalone My Attendance Module (`MyAttendance.razor`):**
  - Extracted personal attendance tracking into a dedicated standalone Blazor page (`/my-attendance` and `/myattendance`), authorized for all accounts (`@attribute [Authorize]`).
  - Features real-time workstation digital clock, live CLOCKED IN / CLOCKED OUT status indicator, one-click Punch In/Out button, schedule details, and 4 personal metric cards (Hours Today, Days Present, Monthly Accumulated Hours, Scheduled Shift).
  - Includes personal punch log table with date-range filters, punch type filters (All, Clock In, Clock Out), and live SignalR auto-refresh via `attendanceHub`.
  - Provides conditional header link for managers to seamlessly navigate to Company Attendance.
- **Attendance Management Refactoring (`Attendance.razor`):**
  - Streamlined `Attendance.razor` (`/admin/attendance` and `/attendance`) into a dedicated Management Self-Service (MSS) module protected by `@attribute [Authorize(Policy = "RequireAttendance")]`.
  - Features two management tabs: **Company Records** (multi-employee punch logs, employee search, date filters, manual entry modal, and deletion) and **Device Settings** (biometric scanner integration and fingerprint enrollment).
  - Added direct header action linking managers back to their personal `My Attendance` dashboard.
- **Navigation Hierarchy & Grouping Realignment (`NavMenu.razor`):**
  - **Main Group:** Organized to host core daily workflows: `Dashboard` (default), `My Attendance` (default), `Boards` (`RequireBoards`), `My Leaves` (default), and `Advances & Loans` (default — moved under Main).
  - **Finance Group:** Gated by `RequireLedger`, `RequirePayroll`, or `RequireInvoices`: `Ledger`, `Payroll`, and `Invoices`.
  - **Administration Group:** Gated by administrative claims: `Clients` (moved under Administration), `Attendance` (moved under Administration), `Users`, `Roles`, `Leave Approvals`, `Work Schedules`, `Time Off & Holidays`, `Locations`, `Database Backups`, and `Audit Trail`.
  - Updated `Dashboard.razor` quick shortcuts, headcount links, and recent activity feeds to route personal actions to `/my-attendance` and team management to `/admin/attendance`.
  - Rebuilt solution with 0 errors and 0 compiler warnings, and recompiled Tailwind CSS v4 assets.

### Security & Bug Fixes — [2026-09-29] (Comprehensive Codebase Audit, Multi-Tenancy IDOR Hardening, SignalR Isolation & RBAC Parity)

- **Comprehensive Security & Bug Audit:**
  - Performed deep architectural and code review of all controllers, services, SignalR hubs, and Blazor pages across the entire repository.
  - Resolved git tracking case-collision by consolidating and merging `AGENTS.md` into `agent.md` and adding `.agents/rules/operating_rules.md` for native Antigravity IDE configuration loading.
- **Multi-Tenancy IDOR Vulnerability Patches:**
  - **`AdvanceRequestController.cs`:** Removed vulnerable unauthenticated `[HttpDelete("admin-cleanup")]` with `[AllowAnonymous]`. Enforced active tenant scoping on `ApproveRequest`, `RejectRequest`, and `DeleteRequest`. Guarded `GetActiveOrganizationId()` null references.
  - **`LeaveRequestController.cs`:** Enforced tenant validation in `UpdateStatus` and `DeleteRequest`. Added null-safe projections for employee user names to prevent runtime `NullReferenceException` crashes.
  - **`PayrollController.cs`:** Enforced tenant scoping on `DeletePayrollPeriod`, `ComputePayroll`, `GetPayslips`, `UpdatePayslip`, and `FinalizePeriod`. Fixed date-type comparison mismatch between `DateOnly` and `DateTime` in E-Loan deduction period calculations.
  - **`CompanyExpenseController.cs`:** Patched IDOR multi-tenancy leak in `DeleteExpense` by enforcing `e.OrganizationId == orgId.Value`.
  - **`AttendanceController.cs`:** Scoped `GetEmployeeFingerprints`, `DeleteFingerprint`, and `EnrollSuccess` to active organization. Replaced global SignalR broadcasts (`Clients.All`) with tenant-scoped group broadcasts (`Clients.Group($"org_{orgId}")`).
  - **`BoardsController.cs`:** Patched IDOR multi-tenancy vulnerabilities and added active tenant scoping to `CreateList`, `CreateCard`, `UpdateCard`, `MoveCard`, `DeleteList`, `DeleteCard`, `MoveList`, `CreateLabel`, `AddLabelToCard`, `RemoveLabelFromCard`, `AddComment`, `AddChecklist`, `AddChecklistItem`, `ToggleChecklistItem`, and `DeleteChecklist`. Fixed `targetEmployee == null` dereference in `AddMember`. Added comprehensive null-safe navigation for board creator, assignees, and comment user projections.
- **SignalR Real-Time Security Hardening:**
  - **`AttendanceHub.cs`:** Added `[Authorize]` attribute and automatic enrollment into the caller's organization group (`$"org_{orgId}"`) on connection, preventing unauthorized or cross-tenant live attendance event exposure.
  - **`BoardHub.cs`:** Added `[Authorize]` attribute and validated board tenant ownership in `JoinBoardGroup`, preventing unauthorized users or other organizations from subscribing to board updates.
  - **`ChatHub.cs`:** Replaced unsafe `Guid.Parse` with `Guid.TryParse` in `JoinRoom` to prevent unhandled 500 exceptions on malformed input.
- **RBAC & Module Policy Synchronization:**
  - **`OrganizationsController.cs`:** Added missing `"Backups"` and `"AuditLogs"` module access claims to newly created organization admin roles.
  - **`TimeOffPoliciesController.cs`:** Added `[Authorize(Policy = "RequireTimeOff")]` attribute to `CreatePolicy`, `UpdatePolicy`, and `DeletePolicy` to prevent regular employees from altering organization leave policies.
  - **`LocationsController.cs`:** Added `[Authorize(Policy = "RequireLocations")]` to `GetActiveFieldEmployees` to protect live employee GPS tracking data.
  - **`OfficeCore.Web/Program.cs`:** Added `"Backups"` and `"AuditLogs"` policies to Blazor WebAssembly authorization services. Enhanced `ApiBaseUrl` resolution to respect configured base URIs.
  - **Blazor Pages Authorization:** Updated `Backups.razor` and `AuditLogs.razor` to require `RequireBackups` and `RequireAuditLogs`. Updated `WorkSchedules.razor`, `RoleManagement.razor`, and `LeaveApprovals.razor` to use policy-based authorization (`RequireWorkSchedules`, `RequireRoles`, `RequireLeaveApprovals`).
- **Registration & Performance Improvements:**
  - **`AuthController.cs`:** Ensured `PhoneNumberConfirmed` is set to `false` upon user registration unless verified via phone OTP.
  - **`ChatController.cs`:** Eliminated N+1 query problem by replacing per-room `CountAsync` loop with a single grouped join query for unread chat message counts.
  - **Clean Compiler Build:** Resolved all warnings across the entire solution, achieving 0 compiler warnings and 0 errors. Rebuilt Tailwind CSS v4 assets.

### Added & Enhanced — [2026-09-29] (Free Email OTP Verification for User Registration)

- **Email OTP Verification Engine (`EmailOtpService.cs`, `AuthController.cs`, `SignUp.razor`, `appsettings.json`):**
  - Switched the user registration verification flow from SMS to **Email OTP** to eliminate recurring cellular gateway fees.
  - Implemented `IEmailOtpService` / `EmailOtpService` with support for standard SMTP (e.g., free Gmail App Password, Resend, Brevo) with an HTML email template and automatic simulation fallback for offline LAN environments.
  - Updated `SignUp.razor` so users verify their email address via a 6-digit code before account creation, while retaining Mobile Number as an optional/standard user profile contact field without requiring SMS verification.
  - Updated `AuthController.cs` to enforce email verification during `POST /api/auth/register` and set `EmailConfirmed = true`.

### Fixed & Enhanced — [2026-09-29] (Registration Phone OTP Verification & Browser Password Eye Parity)

- **Registration Phone OTP Lifecycle Fix (`SmsOtpService.cs`, `AuthController.cs`):**
  - Resolved `Registration failed. Invalid or expired phone verification code.` error occurring after successful OTP code verification.
  - Added `_verifiedPhones` cache with 15-minute validity window in `SmsOtpService` and `IsPhoneVerified` check in `AuthController.Register` so users can proceed seamlessly to account creation immediately after verifying their phone code.
- **Duplicate Password Reveal Eye Fix (`app.css`):**
  - Suppressed browser native password reveal icon (`::-ms-reveal` and `::-ms-clear`) so Edge and Chromium browsers only render the single custom styled visibility toggle icon.

### Added & Enhanced — [2026-09-29] (Simplified Minimalist Welcome Screen)

- **Minimalist Welcome Landing Page (`Home.razor`):**
  - Re-designed the landing page into a clean, unexaggerated welcome screen with a scrollable design.
  - Simplified the layout to focus on clean branding, quick "Sign In" / "Create Account" or "Continue to Workspace" call-to-actions, and straightforward feature cards (Time & Attendance, Payroll Engine, Tasks & Collaboration).
  - Maintained smooth scrollability and theme parity (dark and light modes).

### Added & Enhanced — [2026-09-29] (On-Premises Client Branching Strategy & ACBA Biometric Importer Extraction)

- **On-Premises Client Branching Model (`ACBA` Branch):**
  - Established a dedicated per-client branching strategy for on-premises client customization.
  - Created a dedicated `ACBA` client branch containing the customized biometric attendance importer (`BiometricImportHub.razor`) and specialized import tabs.
- **Base Branches Modernization (`development`, `master`, `production`):**
  - Removed specialized ACBA attendance import tab and modal markup from `Attendance.razor`.
  - Removed `BiometricImportHub.razor` from base branches, keeping the core product lean, standardized, and ready for general distribution without niche client dependencies.

### Added & Enhanced — [2026-09-29] (Minimalist Landing Page, Sign In/Up Polish, Phone OTP Verification & Confirm Password)

- **Minimalist Modern Landing Page (`Home.razor`):**
  - Revamped the primary landing page with a modern minimalist aesthetic: subtle glassmorphic panels, refined typography, ambient radial illumination, and responsive desktop/mobile navigation.
  - Added a live application browser preview window showcasing real-time attendance, net payroll calculations, and hardware biometric status.
  - Added operational feature pillars (Biometric Hardware, Philippine Payroll Engine, and Kanban Boards with WebSockets) and quick metrics highlights.
- **Enhanced Sign In UI (`Login.razor`):**
  - Refined the Sign In interface with minimalist glassmorphic cards, improved spacing, quick demo access chips (Admin & Employee), subtle theme parity, and quick Google OAuth integration.
- **Enhanced Sign Up UI & Flow (`SignUp.razor`):**
  - Added **Phone Number OTP Verification**: users input their mobile number and request a 6-digit verification code. Displays real-time validation, resend timers, and a convenient simulation auto-fill helper for development and local network workstations.
  - Added **Confirm Password** field with real-time match validation before submission.
  - Maintained sleek minimalist styling with dual-theme compatibility (Dark and Light modes).
- **Backend SMS OTP Engine (`SmsOtpService.cs`, `AuthController.cs`, `Program.cs`):**
  - Implemented `ISmsOtpService` and `SmsOtpService` with thread-safe in-memory caching, 5-minute expiration windows, and phone number normalization.
  - Added `POST /api/auth/send-otp` and `POST /api/auth/verify-otp` endpoints.
  - Updated `POST /api/auth/register` and `RegisterRequest` to validate phone number and OTP code upon account creation.


- **In-App Media & Document Viewer Modal (`MediaViewerModal.razor`, `fileInterop.js`, `index.html`):**
  - Created a reusable, high-performance modal component (`MediaViewerModal.razor`) for viewing images and documents inline without leaving the app.
  - Features smooth image zoom controls (zoom in, zoom out, reset), embedded PDF document viewer iframe, file metadata headers, keyboard Escape key dismiss, and a direct "Download File to Desktop" button via `window.fileInterop.downloadFile()`.
  - Integrated into **Sick Leave** medical attachments (`LeaveApprovals.razor`, `MyLeaves.razor`), replacing broken dead-port links (`:5033`) with responsive click-to-preview thumbnails.
  - Integrated into **Board Card Attachments** (`BoardView.razor`), allowing team members to preview attached card assets (images, receipts, PDFs) and download them to local disk with one click.
- **Chat File & Document Attachments (`ChatMessage.cs`, `ChatController.cs`, `GlobalChatWidget.razor`):**
  - Extended `ChatMessage` entity and database schema with `AttachmentUrl`, `AttachmentName`, and `AttachmentSize` via EF Core migration `AddChatAttachments`.
  - Added `POST /api/chat/rooms/{roomId}/attachment` endpoint supporting images (`.jpg`, `.png`, `.webp`, `.gif`), documents (`.pdf`, `.txt`, `.doc`, `.docx`, `.xls`, `.xlsx`) up to 25MB.
  - Enhanced `GlobalChatWidget.razor` with an attachment upload button (paperclip icon), pending attachment preview banner with remove option, inline image thumbnails with hover preview button, and document chips showing file name and size.
  - Integrated `MediaViewerModal` in chat so clicking any attached image or PDF opens the full-screen interactive viewer with desktop download capability.
- **Client Workstation Avatar & Profile Picture LAN Resolution Fix (`Program.cs`, `MediaUrlHelper.cs`):**
  - Resolved root cause of broken profile pictures and group chat images on client workstations: `appsettings.json` previously supplied `ApiBaseUrl` as `localhost:5026`, which caused client browsers/workstations on the LAN to request uploaded media from their own local machine instead of the host server.
  - Configured `Program.cs` in `OfficeCore.Web` to dynamically bind `builder.Configuration["ApiBaseUrl"] = builder.HostEnvironment.BaseAddress`, guaranteeing all uploaded images and avatars correctly point to the server host regardless of LAN IP or hostname.
  - Created `MediaUrlHelper.cs` to resolve relative upload paths and data URLs safely across components.
- **Workspaces Page Cleanup (`Workspaces.razor`):**
  - Removed redundant "Join with invite" card from `Workspaces.razor` workspace selection grid since an action button already exists in the header.

### Fixed — [2026-09-28] (Chat Double-Message Bug & Currency Symbol Parity to ₱)

- **Chat Double Message Rendering Bug Fixed (`GlobalChatWidget.razor`):**
  - Resolved race condition where sending a message caused 2 duplicate chat bubbles to appear in the sender's active conversation window.
  - Implemented dual-stage deduplication:
    - Updated SignalR `ReceiveMessage` handler to detect incoming messages from the current sender and automatically replace/remove the temporary optimistic message.
    - Updated `SendMessage()` to only add the HTTP response message if SignalR has not already appended it, and cleaned up optimistic instances on network failure.
    - Applied `.DistinctBy(m => m.Id)` on the message iteration loop as a fail-safe against any duplicate ID rendering.
- **Philippine Peso Currency Symbol Parity (`Advances.razor`, `Ledger.razor`, `Program.cs`):**
  - Replaced hardcoded `$` signs with Philippine Peso (`₱`) across Cash Advance and E-Loan request amount inputs, expense creation inputs, and loan approval installment inputs in `Advances.razor` and `Ledger.razor`.
  - Configured `CultureInfo.CurrentCulture` and `CultureInfo.CurrentUICulture` to `en-PH` alongside `DefaultThreadCurrentCulture` in `Program.cs` to ensure consistent `₱` formatting across all WebAssembly asynchronous continuations.

### Updated — [2026-09-28] (Production Server & Client Installers Rebuilt)

- **Production Server & Workstation Installers Rebuilt (`dist/Setup-OfficeCore-Server.exe`, `dist/Setup-OfficeCore-Client.exe`):**
  - Recompiled and packaged standalone Windows installers and portable distributions in `dist/` incorporating all recent features:
    - Biometric Attendance Excel log importer for ACBA company workflow
    - Audit Trail logging and compliance ledger
    - Automated Database Backup & 1-Click Disaster Recovery console
    - Role-based modular permissions for Backups and Audit Logs
    - Dark mode visual parity fixes and chat group drawer UI polish
  - Updated production release binaries in `dist/` and synchronized across all deployment branches (`development`, `master`, and `production`).

### Added & Fixed — [2026-09-28] (Biometric Attendance Excel Importer, Chat Group Info UI & Backup Card Spacing)

- **Biometric Attendance Excel Log Importer (`BiometricReportService.cs`, `AttendanceController.cs`, `BiometricImportHub.razor`, `Attendance.razor`):**
  - Integrated `ExcelDataReader` (3.9.0) enabling cross-platform, zero-dependency parsing of legacy `.xls` (BIFF8) and `.xlsx` biometric time-attendance files.
  - Designed dedicated **Biometric Import Hub** component (`BiometricImportHub.razor`) cleanly separated from the built-in workstation clock and direct USB/Wi-Fi hardware sensors, specifically tailored for company workflows like **ACBA** that export logs from standalone terminals (`All Report.xls`).
  - Added intelligent punch parser extracting date windows, employee blocks (`ID :`, `Name :`), and day column punches, automatically pairing timestamps into `ClockIn` and `ClockOut` sequences while visually highlighting days with missing pairs (e.g. In without Out) so supervisors can cross-reference CCTV records.
  - Implemented employee matching engine checking `Employee.EmployeeId` first, falling back to name heuristics, and providing an interactive profile mapping dropdown with an option to permanently link and save the biometric device ID to employee profiles.
  - Added REST API endpoints (`POST /api/attendance/import-biometric-preview` and `POST /api/attendance/import-biometric-commit`) with duplicate punch skipping, SignalR live broadcast, and audit ledger tracking.
  - Added dedicated **"Biometric Import (ACBA)"** tab under `Attendance.razor`.

- **Chat Group Info UI Overhaul (`GlobalChatWidget.razor`):**
  - Resolved visual overlap between the floating camera button, the misplaced "✕" button, and the "Change Photo" label in the group details drawer.
  - Re-architected drawer layout with a dedicated top header bar, centered avatar card with clear action buttons, and spaced member list toolbar preventing "+ Add Colleagues" from colliding with "Group Members".

- **Database Backups Summary Card Spacing Parity (`Backups.razor`, `Styles/app.css`):**
  - Resolved 0px spacing between icons and metric text in `Backups.razor` by applying explicit margin utilities (`mr-3.5`) and updating CSS template scanning with `@source "..";` in `Styles/app.css`.
  - Recompiled Tailwind CSS v4 assets (`npm run css:build`).

### Added & Fixed — [2026-09-28] (Audit Trail Dark Mode Parity & RBAC Modularization for Backups & Audit Trail)

- **Audit Trail Dark Mode Theme Parity (`AuditLogs.razor`, `app.css`):**
  - Resolved blinding white card backgrounds and black borders when viewing Audit Trail in Dark Mode by replacing hardcoded `bg-white dark:bg-zinc-900` classes with native OfficeCore zinc token system (`bg-zinc-900/60`, `border-zinc-800/80`, `bg-zinc-950`).
  - Standardized badge tinting across all categories, filters, pagination controls, and the JSON diff modal to ensure crystal-clear contrast and visual harmony across both Dark Mode and Light Mode (`html.light`).
  - Recompiled Tailwind CSS v4 assets (`npm run css:build`).

- **Granular RBAC Modularization for Database Backups & Audit Trail (`Program.cs`, `DbSeeder.cs`, `RoleManagement.razor`, `NavMenu.razor`):**
  - Elevated **"Database Backups"** (`Backups`) and **"Audit Trail"** (`AuditLogs`) to official modular permission claims under the `Administration` category.
  - Added module authorization policies (`RequireBackups`, `RequireAuditLogs`) in `OfficeCore.Api/Program.cs`.
  - Added Backups and Audit Trail toggles to the Role Permissions Matrix in `RoleManagement.razor` allowing workspace owners to grant or revoke granular administrative access to custom and department roles.
  - Implemented automatic database seeder startup migration in `DbSeeder.cs` ensuring all existing workspace `Admin` and `Owner` roles are backfilled with the new `Backups` and `AuditLogs` claims without requiring manual role re-creation.
  - Gated navigation menu entries in `NavMenu.razor` with `context.User.HasClaim("ModuleAccess", "Backups")` and `context.User.HasClaim("ModuleAccess", "AuditLogs")`.

### Added — [2026-09-28] (Pillar 2: Immutable Audit Trail Logging System & Compliance Ledger)

- **Comprehensive Multi-Tenant Audit Trail Architecture (`AuditLog.cs`, `AuditService.cs`, `AppDbContext.cs`, `AuditController.cs`):**
  - Designed and implemented multi-tenant `AuditLog` database entity implementing `ITenantEntity` with high-performance composite database indexes on `(OrganizationId, CreatedAt)`, `(OrganizationId, EntityName)`, and `(OrganizationId, Action)`.
  - Added EF Core database migration (`AddAuditLogsTable`) and executed schema updates on the active PostgreSQL database.
  - Implemented scoped `IAuditService` / `AuditService` extracting actor identity, IP address, and workstation/client signatures (`X-OfficeCore-Client` / `X-OfficeCore-Station` / `User-Agent`) from `IHttpContextAccessor` and `TenantService`.
  - Added structured before-and-after state capture (`OldValues` and `NewValues` serialized to JSON) for deep diff inspection of sensitive modifications.
  - Built administrative audit REST API endpoints (`GET /api/audit`, `GET /api/audit/summary`, `GET /api/audit/export`) supporting multi-criteria filtering by action, category, date range, and keyword search, along with CSV export streaming.

- **Universal Business Operation & Mutation Instrumentation:**
  - **Payroll Operations (`PayrollController.cs`):** Instrumented draft period creation, payslip re-computations, manual allowance/deduction modifications (with before-and-after net pay diffs), period finalization with total payout summaries, and period deletion.
  - **Employee & Compensation Rates (`UserController.cs`):** Instrumented new employee creation, profile and work schedule updates, daily wage rate modifications (tracking old rate vs new rate), and employee resignation/termination.
  - **Leave Management (`LeaveRequestController.cs`):** Instrumented leave status transitions (`Approved`, `Rejected`), admin-directed leave grants, and leave request cancellations.
  - **Cash Advances & Loans (`AdvanceRequestController.cs`):** Instrumented loan approvals, rejections, and direct admin creation.
  - **Attendance Punches (`AttendanceController.cs`):** Instrumented manual punch creations, timestamp adjustments, and punch deletions.
  - **Database & Disaster Recovery (`BackupController.cs`):** Instrumented manual database snapshot creations, snapshot deletions, and emergency/safety database restores.

- **Web Audit Trail & Compliance Console (`AuditLogs.razor`, `NavMenu.razor`):**
  - Created a compliance dashboard under `/admin/audit-logs` featuring 4 executive metric cards: Total Audit Events, Today's Operations, Financial & Payroll Modifications, and Security & Backup Actions.
  - Implemented filter toolbar with Category Tabs (`All`, `Payroll`, `Employee & Rates`, `Attendance`, `Leaves`, `Advances`, `Backups`, `Security`), Action Type dropdown, Date Range pickers, and live keyword search.
  - Implemented interactive ledger table displaying timestamp, actor avatar and IP address, color-coded action badges, category chips, and event descriptions.
  - Built Audit Event Deep-Inspection Modal with side-by-side JSON diff comparison for modified before-and-after records.
  - Added one-click **"Export CSV Ledger"** streaming compliant audit reports directly to the user's browser.
  - Added "Audit Trail" navigation link in `NavMenu.razor` under the Admin section.

### Added — [2026-09-28] (Pillar 1: Automated Database Backup & 1-Click Restore Disaster Recovery System)

- **Comprehensive Database Backup & Disaster Recovery Architecture (`BackupService.cs`, `BackupHostedService.cs`, `BackupController.cs`):**
  - Designed and implemented automated PostgreSQL custom-format (`.dump`, `-F c -b -v`) backup engine dynamically resolving PostgreSQL binary toolchains (`pg_dump.exe`, `pg_restore.exe`) from `PATH`, `Program Files\PostgreSQL\*\bin`, and custom path overrides.
  - Added background worker service (`BackupHostedService`) running every 30 seconds to execute scheduled nightly/daily automated backups at configurable times without interrupting or blocking concurrent API requests.
  - Implemented automatic **Pre-Restore Safety Snapshots** (`prerestore_*.dump`) executed immediately before any database restoration (from local snapshot or user-uploaded file) guaranteeing zero catastrophic data loss.
  - Implemented configurable retention policies automatically pruning older routine dumps while permanently safeguarding manual labels and emergency safety snapshots.
  - Exposed full administrative REST API endpoints: `GET /api/backup` (snapshot inventory & metadata), `POST /api/backup/create` (on-demand manual snapshot), `GET /api/backup/{fileName}/download` (archive file stream), `POST /api/backup/upload` (accepts up to 500MB `.dump`/`.sql` archives), `POST /api/backup/{fileName}/restore` (1-click safety restore), `DELETE /api/backup/{fileName}`, and `GET/PUT /api/backup/settings`.
  - Added internal loopback station authentication bypass allowing host Server Station orchestrator direct administrative snapshot creation.

- **Web Disaster Recovery Console (`Backups.razor`, `NavMenu.razor`):**
  - Built an administrative dashboard under `/admin/backups` featuring real-time health indicator cards (Engine Status & pg_dump detection, Total Snapshots, Storage Footprint in MB/GB, Automated Schedule Status).
  - Implemented search and category filter toolbar (`All`, `Automated`, `Manual`, `Safety Snapshots`).
  - Added interactive dialogs for Manual Snapshot Creation (with custom descriptive labels), Safety-Gated Restore Wizard (with safety warnings and explicit confirmation checkbox), Upload & Restore Modal (supporting drag-and-drop `.dump`/`.sql`), and Backup Schedule Settings (enabling/disabling auto backups, setting backup time, and retention limit).
  - Added direct browser file download button to export `.dump` archives off-machine.
  - Added "Database Backups" navigation entry with database cylinder icon under Admin section in the main sidebar.

- **Desktop Server Station Host Controls (`OfficeCore.Desktop.Server/MainWindow.xaml`, `MainWindow.xaml.cs`):**
  - Integrated dedicated "Database Backups & Snapshots" host card within the Server Station dashboard below PostgreSQL configuration.
  - Added "📦 Quick Snapshot" button triggering immediate local snapshot creation with live status feedback.
  - Added "🛠️ Open Console" button launching the full Web Disaster Recovery Console directly inside the station's embedded Edge WebView2 host.

### Updated — [2026-09-28] (Office Test-Run Foundation & Strategic Roadmap Prioritization)

- **Prioritized On-Premises Office Test-Run Triad (`ROADMAP.md`):**
  - Aligned core engineering focus on 3 critical pre-deployment pillars: (1) Automated Database Backup & 1-Click Restore, (2) Audit Trail Logging System, and (3) Individual Payslip Printable/PDF Export & Payroll Summary Excel Export.
  - Formally locked architecture to a single unified codebase with tenant configuration/feature toggles (abandoning risky branch-per-client bifurcation).
  - Outlined silent PostgreSQL installer check for Server Station and long-term General Ledger / Chart of Accounts accounting evolution.

### Added — [2026-09-28] (Group Chat Profile Picture & Avatar Management)

- **Group Chat Avatar Upload & Picture Management (`GlobalChatWidget.razor`, `ChatController.cs`, `ChatRoom.cs`):**
  - Added `PictureUrl` property to `ChatRoom` with EF Core database migration (`AddChatRoomPictureUrl`).
  - Added dedicated Group Profile Picture changing and removal in the **Group Details Drawer** with an interactive avatar card, hover upload overlay, floating quick-action camera button, and progress indicators.
  - Added optional Group Photo picker directly within the **New Group Chat** creation modal, supporting real-time local image preview, cancellation, and automatic upload upon room creation.
  - Integrated `POST /api/chat/rooms/{roomId}/picture` supporting JPG, PNG, WEBP, and GIF up to 5MB with automatic server-side storage under `wwwroot/uploads/chat/`.
  - Integrated `DELETE /api/chat/rooms/{roomId}/picture` allowing group members to remove custom pictures and revert back to initials gradients.
  - Implemented real-time SignalR broadcast event `RoomPictureUpdated` updating avatars across the rooms list and active chat headers instantly for all group members without requiring page reloads.
  - Updated room cards in the chat list and active conversation headers to display group photos with custom borders and shadows.

### Added — [2026-09-28] (Group Chat Creation, Member Management & Real-Time Collaboration)

- **Group Chat Creation & Multi-User Management (`GlobalChatWidget.razor`, `ChatController.cs`):**
  - Added dedicated **"New Group Chat"** creation flow accessible via the chat header button (`👥+`) and tabbed dual-mode creation modal.
  - Implemented real-time colleague search with multi-select checkboxes, dynamic member count badges, and removable colleague pill tags.
  - Added new group chat validation requiring a group name (up to 100 characters) and at least 2 active members within the active workspace.
  - Added **Room Type Filters** (`All`, `Direct`, `Groups`) with real-time badges to swiftly organize active direct messages and group conversations.
  - Added dedicated **Group Chat Cards** featuring gradient group icons, member count chips, group badges, and sender name prefixes on latest message previews.
  - Added **Group Chat Header & Subtitle** displaying participant names, group initials avatar, and member count badge.
  - Added **Group Details Drawer** showing full member lists with `(You)` indicators, "Add Colleagues" sub-panel with real-time search, and "Leave Group" confirmation dialog.
  - Enhanced group messaging view with sender name headers in bold accent color (`text-fuchsia-400` / `text-fuchsia-600`) for incoming group messages.
  - Added group-aware typing indicator displaying specific member names (`Alice is typing...`).

- **Backend Group Chat Endpoints & SignalR Broadcasts (`ChatController.cs`):**
  - Added `POST /api/chat/group`: Creates a new `ChatRoom` of type `ChatRoomType.Group` with multi-employee membership (`ChatRoomMember`), strictly scoped to active `OrganizationId`, and notifies all participants via SignalR (`RoomCreated`).
  - Added `POST /api/chat/rooms/{roomId}/members`: Adds colleagues to an existing group chat, validating tenant isolation and active caller membership.
  - Added `POST /api/chat/rooms/{roomId}/leave`: Allows an employee to leave a group chat, removing membership and updating remaining participants.
  - Added `PUT /api/chat/rooms/{roomId}/name`: Allows renaming a group chat and pushes real-time title updates across all group members.

### Updated — [2026-09-27] (Standalone Server Station Installer Refresh)

- **Updated Standalone Server Station Setup Installer (`dist/Setup-OfficeCore-Server.exe`):**
  - Rebuilt and packaged the release distribution of `OfficeCore.Desktop.Server` and `OfficeCore.Server.Installer` into `dist/Setup-OfficeCore-Server.exe`.
  - Updated the production-tracked server setup wizard binary while keeping `Setup-OfficeCore-Client.exe` intact.

### Added & Improved — [2026-09-27] (Desktop-Friendly Workspace Invitation Flow & Join-by-Code System)

- **Desktop-Friendly Workspace Invitation Modal & Token Parser (`Workspaces.razor`):**
  - Added dedicated **"Join with invite"** card in the workspace directory grid alongside "Create workspace", enabling team members operating strictly within the desktop app to join organizations without external web browsers.
  - Added **"Join with invite"** action button in the workspaces header with emerald theme styling.
  - Implemented an interactive Join Workspace modal supporting raw 32-character tokens, `/join/<token>` URLs, and `?code=` / `?token=` query parameters with intelligent parsing (`ExtractToken`).
  - Integrated live invitation verification (`GET /api/organizations/invitations/{token}`) displaying organization initials, name, and inviter name in a verified preview card prior to joining.
  - Implemented one-click **"Enter Workspace"** (`ConfirmJoin`) which joins the organization (`POST /api/organizations/join`), activates it via cookie selection (`POST /api/organizations/{id}/select`), and routes seamlessly to `/dashboard`.
  - Added support for query parameters (`/workspaces?join=true`, `/workspaces?code=...`, `/workspaces?token=...`) with automatic modal launch and instant verification.

- **Desktop-Centric Invite Code Generation & Dual Copy Buttons (`UserManagement.razor`):**
  - Updated workspace invitation dialog in User Management to provide an **Invite Code (for Desktop App)** with dedicated "Copy Code" button alongside the full shareable invite link.
  - Added helper guidance informing administrators that team members can paste the code directly inside the Desktop App under **Workspaces → Join with invite**.
  - Added clipboard integration with animated "✓ Copied" feedback and toast alerts.

- **Workspace Navigation & Switcher Integration (`MainLayout.razor`, `Join.razor`):**
  - Added **"Join with Invite Code"** shortcut in the workspace switcher dropdown in `MainLayout.razor`, routing directly to `/workspaces?join=true`.
  - Added `@page "/join"` route in `Join.razor` that automatically forwards requests without a route parameter directly to `/workspaces?join=true`.

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
