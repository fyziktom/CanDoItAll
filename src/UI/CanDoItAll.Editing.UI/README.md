# Editing renderers

Typed comparison controls for the pure Editing model. Hosts own authorization, observation,
draft capture, freshness, reads and writes. This library depends only on Editing and BaseLib.
Text is rendered as text; no prompt content becomes markup or an external identifier.

`DefinitionSharingFields` renders the two independent **Sharing** options for agents,
Simple Chats and workflows. It receives their values and emits change callbacks. Each
editor owns its draft and save operation.
