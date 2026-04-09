# Metrics, capacity, and conformance

The module should measure value flow, not only step counts.

## Core flow metrics

- lead time
- touch time
- queue or wait time
- blocked time
- first-time-right
- rework rate
- SLA attainment
- bottleneck frequency
- capacity load
- customer acceptance / feedback

## Handoff-specific metrics added in this pass

Because the process is the canonical collaboration graph, the module should also be able to measure:

- baton wait before acceptance
- triage routing frequency
- reroute / rebind frequency
- direct override frequency
- external executor session success/failure by process step (future seam)
- percentage of live runs with overlay-visible bottlenecks

## Conformance outputs

- deviation record
- deviation cluster
- paper-vs-reality review
- improvement candidate
- training opportunity marker

## Guardrail

These outputs must remain evidence-oriented and privacy-safe.  
The conformance system is not a rumor registry about people.
