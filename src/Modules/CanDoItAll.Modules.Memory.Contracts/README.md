# Memory presentation contracts

Data-only provider/editor/result contracts and the in-process owner port. The only project
dependency is Memory.Abstractions. `MemoryProviderActionStatus` exhaustively projects the
Application handler status without bringing that implementation into a renderer.
Existing numeric status values and protocol/ledger identities remain unchanged.

Editor captures copy transport objects, collections and JSON extension elements. Numeric
transport input retains incomplete text until authoritative validation. Presentation
snapshots remove recognized legacy credential values and unsafe provider UI URLs while
retaining migration-key metadata. Environment references are names, never resolved secrets.

See [the boundary and proof record](../../../docs/architecture/memory-ui-boundary.md).
