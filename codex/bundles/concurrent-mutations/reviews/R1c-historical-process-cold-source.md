# Historical Process read graph: source extraction

Source prepared in the managed continuation checkout only. No compilation, execution, database observation or cold no-write proof is claimed. The companion JSON records the ten production inputs and their exact predecessors before this extraction.

`ProcessHistoricalReceiptRead.cs` separates original-evidence lookup and historical current-read observation from ordinary dispatch interfaces. `ProcessOriginalReceiptAuthorityReader` calls the existing EF original-evidence query with the initialized canonical profile; it does not construct an execution store. `ProcessHistoricalReceiptSourcePolicy` validates the captured run and current host fence before and after observation and always returns `DispatchAllowed: false`.

The public `ProjectProcessHistoricalReadAuthority` owns the moved historical source policy and historical receipt-claim overload. Its dependencies are the narrow original-evidence query, initialized profile/generation, historical catalog, existing Projects lifetime reads, API options and clock. It has no launch target query, structure contributors, mutation guard, transaction coordinator or ordinary catalog lease. Original execution creation time, execution/run/step/executor evidence, source fingerprint and project reference remain compared. Current Agent policy and source lifetime are observed around awaits; a current profile/generation change refuses. Historical Agent disclosure requires the current schema and original read ceiling. An old generation is not rewritten or admitted for dispatch. The server-only current context rejects empty profile identity, blank fingerprint and negative generation.

Peer review found that the moved `RequireCurrentAsync` call would select the source lifetime with `FOR KEY SHARE`. The new historical helper now uses the existing nonlocking `CaptureObservationAsync` and compares the exact observed admission, including retirement filtering. The ordinary launch/mutation helper is unchanged. This is a deliberate read-path correction, not a claim that the separate observations form an atomic cross-owner snapshot.

`ProjectProcessReadAuthorityPolicy` shares only pure operator fingerprint/expiry, source project and current grant checks. The ordinary launch authority retains its original catalog lease, original-generation equality, mutation grant and target checks. Its API policy fingerprint serialization fields, hashing and rejection messages are unchanged. The existing R1b `AgentToolReceiptReconciliationClaim` overload and ordinary original-evidence query interface remain intact. The three ordinary owner files have the same source text as the current main parent after newline normalization; the dispatch interface likewise matches after removal of the authored historical member.

Composition remains owned by the runtime peer. The exact proposed registrations, using the existing scoped owner convention, are:

```csharp
services.AddScoped<IProcessOriginalReceiptAuthorityReader, ProcessOriginalReceiptAuthorityReader>();
services.AddScoped<ProjectProcessHistoricalReadAuthority>();
services.AddScoped<IProcessHistoricalSourceAuthorityObserver>(provider =>
    provider.GetRequiredService<ProjectProcessHistoricalReadAuthority>());
services.AddScoped<IAgentToolHistoricalBackgroundSourcePolicy, ProcessHistoricalReceiptSourcePolicy>();
```

The peer's dedicated Projects historical receipt provider consumes the public owner and its internal historical claim method within Workbench. It must not resolve the ordinary mutation/recovery service indirectly. No new package, project reference, registration or test was added in this subset.

Source inspection parsed all ten files with the locally available Roslyn parser: zero syntax errors. This does not establish compilation or runtime registration completeness. Existing historical proof rows remain unchanged; the broader cold-graph owner controls the final source-derived inventory. Required subsequent proof includes actual initialized cold DI construction, forbidden ordinary owner resolution, filesystem publication observation, real Projects/Process receipt authorization, current policy/lifetime/context refusal, cursor witness stability and actual process restart. Filesystem watchers alone do not prove SQL behavior or a linearizable snapshot across files and SQL.
