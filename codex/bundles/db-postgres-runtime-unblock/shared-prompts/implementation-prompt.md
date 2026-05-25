# Implementation prompt

You are implementing a PostgreSQL-only runtime follow-up. Start with the subbundle README for the current phase. Do not skip gates.

Always preserve these invariants:

- one canonical runtime DB per process generation,
- no operation straddles two DB profiles,
- no duplicate process step execution,
- no duplicate automation/outbox delivery,
- no hidden retired-provider string concatenation for grep avoidance,
- no SQLite runtime support.

When optimizing:
- replace in-memory long locks with durable DB claims,
- keep in-memory locks only as local fast-path helpers,
- prove concurrent negative cases with PostgreSQL-backed tests,
- keep UI honest about restart/maintenance requirements.

Every critical subbundle must write:
- `proof/SBxx/manifest.md`,
- command transcript paths,
- changed-file hashes,
- positive semantic proof,
- adversarial negative proof,
- anti-stub audit.
