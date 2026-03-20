# Successful Patterns For This Problem

This section summarizes how this problem is usually solved well in production-like systems.

## 1. Treat this as entity resolution, not simple string matching

The successful framing is:

- each `IndexedScore` is a record,
- the target “same musical work” is an entity,
- grouping is an entity-resolution / deduplication problem.

The mature pattern is **not**:
- normalize one string,
- compare every row with every other row,
- cluster by embedding similarity only.

The mature pattern **is**:
1. prepare robust normalized features,
2. generate candidate pairs with blocking rules,
3. score candidate pairs with multiple signals,
4. cluster only confident matches,
5. route ambiguous cases to human review.

## 2. Blocking is a first-class design concern

Successful large-scale record linkage avoids O(N²) comparison by using **blocking**.

In practice this means:
- compare only records that share strong anchors,
- estimate block sizes before scoring,
- add stricter sub-blocks for “hot” blocks.

For this project, that means things like:
- same canonical composer key,
- same catalog number family,
- same first distinctive title tokens,
- same work type + number.

Bad pattern:
- “we have 200k rows, let’s embed everything and compare all pairs.”

Good pattern:
- “we have 200k rows, let’s narrow candidate sets first, then score.”

## 3. Pairwise scoring plus clustering is more reliable than direct global clustering

A successful workflow is:
- build pairwise evidence,
- assign confidence to edges,
- cluster only edges above threshold,
- apply cluster-level guardrails.

Important guardrail:
- transitive chaining can create junk clusters.
  Example:
  - A ~ B strong
  - B ~ C medium
  - A !~ C weak
  - naive connected-components may still merge all three.

Therefore:
- cluster formation must re-check cluster consistency,
- large or inconsistent clusters must fall back to review.

## 4. Embeddings help, but they are not the main truth source

Embeddings are successful when they are used for:
- candidate expansion,
- tie-breaking,
- strengthening borderline cases,
- clustering within already plausible blocks.

Embeddings are risky when used as:
- the only grouping criterion,
- the only explanation,
- a replacement for structured music metadata.

Why embeddings alone fail here:
- many classical titles are semantically generic,
- movements and full works are semantically close but not identical,
- arrangements can be very similar semantically,
- descriptions may contain noisy editorial differences,
- composer identity matters strongly.

## 5. Use multiple normalization forms, not one “magic normalized title”

Successful systems usually keep at least:
- a strict form,
- a loose form,
- extracted structure,
- aliases.

For this project:
- strict form helps exact deterministic grouping,
- loose form helps recall,
- extracted structure helps safe high-confidence matches,
- aliases help search and review.

## 6. Canonical groups should be curated objects, not only algorithm outputs

A group should behave like a domain object with:
- canonical display title,
- canonical display composer,
- member list,
- review state,
- provenance,
- update history,
- rationale.

The algorithm proposes or updates them.
Human curation can confirm or override them.

## 7. Manual overrides must outrank automation

The successful hierarchy is:

1. explicit curator decision
2. deterministic locked rules
3. high-confidence automatic inference
4. review-only suggestions
5. everything else stays ungrouped

Codex must not build a system where every rerun rewrites curated results.

## 8. Music-domain boundaries matter

Classical and score metadata require domain-aware decisions.

Important distinctions:
- full work vs single movement
- original work vs arrangement
- excerpt vs complete work
- transposition vs arrangement
- editorial version vs work identity

A general-purpose dedupe system often misses these distinctions.
A successful music-grouping system must model them explicitly or at least avoid collapsing them.

## 9. Search systems commonly keep many aliases

A good group model should support many alternate names and search aliases.

That matters because:
- one canonical display title is not enough for discovery,
- the user wants search to reveal alternate variants through group membership,
- composer names also have many valid variants.

## 10. Two-stage retrieval / reranking is the right mental model for embeddings

For this project the recommended pattern is:

- first stage:
  - deterministic filtering / blocking / cheap heuristics
- second stage:
  - more expensive semantic scoring or reranking

This mirrors successful semantic-search and retrieval pipelines and is a safer fit than one-shot clustering.

## Concretely recommended pattern for PDMX Tool

### Use this

- normalization profile per score
- deterministic exact/near-exact grouping for obvious cases
- candidate blocks
- pairwise heuristic scoring
- batched Ollama embeddings
- confidence bands
- non-destructive run preview
- manual review of ambiguous cases
- curated canonical groups

### Do not use this

- single normalized string only
- delete-and-rebuild all groups
- exact key only
- embedding-only clustering
- hidden heuristic weights
- no evidence storage
- modifying the real DB during “tests”
