# B12 — Security, privacy, audit, and safe lifecycle controls

## Purpose

Add sensitive-data markers, audit entries, soft-delete rules, safe search behavior, HR-only note separation, and future-ready permission seams suitable for the current local-user model.

## Dependencies

B01, B02, B03, B04, B06, B08, B11

## Main stories covered

- **DIR-18** As a compliance lead, I can flag records that contain sensitive data so downstream screens treat them carefully.
- **DIR-19** As a support lead, I can see who last changed a party and when so ownership and accountability are visible.
- **HR-28** As a people ops manager, I can keep HR-only notes separate from general party notes so sensitive information is handled more carefully.
- **X-09** As a data steward, I can use archive and safe-delete rules so historical relationships are not broken by aggressive cleanup.
- **X-10** As a compliance lead, I can protect sensitive HR and personal data from overexposure in search and broad list screens so privacy risk is reduced.
- **X-11** As a compliance lead, I can audit important CRM/HR changes so the module is reviewable.

## Main routes

- `/crm-hr`
- `/crm-hr/directory`
- `/crm-hr/workforce`

## Done when

- Confidential notes are stored and displayed separately from broad operational notes.
- Sensitive content is not indexed into global search.
- Audit trail entries exist for important lifecycle and data changes.
- Archive/reactivate flows preserve history.
