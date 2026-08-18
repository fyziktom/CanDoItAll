# Provider usage and cost semantics

## Identities

- Agent observation identity: existing ProviderUsageObservation.Id, enriched with atomic Agent workload attribution and stable execution/run correlations.
- Simple Chat attempt identity: (OperationId, Ordinal).
- Simple Chat logical execution identity: OperationId.
- Consumer identity:
  - Agent: AgentId;
  - Simple Chat: DefinitionId plus optional ConversationId detail.

Provider/model grouping never uses display name alone when a stable profile identity exists; snapshots retain display text for history.

## Counting

- Provider attempts with reported usage count toward tokens/cost even when failed, retried, or cancelled.
- An attempt contributes at most once.
- An operation contributes one success/failure/cancelled execution outcome.
- Transcript messages never contribute a second usage row.
- Terminal operation usage aggregates never contribute a second usage row.
- Both equals the deduplicated union of the two source slices, not a third stored category. For fully attributed evidence, Both equals Agents plus SimpleChats. Any ambiguous legacy contribution is added as a separately reported unattributed completeness row, so it is visible without being assigned to either scoped view.

## Completeness

Usage completeness:

- Observed: authoritative provider/adapter usage exists.
- MissingAfterProviderActivity: dispatch occurred but usage was not returned.
- UsageUnavailable: provider/transport does not expose usage.
- LegacyKnownTokens: legacy nonzero token fields exist but richer detail may be incomplete.

Pricing completeness:

- ProviderReported: immutable provider cost captured.
- CalculatedAtExecution: calculated cost plus pricing version/hash captured.
- Unpriced: usage may be known but no trustworthy historical price exists.

The implementation may reuse existing enums where semantics match, but it must not collapse usage completeness and pricing completeness.

## Legacy rules

- Existing Simple Chat rows with any stored token count become legacy usage-known, pricing-unpriced.
- Existing all-zero Simple Chat rows become usage-unknown unless independent durable evidence proves a known zero.
- Existing Agent observations are Agent only when AgentId/ExecutionRunId and projection evidence are unambiguous.
- ChatSessionId and the term BasicChat never classify SimpleChat.
- Ambiguous legacy evidence remains unattributed and appears only in Both completeness/diagnostics; it is never guessed.

## Source failure

The aggregate query reports source freshness and source errors separately.

- Agents selection requires the Agent source.
- SimpleChats selection requires the Simple Chat source.
- Both may return a partial result only with an explicit incomplete/error state identifying the unavailable source; it must not silently look complete.
- Authorization failure remains fail-closed and is not converted to an empty source.

## Dashboard behavior

- Catalog totals (configured agents, teams, providers, capabilities) remain independent of usage selection.
- Usage observations, tokens, known cost, unpriced count, failure totals, provider chart, and model chart honor selection.
- Agents consumer view ranks Agent rows.
- SimpleChats consumer view ranks definition/conversation rows.
- Both shows combined provider/model totals plus source-specific consumer sections or typed mixed rows with explicit kind.
