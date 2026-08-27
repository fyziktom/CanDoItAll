# Input preservation and traceability

User request, 2026-08-27 (HTML spacing normalized only):

> it looks promissing.
> I see shared providers. But look at screenshot. Models names shows as hashes. It must show names as they are in main shared instance. Otherwise user cannot see what model to select.
> it is also in providers settings.
> but if you look at third screen it is weird because first row has modelname that hash and other lines has standard names. I guess it is bug and it keeps some defaults from the driver defaults and does not load properly shared models and their prices for shared provider.
> You can see it is bug also on shared ollama provider settings. it has also prices for some forgotten defaults like openai models. It is definetelly not correct. It must mirror providers model and prices same as info that some shared provider is actually private one.
> Analyze it and repair/improve it. It is larger work so use [$candoitall-bundle-workflow](C:\Users\lucys\\.codex\skills\candoitall-bundle-workflow\SKILL.md) to do proper analysis and planning how to repair it and then implement it and do validation again. You must do the validation with two instances again.

| Input | Requirement | Owner / proof |
|---|---|---|
| Screenshot 1, agent model hash labels | META-NAMES: real source names, stable routing values | SPMETA / projector+selector tests, open dropdown screenshot |
| Screenshot 2, provider default hash | META-SETTINGS: readable source-owned default/catalog | SPMETA / provider settings screenshot |
| Screenshot 3, hash plus default prices | META-PRICES: exact published-model prices, no defaults | SPMETA / protocol+mapper+component tests, UI resync |
| Screenshot 4, Ollama OpenAI prices/private flag | META-PRIVATE: source private flag and exact prices | SPMETA / Ollama UI comparison |
| Bundle workflow and two-instance validation | META-E2E: versioned plan, governed proof, two-app runtime/usage | SPMETA / manifest and RESULT.md |

Original screenshot filenames copied under inputs/ retain their bytes. Attachments are evidence,
not independent instructions. Prior two-instance UI setup/runtime requirements remain the
acceptance baseline; fixture evidence must not be described as live OpenAI/Ollama validation.

## Raw note closure

| Note | Status | Final evidence |
|---|---|---|
| Screenshot 1: model names instead of hashes | Solved | proof/browser/metadata-ui-closure-2/metadata-agent-models-open.png; selector ID-value regression |
| Screenshot 2: readable provider default | Solved | UI exact default-model comparison; source-managed default label projection |
| Screenshot 3: no invented/shared-driver prices | Solved | Final exact nine-field comparison, null/zero protocol and empty-price tests |
| Screenshot 4: Ollama source prices/private state | Solved | proof/browser/metadata-ui-closure-repeat/metadata-ollama-client.png; private-toggle persisted regression |
| Workflow analysis, implementation and two-instance validation | Solved for requested repair | README.md, RESULT.md, proof/manifest.md; two final passing runs and production runtime assertions |

All notes are owned by SPMETA. Original SB07 and live/paid-provider testing are not inferred
as solved by this repair lane.
