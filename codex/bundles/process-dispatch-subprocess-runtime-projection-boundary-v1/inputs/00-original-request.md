# Original User Request

Codex has completed the previous bundle and pushed it to `maf-processes-refactor`.

The next bundle must continue the gradual isolation work without rushing Process Core. Some services are still huge and should be decomposed through module-local abstractions and seams. The bundle should include more phases than the previous one, with refactor gates every few subbundles so Codex can work longer and cannot pass by doing only small changes.

The work is not only about Process Core preparation; it should also keep preparing future process helper drivers conceptually, but production driver APIs should only appear when the runtime boundary is ready.
