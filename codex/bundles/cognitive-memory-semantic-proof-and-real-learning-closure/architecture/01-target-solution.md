# Target Solution

## Proof system target

The bundle workflow must move from artifact-shape proof to artifact-and-behavior proof. For every meaningful execution report claim, Codex must provide:

- a literal behavior claim,
- a source-level invariant,
- a required production producer path,
- a required consumer path,
- a lifecycle path,
- a negative fixture proving a shallow implementation fails,
- a passing test proving the production path,
- portable proof references only.

Labels such as `embedding-backed`, `Czech/diacritic`, `claim-specific`, `automatic`, `provider-backed`, or `line-level` must have validator-enforced verification rules.

## Cognitive Memory target

The Cognitive Memory loop should behave like a student learning from a professor:

1. The user naturally teaches or corrects the memory.
2. The system captures a temporary professor anchor with structured claims, scope, misconception, examples, counterexamples, and language metadata.
3. The dream/cluster loop compares the anchor against existing memory and independent evidence.
4. The system uses the derived knowledge in real answers or workflows.
5. Accepted outcomes produce durable use evidence.
6. Assimilation only happens after independent support, integration, and accepted-use evidence.
7. The original professor wording fades only after the knowledge is truly internalized elsewhere.
8. Recall returns concise, useful, task-facing information by default, with exact lineage available on demand.

## Service boundary target

- Professor extraction: language normalization, intent classification, claim extraction, scope/misconception parsing, anchor factory.
- Accepted-use lifecycle: outcome event listener, emitter, idempotency, assimilation scheduler, audit writer.
- Cluster discovery: key extraction, lexical fallback, embedding vector provider, semantic ranker, graph/cohesion builder.
- Dream synthesis: claim grouping, domain claim synthesis, entailment/contradiction validation, provenance mapping, calibrated apply.
- Recall synthesis: intent/query planner, statement composer, lineage mapper, reference resolver.
- Proof validation: portable artifact resolver, claim-to-code verification, fake-proof fixtures, active skill sync proof.
