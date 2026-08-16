# User Agent Chat regression handoff

Codex must replace placeholders with exact routes, data setup, and known environment constraints.

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
