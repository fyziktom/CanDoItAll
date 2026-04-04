# Canonical model glossary

## Canonical model
The smallest set of concepts that represent the system's real domain truth.

A canonical model is not the same as:

- UI forms
- projections
- DTOs
- import/export payloads
- caches
- snapshots
- embeddings
- AI-generated suggestions
- activity feeds
- runtime watch state

## Canonical entity
A concept with stable identity that owns domain truth.

Questions:
- What truth does it own?
- What is its identity?
- Who may mutate it?
- Where is it persisted?

## Relation / edge
A first-class connection between concepts.

Examples:
- parent/child
- dependency
- assignment
- references

Relation data often deserves its own invariants and should not always be hidden in collections.

## Value object
A concept defined by its value rather than identity.

Examples:
- date range
- priority
- token scope descriptor
- storage location descriptor

## Projection
A derived representation optimized for a use case.

Examples:
- Gantt view
- Mermaid export
- CRM dashboard summary
- activity feed
- AI summary
- search index

A projection should not quietly become a second writable truth.

## Integration adapter / DTO
A type shaped for talking to another system or boundary.

Examples:
- email provider payload
- OpenAI request model
- storage driver result
- import/export record

These should not usually become canonical entities.

## Event
A fact that something happened.

Examples:
- node created
- dependency added
- snapshot published
- test run recorded

Events are not the same as current entity state.

## Runtime state
Operational state needed to run the system but not part of the domain truth.

Examples:
- watch process status
- temporary diagnostics
- transient command logs

## Snapshot / archival state
A representation of the system at a point in time, usually for transport, backup, replay, or audit.

It is not automatically the live source of truth.

## AI proposal state
Data proposed by an LLM or agent before it is validated and resolved into the canonical model.

Examples:
- parsed user intent
- suggested node type
- confidence score
- semantic vector
- generated summary

Treat proposal state as derived until validated.
