# Original Request

Source: User request on 2026-05-18.

The previously completed bundle is `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-probing-workbench-repair`.

Requested outcome:

- Add text to speech and speech to text as part of the MAF wrapper as a driver.
- Add the voice driver as its own project in the solution.
- Provide proper interfaces because future TTS and STT providers can include local models.
- Use OpenAI API for the first implementation and use current OpenAI documentation.
- Add agent-module settings for TTS driver, STT driver, and provider connection. OpenAI may use the same API key for both.
- Add setup for voice selection and test samples so the operator can hear the selected voice.
- Add per-agent settings to allow voice mode and optionally select a specific voice that overrides the general voice.
- Let normal agent chat and floating project-structure agent chat turn on audio mode.
- Add voice communication to Cognitive Memory probing. During probing, audio should support a dialogue where the operator says something that should be stored, the system tells the operator to wait while processing, explains how memory understood it and how it wants to store it, and then waits for confirmation such as "yes", "ok", or "this is good, store it".
- Make Cognitive Memory probing more interactive and controllable via audio.
