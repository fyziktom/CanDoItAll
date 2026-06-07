# Domain Driver Readonly Evidence Schemas

## Scope
- This is a proposal artifact for SB025-SB027.
- Schemas are documentation-only evidence shapes.
- They are read-only and do not approve driver runtime APIs.

## .NET / Rust Verification Evidence
Readonly build/test/proof evidence schema.

Allowed fields:
- Repository identity and bounded project path.
- Declared target framework, package metadata, and build/test artifact ids.
- Existing build transcript id and result summary.
- Existing test transcript id and result summary.
- Existing proof transcript hashes.
- Diagnostic findings and suggested next proof.

Denied:
- Running `dotnet`, `cargo`, shell, package restore, publish, or git commands.
- Writing files, changing project files, mutating process state, or scheduling retries.
- Installing packages or changing tool versions.

## Office Evidence
Allowed fields:
- Process-provided email or document summary id.
- Message, document, attachment, or artifact metadata already captured by the process.
- Redacted subject/title, sender/owner, timestamp, and evidence hash.
- Readonly checklist result and diagnostic summary.

Denied:
- Calling Office or Graph APIs.
- Sending mail, tagging mail, creating tasks, mutating documents, or uploading attachments.
- Writing workspace/storage content.

## Business-Analysis Evidence
Allowed fields:
- Requirement snapshot id.
- Deliverable artifact id.
- Checklist id and checklist result.
- Traceability link ids.
- Gap analysis, confidence score, and suggested next evidence.

Denied:
- Mutating CRM, project, workflow, or business records.
- Changing process state.
- Writing artifacts or escalating to execution.

## Runtime Verification Evidence
Allowed fields:
- Core descriptor family and descriptor id.
- Process-owned snapshot ids.
- Proof transcript ids and hashes.
- Readonly diagnostic result.

Denied:
- AgentFramework execution, provider repair, retry scheduling, finalizer application, claim renewal, transition mutation, storage writes, and process audit writes.
