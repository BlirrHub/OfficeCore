# Branch and release workflow (ACBA-Dedicated)

This repository is dedicated specifically to the **ACBA** deployment of OfficeCore, including biometric hardware integrations and customized business workflows.

## Active Branches

- **`acba-development`**: Active day-to-day development, bug fixes, feature staging, demo testing, and biometric hardware integrations for ACBA.
- **`acba-production`**: Approved, tested ACBA production releases (default branch on GitHub). Contains production logic with zero demo accounts, quick-login controls, or simulated seed employees.

*(Note: Legacy generic branches `development`, `master`, `acba`, and `acba-dev` have been retired and deleted).*

## Workflow & Promotion

1. **Feature Work:** Create short-lived feature branches off `acba-development`.
2. **Validation:** Validate all changes on `acba-development` against the test suite (`dotnet test OfficeCore.slnx`), UI build (`npm.cmd run css:build`), and hardware integrations (ESP32 / AS608 biometric scanner).
3. **Promotion to Production:** Promote reviewed commits from `acba-development` to `acba-production` selectively (using `git cherry-pick -x`), excluding demo seeding (`DbSeeder.cs`) and environment configuration (`appsettings.json`). Avoid raw whole-branch merges while demo seeding differences exist between development and production.
4. **Release Tagging:** Tag approved production releases on `acba-production` after end-to-end verification. Do not create release tags for unvalidated local work.

## Deployment Database Profiles

Deployment database defaults are explicit assembly metadata in Server Station (`OfficeCoreDefaultDatabase`):
- `acba-development` targets `officecoreacbadevdb`.
- `acba-production` targets `OfficeCoreDb`.

Development seeders reject other database names before migrations to prevent accidental overwrites. Server Station settings are isolated per profile folder under `%LocalAppData%/OfficeCore` (`officecoreacbadevdb` vs production root). Custom production database names remain supported.

## Repository Hygiene

Keep user uploads, local databases (`*.db`, `*.sqlite`), private configuration (`appsettings.*.Local.json`, `station_secret.key`), and installer outputs (`dist/`, `Setup-*.exe`) out of new commits. Never include unrelated working changes in maintenance commits.
