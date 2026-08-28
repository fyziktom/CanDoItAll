# SB07 architecture and root-cause review

## Root causes and boundaries

1. The catalog had no per-model thinking contract. The client policy explicitly made
   all source-managed models Unknown, so the screenshot was current behavior, not a
   stale deployment. Source policy/discovery now produces typed support, control mode,
   allowed efforts and default metadata; the client maps it by routing ID.
2. A label fix alone would not apply an override. The source relay now validates the
   actual request against current source capability, preserves a valid explicit effort,
   or applies its current default when the caller omitted one. No provider-global mutable
   override is used. Prepared-agent fingerprints include capability metadata.
3. Opaque routing IDs bypassed the standard OpenAI temperature omission policy. The
   source now publishes its omission decision; client MAF and the relay both enforce it.
4. Real Responses SDK requests exposed supported fields absent from the relay subset:
   foreground background:false, parallel_tool_calls, empty function descriptions and
   nullable strict. Accept those precise shapes, retaining bounds, tool allowlists,
   rejection of null descriptions, stored/background requests and unsupported tools.
5. Responses ends with response.completed, not the chat [DONE] marker. The stream now
   recognizes a completed response with matching status, captures usage, and finalizes
   before downstream consumption stops. Failed/incomplete/inconsistent terminal events
   fail without forwarding their raw error details. Chat EOF without [DONE] still fails.
6. Model suggestions had used full discovered inventory. A small official main-model
   allowlist is intersected with actual inventory. Publication carries suggestion
   membership; all real models, prices and saved assignments remain intact. Agent and
   Simple Chat selectors sort real labels with natural numeric ordering.

## Ownership and dependency direction

- SharedProviders.Abstractions: immutable protocol data and canonical revisions only.
- ProviderManagement: source/runtime metadata adapter and current-target resolution.
- SharedProviders.Http: bounded protocol validation, dispatch and stream finalization.
- AgentFramework.Models/Core/MAF: per-agent policy, invalidation and request options.
- Existing Blazor components: rendering and orchestration; no new visual framework.

No project/package references, interfaces, composition-root layers or runtime partials
were introduced. No sibling repository source changed. New logic is narrow policy and
adapter code, not a generic service framework. Scoped CodeAnalytics snapshot
snap-20260827223247-84333f15 was healthy with no cycles; its unresolved dynamic/public
contract impact required the three broad test suites. Components MCP was unavailable
(Transport closed); existing component source and actual Playwright MCP were used.
This is a primary-agent review, not an independent reviewer claim.

## Test and compatibility decisions

The original failing-first shared policy, temperature regressions, full SDK envelope,
and Responses terminal regressions are durable tests. The isolated streaming harness
was missing IProviderInferenceRelayRuntime; it now reuses the existing deterministic
runtime adapter. No production DI fallback was introduced. Explicit known thinking
metadata was also added to the old function-call fixture instead of weakening validation.

Old snapshots without optional metadata remain Unknown and keep their canonical hash.
New source fields require coordinated upgrade of strict old clients; all three test
containers are upgraded. Models not on the suggestion list remain valid saved choices.
Unknown or unsupported thinking never becomes inferred from an opaque ID.

The existing evidenced Mini/Luna/Terra Chat Completions plus function-tools restriction
is preserved. The real source also rejected Sol High over Chat Completions at
reasoning_effort. A separate UI-created Responses provider proves reasoning with tools
without silently changing the user's original provider transport or approval policy.
The successful Chat Mini request whose compatibility policy changed Low to None is
explicitly excluded from positive thinking proof.

## Primary protocol references

- https://developers.openai.com/api/docs/guides/reasoning
- https://developers.openai.com/api/docs/guides/latest-model
- https://developers.openai.com/api/docs/models/gpt-5.6-sol
- https://developers.openai.com/api/docs/models/all
- https://github.com/openai/openai-dotnet/blob/main/specification/base/typespec/responses/models.tsp
- https://docs.ollama.com/api/openai-compatibility
- https://docs.ollama.com/capabilities/thinking

Prices are not invented or changed by this phase. Logs expose only model, resolved
effort, override flag, request correlation and allowlisted failure classification;
never credentials, prompts, raw upstream errors or reasoning traces.
