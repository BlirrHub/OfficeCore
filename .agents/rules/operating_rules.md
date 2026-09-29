# OfficeCore — AI Agent Operating Rules & Directives

> **Notice:** This file is automatically loaded by Antigravity IDE and AI agents when operating within the OfficeCore workspace. All directives here are strictly enforced. Full architecture reference is in [agent.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/agent.md).

---

## 1. Mandatory Workflow on Every Chat & Update

1. **CHANGELOG Updates (Strict Add-Only Policy):**
   - After completing any update, bug fix, refactor, or feature, **ALWAYS update [CHANGELOG.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/CHANGELOG.md)** under `## [Unreleased]`.
   - **DO NOT REMOVE OR DELETE ANYTHING:** You must never remove, truncate, or overwrite existing entries, milestones, or history from `CHANGELOG.md`. Only append new entries or update recent entries with additional details.
2. **Occasional & Timely Git Commits:**
   - Commit changes to Git frequently and whenever completing a feature, bug fix, or milestone.
   - Do not leave modifications uncommitted across tasks.
   - Use clean, conventional commit messages (`feat:`, `fix:`, `refactor:`, `chore:`).

---

## 2. Core Architecture & Standards

- **Multi-Tenancy:** Every workspace-scoped entity must implement `ITenantEntity` and include `OrganizationId`. All queries and mutations in controllers must be scoped to the active tenant.
- **Theme Parity:** All pages and components must support both **Dark Mode** and **Light Mode** seamlessly with no contrast or visibility flaws.
- **Tailwind CSS v4:** Whenever adding or altering Tailwind classes, run `npm.cmd run css:build` in `src/OfficeCore.Web` so they are compiled to `wwwroot/app.css`.
- **Preserve Existing Functionality:** Do not delete working code, existing entity configurations, or docstrings unless explicitly instructed.
- **Detailed Reference:** See [agent.md](file:///c:/Users/i5%207th/Documents/Antigravity/Projects/OfficeCore/agent.md) for the comprehensive architecture guide, database schema guidelines, and CLI commands.
