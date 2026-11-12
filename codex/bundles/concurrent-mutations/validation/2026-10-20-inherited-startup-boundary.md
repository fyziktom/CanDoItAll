# Owned startup exhaustion and cancellation boundary

Status: PASS, 40 focused integration cases.

The remaining-time calculation and cancellation check now run inside the existing
owned startup failure boundary. Exhaustion immediately after the durable interval
opens closes measured work as failed; cancellation prevents preparation entry and
closes work as cancelled. Provider/lifecycle failures retain their existing unknown
work behavior. The timer continues to use the original TimeProvider.

Both new deterministic boundary cases pass, along with seven lifecycle, fifteen
coordinator, thirteen registered workspace and three MAF runtime cases. This is a
new production candidate following the earlier 189-case startup baseline, not a
claim that unchanged binaries were reused for it.

Core and Integration Release builds pass. Discovery matches all current method
names and counts. The full portability gate passes with 15,652 unchanged reviewed
findings and no baseline write; documentation, rendering and whitespace checks pass.

Source: 9,777 files, SHA-256
`3E07D22B4F160A2E357D03F50CF810CFD82EFEA2D4D28AF5372EB41E5221D9AD`.
Runtime: 1,449 files, SHA-256
`5DF15891475B70A0E761EA0F9E3A83497E475FC1633EC802129D5CD7DFF87806`.
Both remain unchanged throughout testing. The 406.496-second observer covers all
five commands. No observed process or database-name delta remains; 54 unowned
TEMP entries are preserved.

Proof: `proof/2026-10-19-planning-authority/c4-inherited-startup-boundary.json`,
94,113 bytes, SHA-256
`F9183D544B8A2CC1ACB984084A7D68D2BBDCF4DAA835FC171BFA609D521E489A`.
It is secret-reviewed and retains exact commands, identities and the prerequisite
proof. CodeAnalytics `snap-20261021010656-0b4ceac4` adds no finding, production type,
member or dependency: one Core-to-Models edge, zero project cycles, three unchanged
type cycles and the same 189 workspace diagnostics.

Next: qualify the selected Agent authority snapshot before initial business binding.
The existing coarse catalog lease holds a workspace-wide lock across SQL checks;
the [accepted design](../plan/P07-selected-agent-authority-snapshot.md) reuses the
existing selected-key publication mechanism. Process review/completion, original
execution input, capacity, Workflow and remaining writer/settings integration stay
open. SPA remains on its separate branch, and historical cleanup denials remain.
