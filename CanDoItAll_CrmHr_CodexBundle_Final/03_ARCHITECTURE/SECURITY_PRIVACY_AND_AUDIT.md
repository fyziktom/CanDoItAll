# Security, privacy, and audit

## 1. Data classification model

### Level A — Broad operational data

Examples:

- display name
- company name
- role labels
- non-sensitive contact info intended for operational collaboration
- opportunity stage, project ownership, allocation summaries

Allowed in:

- standard list views
- global search summaries
- activity feed summaries

### Level B — Internal business data

Examples:

- commercial notes
- staffing notes
- non-public contact methods
- opportunity commentary
- rate ranges

Allowed in:

- authenticated business screens
- targeted detail panels
- not necessarily broad search payloads

### Level C — Sensitive HR data

Examples:

- confidential HR notes
- onboarding/offboarding observations
- private candidate feedback
- manager-only comments

Allowed in:

- dedicated sensitive sections only
- excluded from global search
- excluded from broad timeline text summaries
- audit-recorded on access-sensitive operations where practical

## 2. Structural privacy controls

Recommended implementation seams:

- `Party.IsSensitive`
- `PartyConfidentialNote`
- `CrmHrAuditEntry.IsSensitive`
- service-level rule that broad search indexing ignores confidential note bodies
- UI callouts when sensitive content exists but is intentionally hidden from broad surfaces

## 3. Soft-delete and lifecycle rules

Do not hard-delete records that are referenced by:

- project assignments
- workbench participant links
- interactions
- opportunities
- workforce history
- recruiting history
- AI-agent ownership
- validation/test/resource ownership

Instead:

- mark party or profile archived / inactive
- keep history visible where relevant
- optionally restrict editing on archived items

## 4. Audit requirements

At minimum, audit these events:

- party created / updated / archived / reactivated
- duplicate merge
- relationship created / removed
- interaction logged or materially edited
- opportunity stage change
- opportunity converted to project
- workforce profile change
- allocation change
- candidate stage change
- onboarding/offboarding task completion
- AI-agent provider binding change
- AI-agent review status change

Audit record fields should include:

- actor
- action
- entity type
- entity id
- summary
- detail json
- sensitive flag
- timestamp

## 5. Search safety rules

Never index these directly:

- confidential note full text
- sensitive feedback text
- private HR commentary
- secrets or provider credentials
- protected PII that does not need broad discoverability

Instead index:

- display names
- safe summaries
- stage/status
- project / account / unit labels
- non-sensitive tags

## 6. Current-auth limitation and future-ready seam

The repository currently behaves as a local-user app and does not yet provide full RBAC.

Therefore this bundle does **not** claim to deliver a complete enterprise authorization model right now.

What it must deliver is:

- data partitioning,
- cautious indexing,
- explicit sensitive sections,
- audit trail,
- and code seams that allow later policy checks around sensitive actions.

## 7. Privacy success test

The privacy design is acceptable only if all of these are true:

- broad search cannot leak confidential HR notes,
- archiving a person does not destroy project and recruiting history,
- a merge keeps auditability,
- AI-agent provider credentials stay in `Security` / `Workspace`, not copied into party text fields,
- and the UI clearly separates general operational context from sensitive HR context.
