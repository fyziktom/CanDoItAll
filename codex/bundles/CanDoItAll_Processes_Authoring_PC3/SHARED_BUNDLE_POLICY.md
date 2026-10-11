# Stable shared baseline and single-download execution

## Operator decision

This task is delivered as one archive containing only `CanDoItAll_Processes_Authoring_PC3`.
The shared bundle remains the stable baseline already present in the repository. Do not
request another shared download, install an archived replacement or make an exact shared
version/manifest digest a prerequisite. Do not create another shared copy inside PC3.

Read the current shared guidance at `codex/bundles/UI_Decoupling_Shared_Bundle` together
with current repository instructions. Record its actual Git state and public package
metadata in the working ledger, if available. This is provenance, not a compatibility pin.
Check for local changes before using or touching it. Package integrity checks are separate
from semantic compatibility; an unfamiliar version is not itself an incompatibility.
A genuine checksum failure must be investigated, never concealed by blindly resealing.

If the expected directory was moved, locate its current canonical path through repository
instructions/history. Do not fetch a replacement ZIP or invent missing rules. A genuinely
unavailable required policy is an explicit local prerequisite; finish safe independent work
and describe the limitation without fabricating approval or quietly reducing validation.

## Task-local rules do not require shared edits

PC3-specific requirements are authoritative for this assignment's explicitly granted scope.
They refine a generic shared default or a historical PC2 exclusion; they do not weaken
current repository safety, authorization, ownership, portability or functional-parity rules.
In particular, the existing generic warning that the shared bundle alone does not authorize
a schema change remains true: **this PC3 assignment** authorizes the bounded implementation
and additive migration in [scope](SCOPE_AND_STAGES.md) and [migration](MIGRATION_AND_SCOPE.md).
It does not authorize changes to ordinary user databases or unrelated platform behavior.

The useful additions previously placed in the proposed shared update are available locally:

| Requirement | Current PC3 source |
| --- | --- |
| Receipt versus current authoritative revision; clean/dirty draft behavior | [Observation and mutations](OBSERVATION_AND_MUTATIONS.md) |
| Complete native meaning, hidden fields, resolved resources and stable identities | [Semantic coverage](SEMANTIC_COVERAGE.md), [owner design](NATIVE_OWNER_DESIGN.md), [publication](PUBLISH_AND_LAUNCH.md) |
| Explicit native/schema scope rather than another prerequisite-only plan | [Scope and stages](SCOPE_AND_STAGES.md), [migration and scope](MIGRATION_AND_SCOPE.md) |
| Production service lifetimes, independent scopes and real process restart | [Validation](VALIDATION_PLAN.md), [browser journeys](BROWSER_JOURNEYS.md) |
| PGP entry request and incremental verified signed checkpoints | [Signing and commits](SIGNING_AND_COMMITS.md), [prompt](prompt.md) |
| Current review and remaining-work map | [PC2 review](REVIEW_PC2.md), [source register](SOURCES.json), the new PC3 validation report |

Do not create or update shared files solely to duplicate this table's requirements. Do not
refresh shared history, status maps, source registers or version labels merely because a
new assignment exists. Keep PC3 progress, residuals and evidence in PC3's maintained output
reports, outside the sealed input package. Historical maps are navigation, not a command
to repeat already completed extraction.

## Only Codex makes a necessary shared correction

The operator authorizes Codex to make a small in-repository shared correction **when the
current implementation demonstrates a real cross-cutting need**. No extra operator download
is involved. This permission is not an assignment to rewrite the shared bundle every time.

Use this decision rule:

1. Prefer the existing shared rule plus the task-local requirement. Record `NO_CHANGE`
   when they suffice; ordinary PC3 specialization belongs here, not in shared.
2. For a real shared defect, name the exact rule/tool path, current mismatch, affected
   consumers and why a task-local clarification is insufficient. Protect unrelated edits.
   A stale PC2 label or a desire to copy this package's wording is not by itself a reason.
3. Apply only the smallest backward-conscious change. Preserve existing policies, public
   tooling behavior and historical evidence. Do not rewrite earlier bundles or their pins
   to manufacture compatibility. Do not weaken security, tests or functional obligations.
4. Run affected shared-tool/document/link checks and relevant consumer checks. If that
   canonical shared package uses a sealed manifest, inspect and record the before/after
   diff and update only the directly required manifest/metadata by the repository's normal
   process. This limited maintenance permission does not permit tampering with old reports
   or resealing the delivered PC3 inputs to hide edits. No version bump is required merely
   to start PC3; do not create a new shared distribution.
5. Include the correction in a logical, tested, signed checkpoint with its reason, affected
   checks and public commit identity. Report `MINIMAL_REPO_CHANGE` and the exact changed
   paths in PC3 evidence. Continue the assigned implementation without requesting a new
   bundle download or stopping after the shared correction.

Use `BLOCKED` only for a concrete unresolved conflict or missing prerequisite. Do not
silently interpret a blocked shared check as passing. No automatic push is authorized.

## Package verification is deliberately local

The task manifest seals this archive's files only. `bundle.json` has no shared version or
manifest requirement. Use this package's read-only [verifier](tools/verify_package.py).
Existing shared tools may also inspect it where compatible, but no shared-tool upgrade is
required to verify or begin this task. This packaging choice is not permission to skip
reading the actual shared/repository rules or to disregard an observed integrity problem.
