# Assumptions And Risks

## Assumptions

- Absolute path bullets under `## Exact Source References` are the primary place where reference quality should be validated automatically.
- Feedback-profile bundles should always carry `## Status` and `## Raw Note Closure` scaffolding in `reviews/01-execution-report.md` even before implementation starts.

## Risks

- Over-validating markdown could reject older bundles that are structurally sound but were prepared before the stricter rules existed.
- Regex-only path extraction may miss unusual markdown patterns unless the validator is kept intentionally narrow and documented.

## Mitigation

- Limit automated checks to explicit markdown bullet references under the named section instead of scanning arbitrary prose.
- Make the new checks fail only on missing or obviously malformed required content, not on stylistic variation outside the contract.
