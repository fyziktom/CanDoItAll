# Semantic Adequacy Proof

Use this reference when preparing, executing, or validating Behavioral or Governed subbundles. Proof must show that shipped behavior satisfies the intended outcome, not merely that files, rows, statuses, or template strings exist.

## Required Evidence

Each Behavioral or Governed subbundle must record these fields in the execution report or a linked proof artifact:

- Raw note owned: quote the literal request or normalized requirement, including scope words such as `all`, `every`, `must`, `exactly`, and `same flow`.
- Shipped behavior: describe the production behavior that now satisfies that note.
- Source proof: list the changed production files or unchanged source surfaces that enforce the behavior.
- Test proof: list exact commands and tests that prove the behavior.
- Shallow-pass trap: name the simplest fake or over-narrow implementation that would pass structure-only tests.
- Adversarial negative proof: include a case the shallow implementation would mishandle and prove the system rejects or separates it correctly.
- Semantic positive proof: include a realistic intended case and prove the system produces the desired domain behavior.
- Anti-stub audit: state whether template-only output, fixture-specific branching, `TODO`, or `NotImplemented` paths remain in production flow.
- Production behavior artifact matrix: when the invariant names a production signal, state, record, or event, cite producer, consumer, lifecycle, and negative-test proof. Enum definitions, consumers, and seeded tests alone do not prove a production artifact exists.

## Artifact-Backed Evidence

For Governed proof, semantic labels do not count unless the underlying evidence is durable. The execution report must cite `proof/SBxx/manifest.md`, and that manifest must point to existing transcripts or artifacts for:

- failing-first negative proof when behavior changes;
- passing positive proof after implementation;
- source assertions showing the behavior in production code, not only fixtures;
- producer and lifecycle assertions for production-only signals, states, records, or events;
- changed-file hashes;
- anti-stub audit output;
- browser, host, downstream smoke, or red-team artifacts when those proof types are required.

For Governed proof, the semantic gate fails when the manifest is missing or a cited path does not exist. Behavioral proof may keep exact commands and results in the execution report without separate transcripts.

## What Does Not Count

The following evidence can support a gate, but cannot close a critical subbundle by itself:

- a populated execution table
- a status flag transition
- file existence or generated artifact existence
- a non-empty string assertion
- a count assertion without behavior checks
- assertions for diagnostic boilerplate such as headings, template markers, or metric labels
- diagnostic synthesis templates such as `Conclusion: ... supported by N source-backed observation(s)` as positive shipped memory behavior
- happy-path fixture tests that do not vary the key inputs
- a direct capture being used as its own derived proof
- screenshots captured without explicit visual review questions when the work is UI-visible

## Closure Decision

Pass the semantic gate only when the adversarial negative proof would fail the shallow-pass trap and the semantic positive proof demonstrates the intended behavior for realistic input. If either side is missing, mark the subbundle `In progress` or `Blocked` and add the missing proof before downstream work starts.

If a raw note is only partially solved, do not mark it solved in prose. Add a concrete blocker, follow-up subbundle, or exception row with the proof still missing.
