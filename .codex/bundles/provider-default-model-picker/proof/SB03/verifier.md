# SB03 Verifier

## Fake-Proof Resistance Review

- Re-read the failing-first transcript: `proof/SB03/transcripts/failing-first-explicit-override.txt` contains two failures before the production fix. The selector hid the model text field for a non-empty provider-default value, and the agent dialog saved an empty model instead of `gpt-5-mini`.
- Re-read the passing transcript: `proof/SB03/transcripts/passing-targeted-tests.txt` shows 10/10 focused tests passing after the fix, including the new explicit-default override test and the existing provider-default-linkage reset test.
- Re-read source assertions: `proof/SB03/transcripts/source-assertions.txt` shows provider-default dropdown selections still emit empty through `ResolveProviderDefaultEmittedValue`, while `NormalizeAgentModelForSave` now only trims or empties whitespace.
- Re-read anti-stub audit: `proof/SB03/transcripts/anti-stub-audit.txt` found no production TODO, NotImplemented, fixture-specific branching, or test-name branching markers in the touched production files.

## Decision

SB03 proof is behavior-level, not table-only proof: the negative case fails for the exact user symptom, the positive case proves save/reload persistence, and the old provider-default linked-empty case remains covered.
