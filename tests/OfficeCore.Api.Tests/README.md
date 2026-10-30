# API service regression tests

Run from the repository root on Windows with the .NET 9 SDK:

```powershell
dotnet test OfficeCore.slnx -c Release -m:1 -nr:false
```

`OfficeCore.Api.Tests` exercises the actual payroll service, EF entity model, tenant resolver and notification hub. Each test creates a unique EF in-memory database. No application database, demo seeder, migrations, existing uploads or fingerprint hardware is accessed. Notification tests record actual hub group subscriptions using a test connection and real Identity user resolution.

Payroll tests use explicit UTC+8 and UTC-8 office timezones, independent of the machine's timezone. Normal application construction still defaults to the server's local timezone. Leave and overtime inputs use the UTC-midnight calendar-date representation written by the existing controllers; attendance inputs are real timestamp instants.

Coverage includes unpaid breaks, early arrivals, unapproved/approved overtime, midnight cutoff punches, incomplete punch pairs, fixed-rate workdays/rest days, leave spanning payroll cutoffs, configured government deductions, remaining advance balances, recomputation, finalized/paid locks, inactive staff and cross-workspace records. Notification coverage includes foreign workspace cookies, inactive memberships, ordinary members, owners and missing workspace selection.

On 2026-10-30, seven new regression cases failed before fixes: two fixed-rate weekday cases, two leave cutoff cases and three notification membership cases. After fixes and additional boundary coverage, all 26 API tests and 14 authorization/deployment tests passed.

These are service/component tests, not HTTP, browser or PostgreSQL integration tests. They do not establish SQL translation, migration correctness, transaction/concurrency behavior, legal payroll compliance, physical device behavior or restore reliability. They assert configured deduction behavior, not current statutory contribution tables. Overnight shift pairing, browser workflows, PostgreSQL integration and disaster recovery still require dedicated tests. The existing NU1903 dependency advisory remains recorded in ROADMAP.md.
