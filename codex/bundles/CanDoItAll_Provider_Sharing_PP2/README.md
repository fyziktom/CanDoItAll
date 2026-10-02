# Shared provider verification and UI extraction PP2

Read [prompt.md](prompt.md). Execute two ordered outcomes: first prove and repair real
multi-instance provider behavior, then extract Sharing and source-connection rendering.
This is not permission to begin another module or rewrite provider protocols.

The source review is based on application `00c395ba0f4ce62c611c62718d8a510f9044f7b4`;
it is not an execution pin. Inspect the actual current checkout and repository instructions.
The original PP1 package and historical bundles remain immutable. The copied
[shared v3 foundation](shared/README.md) is unchanged; current canonical repository guidance
and the explicit operator scope here govern new work.

Start with [PP1 review](PP1_REVIEW.md), [S0](S0_FINDINGS.md),
[multi-instance runbook](MULTI_INSTANCE_RUNBOOK.md), [model parity](MODEL_PARITY_CONTRACT.md)
and [signing](COMMITS_AND_SIGNING.md). Then read [PP2 architecture](PP2_ARCHITECTURE.md),
[state/operation rules](STATE_AND_OPERATIONS.md) and the [validation matrix](VALIDATION_MATRIX.md).
The [roadmap](ROADMAP.md) states what remains after this cut.

The operator explicitly authorizes local task-owned test publication, test credentials,
isolated containers and signed local commits. This does not authorize external publication,
Git push/merge, real-provider spending, real credentials or changes to ordinary installations.
Ask the operator to unlock their existing signing key early through native pinentry; never ask
for the passphrase in chat. Preserve normal hosts, databases, ports, keys and history.

Run `python tools/validate_package.py --root .` to check this handoff's integrity.
`python tools/check_model_parity.py <safe-snapshot.json>` checks only the internal consistency
of exported parity evidence, not whether a browser/container actually ran.
Templates start NOT_RUN. Product evidence belongs in a separate task-owned run directory.
