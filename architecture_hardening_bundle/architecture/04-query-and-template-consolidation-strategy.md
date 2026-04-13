# Query and template consolidation strategy

## Query-side target

Introduce focused query services for common read surfaces, such as:
- definition list summary,
- editor load,
- run detail,
- analytics summary.

Exact class names are flexible, but each query service should:
- shape the minimal necessary projection,
- avoid unnecessary full-graph loading,
- remain projection-only.

## Template/shared-helper target

Separate helper extraction into two buckets.

### Truly shared, neutral helpers
Candidate examples:
- JSON file read + deserialize helper,
- text/slug helper,
- generic enum text parser with explicit default behavior.

These may live in a neutral shared or infrastructure location.

### Process-template-domain helpers
Candidate examples:
- role snapshot summary builder,
- template-specific projection transforms.

These should remain process-template-owned, even if reused by multiple process-template services.

## Extraction rule

Before extracting any duplicated code, answer:
1. Is the behavior genuinely generic?
2. Is it reused at least twice with the same semantics?
3. Will moving it improve ownership clarity rather than blur it?

If the answer is not clearly yes, keep the helper module-local.

## Read/query proof expectation

After query extraction, proof must show:
- correctness remained intact,
- the common query shape became slimmer or more intentional,
- no query service became a second domain mutation path.
