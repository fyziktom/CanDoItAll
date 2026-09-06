# Frozen sandbox context contract

Before production changes: browser phase `context` expects explicit card-states/flexible query to render that specimen and survive reload. Existing source ignores the query; retain this failing-first run.

Unit `CatalogSandboxContextTests`: Defaults_are_stable (1), Explicit_context_round_trips (5 scenarios), Invalid_tokens_use_defaults (3), Unknown_or_malformed_ids_are_not_accepted (3), Selection_ids_are_validated_independently (1). Expected new discovery: 13; existing CatalogAssetModeTests: 5; combined Unit discovery: 18. Counts are execution inventory, not architecture assertions.

Browser per mode: explicit card/flexible reload, five scenario reloads, agent/team/matched restoration with unchanged history length, invalid query normalization: 8 named checks. Existing real-component asset/tooltip/normal acceptance remains in the `acceptance` phase. The later diagnostic explicitly navigates to normal/matched without selection before applying its frozen predicates.
