# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- **Landing Page**: Implemented a modern landing page in `Home.razor` with a Hero section, Features list, and Call-to-Action.
- **Workspaces Dashboard**: Created `/workspaces` page for organization selection. Users land here after logging in or signing up.
- **SignUp Page**: Created a new registration flow to capture `FirstName`, `LastName`, `Email`, and `Password`. Users are now signed in automatically and redirected to their workspaces.
- **Organization Switcher**: Added a dropdown in the `MainLayout.razor` top bar to seamlessly switch between different active organizations.
- **Organizations API**: Implemented `/api/organizations/my`, `/api/organizations`, and `/api/organizations/{id}/select` to manage the multi-tenant scope and context.

### Changed
- Refactored `Home.razor` into `Dashboard.razor` mapping it to `/dashboard`.
- Redesigned `Login.razor` with a more modern UI and linked to the new SignUp page and Google Auth placeholder.
- Updated `AuthController` to include the `/register` endpoint and sign-in functionality upon registration.
- Added `ActiveOrganizationId` persistence via cookies on the backend when an organization is selected.
### Added
- **Multi-Tenant Architecture Refactoring**: Refactored the core database schema to support Organizations/Companies. Transitioned `IdentityUser` to a global `ApplicationUser` and created a new junction `Employee` model. All entities in the system now implement `ITenantEntity` and include `OrganizationId` to isolate data between tenants.
- **Global Chat System**: Added a real-time Chat System with a global floating Chat Bubble, supporting direct 1-on-1 messaging, unread badges, typing indicators, and instant real-time message delivery via SignalR.
- **Member Avatars**: Kanban boards now display the profile pictures of the top members inside the board summary cards.
- **Board Profile Pictures**: The individual Kanban board view now properly displays profile pictures for members instead of placeholder initials.
- **Profile Avatars**: Users can now upload and display a custom profile picture. The profile image integrates seamlessly into the global sidebar and the overhauled profile page.
- **Profile Redesign**: Upgraded the "My Profile" page with a refined layout that includes a dynamic avatar upload dropzone and better visual hierarchy.
- **Board Pinning**: Users can now star/pin specific boards on their dashboard to keep them locked at the very top of their project list.
- **Card Rearrangement**: Fully enabled precise card reordering by dragging and dropping a card directly onto another card, supporting sorting both within the same list and across different lists.
- **Due Date Indicators**: Card due dates are now color-coded dynamically (Red for overdue/today, Amber for tomorrow, Zinc for future) complete with an exclamation mark warning icon for critical dates.
- **Checklist Deletion**: Users can now fully delete checklists from cards using the new trash icon in the checklist header.
- **Board Manual Refresh**: Added a manual "Refresh" button to the Boards index page to load newly assigned boards without requiring a full browser tab reload.
- **Boards Module (Kanban)**: Implemented a robust Trello-style Kanban board system featuring native HTML5 drag-and-drop for sorting lists and moving cards across lists.
- **Role-Based Board Access**: Introduced Board-level roles (Owner, Member, Viewer). Owners can fully manage lists, cards, and members; Members can add/edit cards; Viewers have read-only access.
- **Board Deletion**: Board Owners can permanently delete entire project boards directly from the UI.
- **Add Card / Add List**: Fully functional interface to quickly create new categorized workflow lists and individual task cards.

### Fixed
- **Kanban Event Bubbling**: Fixed an issue where dragging an individual card would improperly bubble up and trigger the drag event for the entire parent List.
- **Viewer Role Enforcement**: Hid the "Add Card" button and functionally disabled card dragging capabilities for board Viewers to ensure strictly read-only access.
- **Board Member Role Serialization**: Fixed a bug where assigning "Viewer" or "Member" roles to a new user inadvertently elevated their permissions to "Owner".
- **Board Flash/Flicker**: Implemented smart debouncing for SignalR updates to completely eliminate UI flashing for the person actively dragging/dropping cards.
- **Is Employee Toggle**: Added an `IsEmployee` toggle to User Management. Users with this disabled (like administrative accounts) are safely excluded from the payroll computation engine, and payroll-related form fields are dynamically hidden.
- **Weekly Rate (No Attendance)**: Added a "Pay Mode" toggle for Fixed Work Schedules. Selecting "Weekly Rate" hides the required start/end time inputs and instructs the payroll engine to automatically calculate basic pay based solely on the number of scheduled work days for the period, completely ignoring any required attendance logs.
- **Default Passwords**: Removed the password input field when creating an employee. New accounts now default to the password `"password"` and global identity complexity requirements have been relaxed to allow this.
- **Forced Password Update**: Added a `MustChangePassword` security flag for newly created accounts. When logging in for the first time, users are now automatically trapped in the Profile page and blocked from accessing any other system tabs until they successfully change their default password.
- **Change Password Form**: Added a functional "Change Password" interface directly into the user Profile page.
- **Detailed Registration Errors**: Updated the User Management modal to catch and explicitly display detailed API validation errors (e.g. "Invalid Email") instead of generic failure messages.
- **Auto Clock Out**: Added an "Auto clock out at end of shift" toggle to Work Schedules.
- **Time-Tracked Payroll Engine**: Radically overhauled the payroll computation logic. The system now parses raw `ClockIn` and `ClockOut` pairs for the payroll period, correctly deducts overlapping unpaid breaks from the employee's `WorkSchedule`, and derives standard `BasicPay` strictly from these tracked hours using the computed `HourlyRate` (DailyRate / 8). Hours logged on rest days are cleanly ignored.

