# Evidence templates are empty, not passed tests

Copy `evidence.json` to a task-owned output directory. Retain the original template unchanged.
Each coverage group starts NOT_RUN and references completed attempt IDs only after actual proof.
Record commands, expected/actual discovery, runtime expansion, source/binary/image identifiers,
private raw artifacts and qualified failures. Non-test checks have their own evidence type.

Use `python tools/validate_handoff.py --evidence <actual.json>` to inspect recorded structure,
or add `--require-complete` to reject incomplete/non-passing groups. This is not a verifier of
product correctness or artifact authenticity; the native reviewer must inspect referenced outputs.

`model-parity.json` and `check_model_parity.py` are unchanged helpers from the preceding PP2 handoff.
Their schema is useful for exact safe exports; running them alone does not execute containers,
UI selectors, native authorization, model routing or canonical evidence reads. Retain original
source/client snapshots separately and do not substitute fabricated expected data.

## Recorded attempt fields

Use one immutable `id` per actual attempt, with `kind` (`test` or another accurately named check),
`status`, exact `command`, SHA-256 `source_fingerprint`, and `artifact_refs` to retained outputs.
A passing test also records integer `expected_discovery`, `actual_discovery`, `executed`, `passed`,
`failed` and `skipped`. Record an `expansion_note` when discovered rows expand into more runtime
cases. Failed attempts remain separate and may be referenced alongside their successful follow-up;
do not edit their counters into a pass. A group is qualified/blocked when its actual proof is.

`--require-complete` is a strict optional final check, not a reason to fabricate evidence or remove
remaining limitations. `VERIFIED_LOCAL` dependency delivery is distinct from `VERIFIED_REMOTE` and
never claims publication. The checker validates structure, not signatures or private artifacts.
