
# Specification

## Item identity

- **Item ID:** I01
- **Title:** Foundation: rich node schema, metadata, and compatibility
- **Origin:** docx
- **Dependencies:** None

## Objective

Create a stable data foundation for all new canvas nodes without exploding the schema or breaking existing project structure records.

## Normalized scope

Introduce a typed metadata strategy, moderate expansion of ProjectObjectType, disciplined ObjectSubtype usage, and backward-compatible persistence rules for the new node families.

### In scope

- Shared contracts and enums.
- Project object persistence model and schema initializer.
- Metadata serialization, validation, and round-trip helpers.
- Compatibility coverage in integration tests.

### Out of scope

- Implementing every downstream editor feature that depends on the new schema.
- A full polymorphic ORM rewrite.

## Key implementation decisions

- Add a structured metadata payload such as MetadataJson to project objects instead of adding dozens of dedicated columns.
- Add only a small set of new ProjectObjectType values for real behavioral families such as Meeting, Recording, Transcript, Participant, WorkItem, Script, Environment, and Infrastructure.
- Use ObjectSubtype and typed metadata DTOs for specialized variants such as online meeting, onsite meeting, HR participant, DNS record, Tailwind watch, or ChatGPT link.
- Keep all existing nodes working without migration surprises; the new metadata strategy must be additive and backward-compatible.

## Implementation tasks

- Define the new node-family strategy and codify it in shared contracts.
- Add typed metadata DTOs and safe serialization helpers.
- Extend persistence schema and service logic to round-trip metadata cleanly.
- Provide migration or defaulting logic so existing records continue to load without metadata.
- Add validation that rejects malformed metadata and unknown critical subtypes where appropriate.

## Risks to control

- Schema sprawl if every new note becomes a dedicated column.
- Breaking old data if default metadata handling is not defensive.

## Covered original notes

- N001 — Project structure
- N038 — Requrements/Prerequisites
- N039 — Generic node
