# Codex Gap Analysis

## Why the previous pass still looks incomplete

Codex followed the letter of the previous bundle more than the full semantic intent. It added useful collaborators, tests, and proof manifests, but several changes are still narrow or heuristic.

The most important pattern is this: Codex tends to close a subbundle when a new local test passes, even if the implementation only satisfies that test's wording. The current workflow skill now asks for artifacts, but it does not yet force a requirement-by-requirement behavioral invariant matrix that can catch narrow solutions.

## Examples

- The process validator now requires artifact-backed manifests, yet the completed bundle can fail after path relocation because proof uses machine-specific Windows absolute paths.
- Clustering now has composite edge scoring, but candidate pair discovery still depends mostly on exact shared keys.
- Dreaming now has a claim synthesizer, but it is a common-prefix/string-join synthesizer and the claim signature ignores the claim text.
- Professor learning now has anchor states, but natural capture remains keyword-dependent and explicit capture bypasses structured professor extraction.
- Assimilation now checks independent support and repeated use, but mastery is still inferred from text keywords rather than domain events.

## What must change in this bundle

- First harden the workflow skill again so completion proof becomes portable and invariant-backed.
- Add failing-first tests for the remaining exact gaps before touching production code.
- Require Codex to prove behavior on adversarial examples, not only happy-path unit tests.
- Force changed-file source assertions that describe why each modified production file is necessary and which invariant it satisfies.
- Use completed-stage validation plus an explicit red-team review before closing.
