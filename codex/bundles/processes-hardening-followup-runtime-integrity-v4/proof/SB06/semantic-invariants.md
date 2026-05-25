# SB06 Semantic Invariants

## SB06-INV-001

Expected behavior:

- Workflow artifacts satisfy process artifact expectations by explicit output mapping metadata or by the single workflow artifact/single compatible expectation fallback only.
- A workflow artifact is not assigned to a process expectation by kind/title/summary when multiple same-kind workflow artifacts or process expectations exist.
- Subprocess parent projection maps a parent expectation to a child artifact through an explicit child expectation id when multiple same-kind child artifacts can match.
- A subprocess parent step records a projection diagnostic instead of selecting a same-kind/title child artifact when the mapping is ambiguous.
- Many-to-one and one-to-many mapping declarations are treated as invalid and do not silently project artifacts.

Disallowed shallow implementation:

- prompt-only change
- source-assertion-only proof
- tests that manually seed final state instead of exercising producer/consumer lifecycle
- branch-specific hardcoding
- software-only behavior for generic process runtime

Required proof:

- failing-first/red-team proof
- passing proof
- source assertions
- anti-stub audit
- changed-file hashes
- production behavior artifact matrix when new runtime state is introduced
