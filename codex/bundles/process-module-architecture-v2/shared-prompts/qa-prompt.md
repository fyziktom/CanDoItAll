# QA Prompt

Review a future implementation bundle derived from `codex/bundles/process-module-architecture-v2` against:

- raw requirement coverage,
- architecture boundary compliance,
- semantic behavior, not only file shape,
- negative tests for failure paths,
- runtime event/state production behavior,
- browser-visible UI behavior where applicable,
- migration and rollback safety where applicable.

For critical future implementation phases, require:

- shallow-pass trap,
- adversarial negative proof,
- semantic positive proof,
- anti-stub audit,
- raw-note literal closure,
- proof manifest with changed-file hashes and transcript paths,
- production behavior artifact matrix for new events, states, records, or signals.

Reject proof that only shows non-empty output, static markers, row counts, or happy-path fixtures.

Reject any implementation that wraps the old dispatcher, lets runtime select strategies dynamically, lets UI query runtime internals, or treats Markdown/Mermaid as canonical template source.
