# Specification

## Objective

Add sensitive-data markers, audit entries, soft-delete rules, safe search behavior, HR-only note separation, and future-ready permission seams suitable for the current local-user model.

## Scope

- Implement confidential-note separation and audit trail screens/panels.
- Prevent sensitive content from entering broad search documents.
- Enforce archive-safe lifecycle rules instead of destructive delete.
- Add clear UI callouts when sensitive data exists.

## Services and entities involved

**Services**

- `PartyDirectoryService`
- `HrService`
- `CrmService`

**Entities / concepts**

- `CrmHrAuditEntry`
- `PartyConfidentialNote`

## Bundle-specific implementation notes

1. Follow the global architecture documents first.
2. Keep the module inside `CanDoItAll.Modules.CrmHr` unless the file reference list explicitly points to another module for integration changes.
3. Reuse the existing CanDoItAll services listed in `FILE_REFERENCES.md` instead of inventing parallel registries or orchestration layers.
4. Keep database changes additive and backward compatible where Workbench or existing modules already persist data.
5. Any UI added here must stay inside BaseLib + normal Razor patterns.

## Detailed functional outcomes

- **DIR-18** As a compliance lead, I can flag records that contain sensitive data so downstream screens treat them carefully.
- **DIR-19** As a support lead, I can see who last changed a party and when so ownership and accountability are visible.
- **HR-28** As a people ops manager, I can keep HR-only notes separate from general party notes so sensitive information is handled more carefully.
- **X-09** As a data steward, I can use archive and safe-delete rules so historical relationships are not broken by aggressive cleanup.
- **X-10** As a compliance lead, I can protect sensitive HR and personal data from overexposure in search and broad list screens so privacy risk is reduced.
- **X-11** As a compliance lead, I can audit important CRM/HR changes so the module is reviewable.

## Out of scope inside this bundle

- Bundles that are listed as dependencies but handled elsewhere stay out of this bundle.
- Do not prematurely solve later-wave concerns unless the dependency chain requires a small seam.
- Do not introduce payroll, marketing automation, or canvas-based UI work here.

## Definition of success

- Confidential notes are stored and displayed separately from broad operational notes.
- Sensitive content is not indexed into global search.
- Audit trail entries exist for important lifecycle and data changes.
- Archive/reactivate flows preserve history.
