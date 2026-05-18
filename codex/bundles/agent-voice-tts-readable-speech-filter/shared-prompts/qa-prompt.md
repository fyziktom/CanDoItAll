# QA Prompt

Validate the bundle against the original raw notes, not only the implementation summary.

Check:

- Full GUIDs are removed from speech text.
- Truncated hex IDs with ellipsis are removed from speech text.
- Ordinary dates, numbers, and normal prose remain.
- Notice is included only when IDs were omitted and suppression is false.
- Notice can be suppressed through the synthesis request.
- Normal and floating chat do not repeat the notice every time in the same conversation.
- Visible chat content still contains IDs.

Required proof:

- Targeted unit tests for `AgentVoiceTests`.
- Targeted component tests for chat voice controls where relevant.
- Solution build.
- Browser smoke for `/agents?tab=chat` or an explicitly documented blocker.
