# P7-002 - The universal node carrier is still overloaded instead of being a stable carrier plus typed facets and bindings

- Severity: Critical
- Gate: Hard blocker
- Status: Open
- Repeated from: PW6-002

## Problem

ProjectObjectRecord still mixes identity, hierarchy, text, route, external artifact binding, media, storage reference, progress, marker columns, metadata, and scheduling in one broad record. The node should stay the universal carrier, but the carrier itself must stay lean or every new module and connector will keep expanding it.

## Required direction

Keep node identity stable and central. Keep canonical text, status/priority, semantic X/Y, canonical markers, and schedule anchors on the node carrier. Move artifact ids, media/storage payload, provider/resource/secret bindings, and kind-specific business payload into typed facet or binding tables keyed by node identity.

## Closure proof

ProjectObjectRecord no longer owns external artifact/media/storage binding fields; typed facet/binding tables exist; X/Y and canonical markers remain available as canonical node semantics.
