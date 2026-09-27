# PostgreSQL 18 implementation and local migration report

> Unfilled template. Every status below starts as **NOT RUN**. Replace it with observed evidence; never copy example statuses as results. Keep the filled private report outside Git unless sanitized for a deliberate repository record.

## Overall outcome

| Work item | Status | Evidence or precise blocker |
|---|---|---|
| Repository implementation | NOT RUN | |
| Required validation | NOT RUN | |
| Existing development instance on application port 5032 | NOT RUN | |
| Original data/recovery set retained | NOT RUN | |

Do not collapse these into “done” when the third row is blocked or only a partial API copy exists.

## Source and code identity

- Owner-supplied starting branch retained (no required branch name or review SHA):
- Actual starting HEAD and ending commit or working-tree fingerprint, provenance only:
- Relevant changes discovered after handoff review and how the plan was adapted:
- Components/FileTools dependency commits, dirty state and build mode:
- CI entry point/base branch and Components resolution method; FileTools pin retained:
- No sibling source/package substitution or unrequested branch change:
- Existing unrelated working-tree changes preserved:
- Source application version/schema history and hosting mechanism:
- Actual application URL/process identity for port 5032:
- Actual source PostgreSQL version and sanitized host/port/database/role identity:
- Authoritative launch configuration/profile and any runtime override:
- Filesystem/control-plane/vault/key roots identified (paths only, no secret content):

## Chosen PostgreSQL artifacts

- Target server patch version and why selected:
- Official Docker image, resolved digest and architecture verification:
- Official Windows binary archive URL, exact byte length and computed SHA-256:
- Actual client/server binary version probes performed:
- New layout/mount and generated-launcher verification:

## Backup, migration, and continuity

- Method: logical dump/restore, supported API, already-on-target verification, or other justified method:
- Source/target identity checks and proof target was separate:
- Writer quiescence and prevention of supervisor restart:
- Private backup locations, timestamps and integrity hashes:
- Matching application files/key protection context retained:
- Actual restore command/result; role/ownership/extension handling:
- Any separate application-schema migration and its verified effects:
- Old cluster/recovery copy retained, and how accidental old-host writes are prevented:

## Reconciliation

| Data category | Before | Restored before application writes | After activation/restart | Explanation of legitimate differences |
|---|---|---|---|---|
| Populated tables and exact counts | | | | |
| Stable IDs and relationships | | | | |
| EF migration history | | | | |
| Sequences/identity state | | | | |
| Workflows/process histories | | | | |
| API users/access state | | | | |
| Provider-sharing identities/references | | | | |
| Image-input/cached-image-input prices and null/zero semantics | | | | |
| Custom model/options and frozen historical pricing | | | | |
| Protocol envelopes v1/v2, fingerprints and digests | | | | |
| Long journals and durable receipt/operation state | | | | |
| Profile-bound conversations and transcript content | | | | |
| Workspace content/attachments | | | | |
| Control-plane and secret continuity | | | | |
| Extensions, custom indexes and search behavior | | | | |
| Other populated or legacy data | | | | |

Counts alone are not proof of equal content. Identify the representative records/relationships checked without exposing confidential payloads. Explain operational counters/audit writes and intended managed-seed effects rather than silently excluding mismatches. Record categories absent from the real source separately from formats covered by synthetic tests. For any checkpoint that requires explicit recovery, state whether that condition existed before migration and whether its SDK fingerprint changed; do not claim that re-executing its side effects is a preservation test.

## API fallback, when needed

- Direct-route blockers and remedies attempted:
- Actual API schema/routes/authentication mode discovered (no tokens):
- Trusted transport/listener and Swagger redirect handling, without auth/TLS bypass:
- Public-catalog feature negotiation, omitted fields and limitations accounted for:
- Coverage mapping for all required source data:
- Pagination/dependency/ID-preservation handling:
- Per-item results and authoritative target read-back:
- Physical source/target separation beyond profile GUIDs:
- Partial transfer and retry handling; incomplete target kept unselected:
- Unsupported data, retained guards, and resulting complete/blocked status:

## Actual 5032 validation

- Effective runtime database target and actual server version, not just selected profile:
- Health result:
- Existing known IDs/content observed via authorized API/UI:
- File/attachment and non-secret credential-continuity checks:
- Restart result and repeated runtime identity/content checks:
- External side effects avoided:
- Remaining operational issues:

## Repository validation

| Check | Exact command/context | Discovered | Passed | Failed | Skipped/unavailable | Evidence |
|---|---|---:|---:|---:|---|---|
| Affected production build | | | | | | |
| Relevant PostgreSQL 18 tests | | | | | | |
| Clean schema and restored schema | | | | | | |
| Installer/launcher and WhatIf | | | | | | |
| Docker persistence/rejection cases | | | | | | |
| Current CI dependency-policy regression tests | | | | | | |
| Recent fixture timeout/lifetime and profile guards retained | | | | | | |
| New persisted-data roundtrip topics | | | | | | |
| Documentation/link checks | | | | | | |
| Final portability-static enforcement | | | | | | |
| Broader frozen-checkpoint validation, if required | | | | | | |

Distinguish local Windows/native, Linux/Docker, and remote CI matrix evidence. Configuring CI is not executing CI. State source fingerprints when reusing a build. Those are validation provenance, not a constraint on the initial branch. Identify the actual PostgreSQL server for each database-backed lane and the actual current discovery count; skip/wrong-version/zero-discovery cannot be reported as PostgreSQL 18 proof. Linux process-host container evidence includes use of an init process. Preserve the separation between fixture administration, ordinary SQL and HTTP deadlines.

## Recovery and final limitations

- Exact retained recovery locations and how to identify the right source instance:
- Rollback prerequisites and matching application/files/keys requirements:
- Whether the target has accepted new writes, and implications for returning to the source:
- No automatic source cleanup performed:
- Remaining code, environment, validation or API-coverage blockers:
