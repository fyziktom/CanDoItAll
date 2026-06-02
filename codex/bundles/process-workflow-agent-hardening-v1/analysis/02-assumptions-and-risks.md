# Assumptions And Risks

## Assumptions

- Codex will execute this bundle inside a checkout of `fyziktom/CanDoItAll` on branch `development`.
- The input evidence packet remains available at `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/`.
- Local API hosts and database profiles may differ during execution; therefore runtime profile identity must be verified before any live API proof.
- The active Codex skill root may not automatically reflect repository skill edits; skill synchronization proof is required when skills are changed.
- Some provider calls may be private/local and should be represented as private-provider cost categories rather than forced into OpenAI pricing.
- External mailbox tests should use dry-run or dedicated disposable test categories unless a subbundle explicitly authorizes side effects.

## Critical Path Risks

1. **Contract drift remains after refactor.** Splitting files without canonicalizing identifiers and state semantics will make the system prettier but not safer.
2. **Token/cost undercount persists.** A process-level actual cost based only on persisted run metrics can miss finalizer short-circuits, structured-output repair, failed-after-provider-call usage, workflow summarization calls, and usage-null states.
3. **Browser proof can be falsely accepted.** If browser tool availability is not traced from operation contract to runtime tool catalog to captured artifacts, a process can claim proof from stale or unrelated evidence.
4. **Current-run lineage remains weak.** A stale run id already appeared in agent output lineage; final proof must reject artifacts not bound to the current process run and execution run.
5. **External side effects are unsafe.** Email workflows can move or mark messages. Without idempotency and dry-run semantics, regression runs can mutate real mailboxes.
6. **Build/run host locks can poison validation.** Existing host processes can lock build outputs or point API tests to the wrong database profile.
7. **Skill/template edits may not affect active Codex behavior.** Repository skill updates must be synchronized to the active skill root and proven with hashes before downstream subbundles rely on them.

## Validation Risks

- Unit tests may pass while semantic proof is still weak.
- Integration tests may use fixture-specific strings rather than exercising generic process behavior.
- UI component tests cannot replace Playwright proof for browser-visible behavior.
- A process run can complete while post-release learning evidence is too qualitative.
- Provider usage may be unavailable for cancelled/background responses; the UI must explicitly represent unknown usage rather than hiding it.
- Numeric enum HTTP shape can be misread by agents or UI if not consistently translated.
- Available/inavailable workflow executors may be displayed without clear execution diagnostics.
- Cost reconciliation cannot rely on exact OpenAI billing UI timing; it must compare internal ledger against provider response usage and optionally against exported provider usage when available.

## Reopen Triggers

Reopen the earliest affected subbundle when any of these occur:

- A new magic string or JSON path is added outside a canonical descriptor/constant.
- A process artifact is accepted without current process run id, process step id, and execution run id binding.
- A browser proof artifact lacks route, viewport, host, screenshot path, console evidence, and current-run binding.
- A provider call occurs without a durable usage observation or an explicit usage-unavailable record.
- A failed execution run has nonzero provider activity but zero or estimated-only token usage without diagnostic status.
- A required finalizer path returns tokens as zero while provider usage was available in streaming updates.
- A workflow executor performs side effects without idempotency key and processed-marker proof.
- Active skill root hashes do not match repository skill hashes after skill changes.
- The five-domain E2E suite exposes Tetris-specific assumptions.
- A later subbundle finds that an earlier critical foundation only passed shallow or fixture-specific tests.
