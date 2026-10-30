# Branch and release workflow

- `development`: generic product development; excludes ACBA-specific hardware.
- `acba-dev`: ACBA integration and demo testing, including biometric hardware.
- `acba`: approved ACBA production releases; no demo accounts or login controls.
- `master`: legacy and outside the current maintenance workflow.

Use short-lived `codex/` feature branches from the target development branch. Validate general fixes on `development`, then cherry-pick the reviewed commits into `acba-dev`. ACBA-only changes stay on `acba-dev`. Promote reviewed changes to `acba` selectively, excluding demo seeding and environment configuration. Avoid whole-branch merges while these variants differ. Record source commits when cherry-picking (`git cherry-pick -x`).

Before pushing, build the API and Server Station in Release, run authorization and deployment tests, rebuild Tailwind when UI classes change, and inspect the staged diff. Test ACBA hardware changes on the actual device before release. Tag approved production releases after validation; do not create release tags for untested local work.

Deployment database defaults are explicit assembly metadata in Server Station: `officecoredevdb` for development, `officecoreacbadevdb` for acba-dev and `OfficeCoreDb` for acba. Debug and Release behave alike. The API configuration must match the same profile. Development seeders reject other database names before migrations. Development Server Station settings live in separate profile folders under LocalAppData/OfficeCore; configure credentials again on first launch after this change. Production retains its existing settings path. Custom production database names remain supported.

Keep user uploads, local databases, private configuration and installer outputs out of new commits. Existing tracked runtime data requires a separate content review before removal. Never include unrelated working changes in maintenance commits.