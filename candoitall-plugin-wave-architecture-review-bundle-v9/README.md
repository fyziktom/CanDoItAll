# CanDoItAll plugin-wave architecture review bundle v9

## Purpose
Re-review the codebase after the phase8 refactor and decide whether the architecture is finally safe for the next large plugin wave (email, LinkedIn, custom APIs, and other connector-driven features).

## Verdict
**NO-GO for the large plugin wave.**

The codebase is better than in v8, and some earlier blockers are genuinely improved:
- persisted system-managed parallel projection truth looks retired from the Workbench load path,
- hierarchy / reparenting is cleaner than before,
- CRM/HR ownership seams and node-kind / assignment seams are healthier.

However, the architecture is still not in a plugin-ready state because several deep blockers remain:
1. legacy binding / carrier fields still exist on the core node entity and DB table,
2. runtime binding logic still hydrates legacy carrier state back into the node,
3. marker truth is still dual and write-normalized on reads,
4. plugin editors are still hardcoded by known field keys and hardcoded editor models,
5. custom plugins still persist fake legacy enum identity,
6. node references are still closed-world,
7. read-time normalization still mutates the DB,
8. the generic durable boundary for future write-side connectors is still missing.

## Why this bundle is stronger than v8
The previous gate structure produced a false green because it only scanned a narrow set of files and symbols. In this revision:
- hard gates scan the **whole relevant repo surface**, not just one file,
- forbidden-pattern rules explicitly fail on **moved partial classes** and **compatibility shims living in active paths**,
- closure requires **code + tests + proof**, not just ADR text,
- plugin readiness now includes **manual gate MG-01** for the future write-side connector boundary.

## Runtime validation status
Static review only in this environment. `dotnet build/test/run` could not be executed here because the runtime/SDK is unavailable in the container. Runtime closure must still be completed by Codex inside a real .NET environment.
