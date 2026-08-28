# SB09 semantic invariants

- SB09-I1: one typed source policy resolves administrator override, discovery, then
  built-in definition. Persisted overrides survive health refresh; automatic reset
  removes only that model's override. Unknown is not silently called unsupported.
- SB09-I2: invalid support/mode/effort/default combinations and duplicate model IDs
  fail before persistence. Unrelated provider JSON remains intact. Each agent owns
  its own override; omitted client effort uses the current source model default.
- SB09-I3: shared imports consume only published capabilities, never local guesses
  or client edits. Explicit refresh preserves unsaved agent selections and retired
  import intent; failures remain visible.

## Production artifact matrix

| Producer | Consumer | Actual proof |
| --- | --- | --- |
| ProviderModelThinkingConfiguration + ProviderProfileService | Local policy and normal provider save | ProviderModelThinkingConfigurationTests; editor interaction tests |
| SharedProviderThinkingCapabilityMapper.ToCatalog | Existing source catalog/import policy | SharedThinkingEffortTests; shared publication tests; SB10 mirrored table |
| AgentThinkingEffortPolicy | Runtime preparation and upstream relay | Adapter/policy tests, 56 integration cases, real source dispatch |
| SharedProviderRefreshButton | Existing source service and agent provider list reload | Refresh component tests; 5214 stale-to-current UI proof |

## Shallow-pass traps and negatives

Do not merely enable a dropdown, infer capabilities from an opaque route, freeze the
source default on the client, invent model names, erase manual intent on discovery,
or accept the same effort for every model. Nine original red tests and the final
138 Unit/35 Components/56 Integration cases check these behaviors. Live empty-option
configuration is rejected; GPT-4.1 cannot accept an override. Exact discovered and
executed test names are compared by Collect-Closure.ps1, not just totals.

## Invalidation

Reopen on configuration schema, precedence, save validation, catalog mapping or
runtime policy changes. The final layout-only adjustment invalidated component and
browser proof; both were rerun. Broad fallback was honored once at the frozen
checkpoint; it is not rerun for CSS/grid-only changes.