### Changed
- **Leave Pay Restored**: Restored the "Leave Pay" column to the main Payroll Details table per user request.

### Removed
- **Cutoff Date**: Dropped the `CutoffDate` property from the database and UI, since standard Start/End Dates represent the period naturally.
- **Undertime/Late Deductions**: Deprecated these separate fields from Payslips since the raw time-tracking calculation implicitly accounts for undertime.



### Added
- Added Sick Leave / Vacation Leave eligibility and Work Schedule assignment to User Management
- Added Daily Rate tracking directly in the Employee profile
- Created Advances & Loans module (for Cash Advances and E-Loans)
- Created Ledger module for employee deductions and additions tracking
- Implemented global minimum rate configuration in Payroll Settings
- Added support for resigned employee tracking (`ResignedDate`, active status)

### Changed
- Reorganized Add/Edit Employee modals for better UX (grouped standard inputs, separated toggles)
- Changed sick leave calculation toggle to explicitly use personal Daily Rate vs. global minimum
- Adjusted `Employee` model to support `DateHired`, `ResignedDate`, `IsEligibleForLeave`, `UseCustomDailyRate`, and `DailyRate`
- Registered `Advances` and `CompanyProfile` as full system modules
- Redesigned sidebar layout: made it collapsible, moved Locations and Leave Approvals to the Main category, and improved Profile/Notification placement.
- Refined collapsed sidebar UI: replaced category headers with horizontal dividers and hid link texts.

### Fixed
- Fixed User Management Add/Edit modals overflowing vertically by implementing scrollable inner containers and `max-h-[90vh]` constraints.
- Fixed layout mismatch issue in User Management modals
- Ensured sick leave logic uses correctly scoped Daily Rate
- Fixed profile button alignment and overflow issues when sidebar is collapsed
- Replaced default scrollbars with a custom dark-themed design globally

## [Previous Milestones]

### Core System
- Configured ASP.NET Core Identity with custom `Employee` and `AppRole` entities
- Implemented JWT / Cookie-based Authentication for Blazor WebAssembly
- Created dynamic role-based and claims-based authorization (`NavMenu` protected routes)
- Configured Entity Framework Core with PostgreSQL

### Attendance & Time Tracking
- Developed real-time Attendance tracking using SignalR (`AttendanceHub`)
- Created Fingerprint Device Service integration (`ZKteco` support over COM ports)
- Added interactive map to Attendance Dashboard to track remote check-ins
- Built comprehensive Work Schedules module with features for:
  - Complex break rules and paid/unpaid conditions
  - Overtime multipliers and trigger modes
  - Auto-deductions for tardiness or early exits
  - Support for rest days and specific shift timings

### Human Resources
- Created Time Off & Holidays management (Company-wide Holiday Calendar)
- Built Leave Request system (`MyLeaves` for employees, `LeaveApprovals` for admins)
- Developed complete User Management dashboard (Create/Edit employee profiles, assign roles, track government IDs)

### Finance & Payroll Prep
- Implemented base Payroll Settings including SSS, PhilHealth, and Pag-IBIG config tables
- Built Clients and Invoices tracking modules
- Prepared base schema for Payroll Generation


### Updates
- Added Advances, My Leaves, and Leave Approvals as configurable Role modules.
- Grouped Role modules by category in Edit Role dialog.
- Implemented dynamic Sidebar rendering to hide empty categories based on user module access.
- Fixed UTC storage issues for Leave Requests and Attendance records.
- Added Delete capability to Leave Approvals and Attendance log for development purposes.
- Standardized UI layouts across the `Advances`, `Ledger`, `WorkSchedules`, and `TimeOff` modules to ensure a consistent, premium deep-dark aesthetic aligning with the `Dashboard` and `Attendance` designs.
