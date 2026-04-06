# Plugin ingress inbox and materialization boundary

## Decision
Any external stream-like plugin input must first land in a durable ingress inbox.

## Examples
- email polling
- WhatsApp synchronization
- webhook callbacks
- CRM sync events
- batch import scans
- external notification streams

## Why
These sources need:

- external IDs,
- cursors,
- dedupe,
- replay,
- quarantine,
- explicit materialization,
- failure visibility.

Those are execution-plane concerns, not direct Workbench concerns.

## Recommended exact types for phase11
- `PluginIngressEnvelopeRecord`
- `PluginIngressState`
- `IPluginIngressInbox`
- `IPluginIngressMaterializer`
- `PluginIngressCursorRecord`

## Required rule
An ingress envelope may remain unmaterialized.
Only an explicit materializer may turn it into domain artifacts such as nodes, tasks, notes, transcripts, or resources.
