# Implementation prompt

Implement **B12 — Security, privacy, audit, and safe lifecycle controls** for CanDoItAll.

## Bundle goal

Add sensitive-data markers, audit entries, soft-delete rules, safe search behavior, HR-only note separation, and future-ready permission seams suitable for the current local-user model.

## Hard rules

- follow `03_ARCHITECTURE/*` and `02_REQUIREMENTS/SCOPE_AND_NON_FUNCTIONAL_DECISIONS.md`
- keep UI in BaseLib / Razor / HTML only
- do not introduce canvas components
- preserve backward compatibility for existing project/workbench flows where relevant
- add or update tests listed in `FILE_REFERENCES.md`
- add screenshot evidence requirements from `SCREENSHOT_REQUIREMENTS.md`

## Implementation steps

1. Inspect all files in `FILE_REFERENCES.md`.
2. Implement the data model / service changes required for this bundle.
3. Implement the route or UI changes required for this bundle.
4. Wire search/activity/integration seams if this bundle requires them.
5. Add automated tests at the correct level.
6. Execute browser validation and capture screenshots.
7. Write a concise evidence note summarizing code changes, tests, and screenshots.

## Bundle-specific targets

- Implement confidential-note separation and audit trail screens/panels.
- Prevent sensitive content from entering broad search documents.
- Enforce archive-safe lifecycle rules instead of destructive delete.
- Add clear UI callouts when sensitive data exists.

## Stories that must be satisfied in this bundle

- **DIR-18** As a compliance lead, I can flag records that contain sensitive data so downstream screens treat them carefully.
- **DIR-19** As a support lead, I can see who last changed a party and when so ownership and accountability are visible.
- **HR-28** As a people ops manager, I can keep HR-only notes separate from general party notes so sensitive information is handled more carefully.
- **X-09** As a data steward, I can use archive and safe-delete rules so historical relationships are not broken by aggressive cleanup.
- **X-10** As a compliance lead, I can protect sensitive HR and personal data from overexposure in search and broad list screens so privacy risk is reduced.
- **X-11** As a compliance lead, I can audit important CRM/HR changes so the module is reviewable.

## Stop conditions

Do not mark this bundle done until all acceptance criteria pass and the screenshot evidence is semantically reviewed.
