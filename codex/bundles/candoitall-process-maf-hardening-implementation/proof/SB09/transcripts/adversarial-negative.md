Command: semantic negative proof for pre-implementation Process MAF hardening invariants
ExitCode: 1
Result: Pre-implementation behavior violated the closure invariants and is preserved as the failing-first/adversarial baseline from GPTPro F01-F12 plus SB01 inventory.

Invariant IDs covered: INV-SB01-01, INV-SB02-01, INV-SB03-01, INV-SB04-01, INV-SB05-01, INV-SB06-01, INV-SB07-01, INV-SB08-01, INV-SB09-01.

Negative cases:
- INV-SB01-01: stopping at only `prepare-solution-skeleton` misses eight other subprocess parent steps.
- INV-SB02-01: truncated AgentFramework observation lookup can miss the exact blocked process step and produce blind retry advice.
- INV-SB03-01: raw prose-only process result summaries cannot reliably drive blocked-step rework.
- INV-SB04-01: prose-only subprocess mappings cannot distinguish accepted child handoff from no-go escalation.
- INV-SB05-01: child folder existence is not valid parent evidence.
- INV-SB06-01: raw-output-only artifact hashes and original-command ledgering can record invalid artifacts.
- INV-SB07-01: missing runtime tools discovered after LLM execution can repeat manager loops.
- INV-SB08-01: template manual skip without typed already-satisfied output can skip required parent evidence.
- INV-SB09-01: proof without source hashes, command transcripts, and regression tests is rejected.
