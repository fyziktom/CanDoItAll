# Subbundle result — M04

## Anchor

- Repository commit before: `386d8beb6038035f89a9a6961ec017d8213879a5` with accepted M00-C1 working-tree changes
- Dependency mode: package
- Windows host: Windows x64; SDK `10.0.303`; runtime `10.0.11`
- Linux host: Docker Linux x64; SDK `10.0.302`; runtime `10.0.10`

## Implemented behavior

Local stdio MCP now uses a persistent buffered reader and one serialized writer. While awaiting its own response, the connection answers peer `ping` requests, returns JSON-RPC method-not-found for unsupported peer requests, ignores bounded notifications, and continues waiting for the original response.

New strongly typed transport failure kinds distinguish invalid JSON, invalid envelopes or IDs, oversize messages, duplicate IDs, excessive unmatched traffic, EOF, process exit, and I/O failure. Newline and content-length frames are bounded to 8 MiB, decoded as strict UTF-8, parsed at depth 64, and retained across buffered reads. Unmatched traffic is capped at 64 messages and string IDs at 128 characters. Outgoing frames and tool-argument JSON use matching bounds.

The process host can retain a bounded stderr tail; MCP selects a 16 KiB tail but withholds it from setup errors, so protocol and process-exit failures remain redacted. The existing operation timeout and owned-process cleanup remain authoritative. Initialization still advertises no callbacks or capabilities.

## Commands and results

| Scope | Result |
|---|---|
| Windows Unit project build | PASS, 0 warnings/errors |
| Windows MCP runtime/contract/policy/payload unit slice | PASS, 64/64 |
| Windows MCP portability integration class | PASS, 28/28 |
| Linux Integration project build | PASS, 0 warnings/errors |
| Linux MCP process-host/payload unit slice | PASS, 18/18 |
| Linux MCP portability integration class | PASS, 28/28 |
| CodeAnalytics scoped refresh | PASS, `snap-20260812134623-678a8a60`; no blocking errors or dependency cycles |

## Validation reuse/invalidation

- Invalidated keys: MCP stdio framing, peer-message response loop, transport failure contract, stderr capture policy, and the M08 integrated Windows/Linux candidate.
- Reused evidence: M01 persisted plan semantics, M02 dependency provenance, and M03 owned-process lifecycle.
- Reason reuse is valid: M04 consumes the M03 process host without changing its ownership identity or receipt contract and does not alter persisted plans or dependency selection.

## Residuals

`notifications/cancelled` remains intentionally unimplemented and unadvertised. It is a non-blocking optional extension rather than part of this bounded response-loop contract.

## Decision

`GO`

## Next eligible subbundle

M05
