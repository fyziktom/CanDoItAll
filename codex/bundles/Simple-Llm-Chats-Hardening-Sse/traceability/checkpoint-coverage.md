# Checkpoint coverage

| Dependency group | Checkpoint | Invalidated when |
|---|---|---|
| Source/proof baseline | CP0 | development/source/proof head changes or prior failure classification is contradicted |
| Canonical persistence, state machine, profile, dispatcher, queries | CP1 | SB01–SB05 reopens or streaming requires an unmodeled transition |
| Provider stream, events, SSE, security | CP2 | SB07–SB10 reopens or reconnect/terminal semantics change |
| Whole feature and repository | FINAL | any checkpoint reopens, final head changes, stable gate/CI is red |

A checkpoint is not a test-count summary. Its review file must explain why the semantic invariants are
proven at the actual commit.
