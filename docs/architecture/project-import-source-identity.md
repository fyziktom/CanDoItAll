# JSON outline source identity

JSON outline imports can preserve a caller's `sourceKey` in owner-created structured
metadata. This supports reliable readback and recovery without putting identifiers in
titles or human notes. Duplicate titles are allowed; source identity is independent.

Each optional key starts with an ASCII letter or digit and has at most 128 characters,
using ASCII letters, digits, hyphen, underscore, period, colon or slash. Keys are unique
over the entire outline with ordinal case-sensitive comparison. A supplied null,
number, empty/invalid string or duplicate fails with HTTP 400 before import writes.
`requireSourceKeys: true` additionally rejects any node without a key and is accepted
only for JsonOutline. Clients can discover this property in the live import schema;
older hosts must be reported as unsupported before a client relies on persisted keys.

The Workbench import service validates and parses the source before creating its
container or source asset. During creation it writes `metadataJson.importSource` with
`sourceKey`, numeric `sourceKind` and `containerNodeId`. The existing canonical task
owner still creates task lifecycle/description metadata. The typed metadata envelope
recognizes import identity so task creation, scheduling and later owner edits retain it.
Generic task metadata writes remain forbidden. No caller-supplied arbitrary metadata is accepted through import.
Omitted keys and other formats retain their existing behavior. Import is not atomic
after validation; a write failure can still leave partial results and must not trigger
a blind retry.

The small identity record owns validation and serialization, independently exercised by
unit tests. The existing import service remains the orchestration and mutation owner;
no projects, registrations, partial classes or service interfaces are added. Raw HTTP
tests cover same-title nodes, clean notes, exact identity readback and rejection before
any nodes are created. Existing Mermaid/DOCX/XMind and raw task-update suites cover
unchanged import and canonical task behavior. SharedInfo carries the API-user guidance
and exact generated snapshot; the hotel seeder consumes it through HTTP only.

## Validation and architecture closure

Review result: **Pass**. The existing Workbench importer and metadata normalization
remain the owners; the typed identity adds no dependency direction, registration,
interface or partial-class split. Production Workbench/Web Release builds passed with
zero warnings/errors. All 14 identity unit cases and 35 focused HTTP/import cases
passed without skips against PostgreSQL 18.6. The latter include eight new raw JSON
cases, 23 canonical task-update cases and four existing Mermaid/DOCX/XMind imports.
The final portability gate passed with 15,312 reviewed executable findings unchanged;
no baseline refresh was needed. The initial HTTP failures exposed dropped metadata
in both task serialization and node-kind scoping; the final proof includes the fixes.
