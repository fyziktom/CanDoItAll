# Cutover and rollback matrix

| Change | Temporary selector allowed? | Shadow allowed? | Rollback | Delete old path when |
|---|---|---|---|---|
| Canonical authority enforcement | Yes, test/dev only | pure decision comparison only | select old planner before old deletion | CP1 passes and differences resolved |
| Source authority registry | No broad fallback in production | no | restore explicit prior provider only | all source publishers inventoried |
| Workspace owned aggregate | construction-root selector | instance/identity observation only | dispose new graph then switch | CP2 passes, no active process leak |
| Scope-bound recovery/script | no dual reads on production evidence | pure path-resolution comparison | revert adapter wiring | project/organization differential tests pass |
| State envelope v2 | read old/write new | compatibility decision comparison | continue v2 reads; binary downgrade prohibited | retention criteria remove old readers |
| Per-proposal UI | bool endpoint retained | no dual continuation | use compatibility endpoint | clients migrated and telemetry confirms |
| Tool governance pipeline | bounded selector before deletion | pure decisions only | switch injected policy implementation | CP4 passes |
| Lightweight LLM hardening | same port implementation selector | request/result mapping only | select previous adapter | provider/workflow matrix passes |
