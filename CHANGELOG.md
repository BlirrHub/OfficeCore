# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]
### Added
- **Auto Clock Out**: Added an "Auto clock out at end of shift" toggle to Work Schedules.
- **Time-Tracked Payroll Engine**: Radically overhauled the payroll computation logic. The system now parses raw `ClockIn` and `ClockOut` pairs for the payroll period, correctly deducts overlapping unpaid breaks from the employee's `WorkSchedule`, and derives standard `BasicPay` strictly from these tracked hours using the computed `HourlyRate` (DailyRate / 8). Hours logged on rest days are cleanly ignored.

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

