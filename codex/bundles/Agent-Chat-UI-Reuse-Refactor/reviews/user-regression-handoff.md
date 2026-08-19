# User Agent Chat regression handoff

## Exact setup

1. From `C:\repositories\CanDoItAll`, start `src/App/CanDoItAll.Web/CanDoItAll.Web.csproj` with the normal local database and configured Agent providers.
2. Use a large desktop viewport, preferably 1600 × 1000 or larger.
3. Primary Agent Chat: `/agents?tab=chat`.
4. Floating Agent chats: use the left-shell `Agent chats` action.
5. Agent identity/runtime settings: `/agents?tab=agents` and open an Agent editor.
6. Floating lifecycle/preparation settings: `/agents?tab=floating-chat`.
7. Process consumer: `/processes`, select an existing definition, then select `Manager chat`.

Unchecked items below are the user's live regression/approval checklist. Codex automation already confirmed:

- [x] Main `.NET Application Developer` exact response `MAIN AGENT CHAT OK`.
- [x] Floating Delivery QA Observer exact response `FINAL FLOATING AGENT CHAT OK`.
- [x] Floating detach/follow, hide/keep-active, reopen persistence, history, stop, and zero-active state.
- [x] Prompt Gallery opens and lists canonical items; unavailable voice state is explicit.
- [x] Identity/provider/model load and unchanged settings save through the Agent editor.
- [x] Process Manager chat loads the shared conversation workspace and composer.
- [x] Browser console reports 0 errors and 0 warnings at 1600 × 1000.

## Catalog and Agent selection

- [ ] Agents page loads
- [ ] search works
- [ ] tag/team filters work
- [ ] favorites preserve ordering and toggle behavior
- [ ] card details and badges are correct
- [ ] switch dialog opens, filters, selects, and closes
- [ ] new chat and history actions target the correct Agent

## Sessions and transcript

- [ ] existing sessions load
- [ ] selecting a session loads the correct transcript
- [ ] search filters the same sessions as before
- [ ] new session works
- [ ] title rename works
- [ ] user and assistant messages render correctly
- [ ] markdown, copy, timestamps, token metadata, and hidden context match prior behavior
- [ ] scroll and focus behavior remain usable

## Sending and Agent execution

- [ ] normal prompt sends
- [ ] busy/disabled states are correct
- [ ] long response remains usable
- [ ] cancel/stop behavior works
- [ ] error presentation and retry/recovery behavior remain correct
- [ ] execution activity and runtime detail dialog work
- [ ] approval request, approve, reject, and auto-approval controls work

## Composer extensions

- [ ] attachments can be staged, removed, and sent
- [ ] prompt gallery action works
- [ ] voice controls work when a provider is configured
- [ ] unavailable voice state remains correct

## Floating Agent chats

- [ ] open floating catalog
- [ ] search/select Agent
- [ ] open new floating chat
- [ ] open history
- [ ] switch active chats
- [ ] hide and reopen
- [ ] close versus stop semantics are correct
- [ ] active limits/retention still apply
- [ ] context access and affinity follow/detach behavior work
- [ ] overlay placement, focus, layering, and internal scrolling are correct

## Settings

- [ ] identity fields load and save
- [ ] avatar choose/default/generate behavior works
- [ ] summary and instructions save
- [ ] provider and model selector/default/override work
- [ ] thinking effort and current advanced settings work
- [ ] status/workload/history/approvals remain correct
- [ ] Memory, Images, capabilities, tools, skills, governance, and other Agent-only tabs work
- [ ] delete/version/concurrency/error paths remain correct
- [ ] floating lifecycle and prepared-Agent settings save correctly

## Other consumers

- [ ] contextual Agent workspace windows work
- [ ] Process workspace Agent context/chat integration works

## Approval

User result:

- [ ] approved for a separate Simple Chat UI bundle
- [ ] issues found; reopen Phase 1

Notes:

- Voice recording requires a configured browser/audio provider.
- Approval prompts, retry/error recovery, concurrency, destructive settings, and attachment sends require suitable live scenarios and should be exercised against disposable data.
- The final Stable gate recorded three unrelated failures in untouched LlmChats integration tests; the affected Components suite passed 990/990. See `proof/SB09/final-test-execution.json`.
- Simple Chat UI remains inactive until the approval checkbox above is explicitly selected by the user.
