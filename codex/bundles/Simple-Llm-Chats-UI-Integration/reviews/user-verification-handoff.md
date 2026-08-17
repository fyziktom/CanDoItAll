# User Verification Handoff

Final execution must leave this checklist for the user and mark only automated items as automated.

## Automated Browser Proof

- [x] Automated: main Simple Chat returned exact `MAIN_SIMPLE_OK` and settled ready for another message.
- [x] Automated: floating Simple Chat returned exact `FINAL_FLOATING_SIMPLE_OK`, hid, remained Active, reopened, and preserved its transcript.
- [x] Automated: floating Agent chat followed `Agents · Overview`, returned exact `FINAL_FLOATING_AGENT_OK`, used Keep active, reopened, and preserved its transcript.
- [x] Automated: unified `Available / Active` and `All / Agents / Chats` controls rendered and remained usable at 1440x1000.
- [x] Automated: the browser reported zero console errors at the final scenario checkpoint, before intentional runtime shutdown, and the managed app reported no new agent-optimized error log entries.

## Existing Agent Regression

- [ ] Edit and save an Agent identity/runtime setting.
- [ ] Open main Agent chat and send a normal message.
- [ ] Open Project Structure and chat with an allowed Agent using current context.
- [ ] Verify tool approval behavior remains unchanged for an approval-requiring Agent.
- [ ] Open floating catalog, Agent history, hide/reopen, affinity detach/follow, and stop.
- [ ] Verify Process-manager/other contextual Agent chat still opens.

## Simple Chat Definitions

- [ ] Create a definition with avatar, system prompt, provider/model, and temperature.
- [ ] Edit it, review revision metadata, activate it, and verify stale-editor conflict handling.
- [ ] Verify suspended/archived definitions cannot start a new conversation.

## Main Simple Chat

- [ ] Create a conversation and send a short response.
- [ ] Use a slow/local model and observe incremental Assistant text.
- [ ] Refresh the browser during a response and verify the operation reconnects without a duplicate answer.
- [ ] Cancel an active response and verify the conversation becomes usable again.
- [ ] Rename and archive a conversation.
- [ ] Exercise recovery/reconcile UI with the provided deterministic test/simulator path.

## Floating Simple Chat

- [ ] Filter `All / Agents / Chats`.
- [ ] Start a Simple Chat, stream a long response, hide/reopen the window, and open history.
- [ ] Verify no Project Structure/context badge or automatic context is shown for Simple Chat.
- [ ] Verify closing the window does not cancel the active response.
- [ ] Verify explicit Cancel does cancel it.

## Profile And Layout

- [ ] Switch database profile and verify old-profile chat projections disappear fail-closed.
- [ ] Confirm no horizontal page overflow at normal large desktop size.
- [ ] Confirm dialogs and floating windows keep actions visible and scroll internally.
