# Capability integration and small editor confirmations

## Three different operations

| Intent | Existing semantics to retain |
|---|---|
| Toggle assignment on new agent | Stage exact selected capability IDs until parent Save |
| Toggle assignment on saved agent | Existing complete-draft Save path; one admission with footer/Enter Save |
| Verify attached capability | Native diagnostic/proof publication; never an implicit Save of unrelated draft fields |

R10/R11/R12/R13/R20 are the source trail. The list already has a genuine renderer in
AgentFramework.UI. Use its actual options/status/actions or an explicitly justified neutral
sub-boundary; do not rebuild a lookalike list in the module. New Tool/MCP/Skill launch buttons,
filters, current assignment states, selected missing entries and verification messages belong
to A2; the entire definition-creation wizard does not.

## Repair and preserve diagnostic outcomes

S0 requires unsaved-before-Verify preservation. Additionally keep the existing distinctions:
Rejected, CanceledBeforeDiagnostic, DiagnosticInterrupted, Superseded, PublicationCanceled,
PublicationNotStarted, Committed, Unconfirmed and InfrastructureUnavailable. UI projections may
simplify language but not collapse every result into 'failed; retry'. Existing proof recovery can
show satisfied/not-published/superseded/unconfirmed without running the diagnostic again.
Do not invent a new execution engine or refactor the public API for cosmetic consistency.

Verification checks exact agent/capability/provider inputs and publishes a new agent version.
This is not a harmless read. Require one admitted attempt; preserve exact original identity and
safe stage/receipt facts. Never move a late result to another agent, same-ID new editor or a newer
capability attempt. Keep pending and genuinely unknown publications fenced until correctly reviewed.

## Wizard create/assignment boundary

Current flow records only a `created` bool before optional capability-list reading. The returned
capability ID is a known independent fact: retain it, its originating editor and intended assignment
before awaiting another read. A catalog read or parent Save failure must not encourage duplicate
capability creation. Read-only retry refreshes lists; assignment retry is an explicit exact intent.
A new-parent agent stages assignment; an existing one uses its actual complete-draft Save semantics.
If the parent closed, creation may still exist, but no late automatic attachment to a successor.

Do not change wizard internals or add a new wizard to satisfy A2. For production integration use a
safe no-network fixture tool/skill in the real existing wizard. MCP launch/probe is disabled unless
a separately owned safe fixture is explicitly configured. Capture native ID/count and selected
parent state before and after induced catalog/read-back failures.

## Confirmations

Extract editor-specific Delete, automatic-approval and workspace-risk dialog markup into the
appropriate light presentation boundary while retaining compatible entry points. Preserve actual
acknowledgement checkbox, confirm/cancel/escape/focus behavior and managed-agent restrictions.
Deletion keeps its current owner/journal and committed/unconfirmed semantics; no force delete.

Each opener captures editor origin, target, requested permission/action and relevant revision.
A changed editor or superseding choice retires only its own prompt. Normal dismissal is not
failure of another overlay. A delayed confirm does not enable a new target. Multiple editors and
nested Storage/wizard dialogs must remain independent; no global DialogService.CloseAll fix.

Test real confirmation components in both composed sandbox and production host. Neither resetting
a displayed checkbox nor a disabled button replaces the handler's current-origin/policy checks.
