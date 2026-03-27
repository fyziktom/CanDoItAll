# QA Prompt

Validate the blocks explorer against the extracted screenshots and bundle requirements.

Check:

- the explorer now shows standard minimize, reset, and hide controls in the floating-window header
- the explorer can be dragged like the other shared floating windows
- clicking a section reveals its items and the browser-visible body reads like an accordion
- search results remain scrollable inside the explorer window and visible labels stay readable
- screenshot proof exists for the default explorer and for a filtered or scrolled result state
- this repo does not use Microsoft Testing Platform yet, so `mtp-hot-reload` is not expected for this pass
