# API server-sent events performance review

## Scope

Static review of the 23 content-changing production C# files in the SSE/API
increment (6,835 lines at review time). This covers the shared replay transport, agent and
provider streams, workflow/process adapters, API configuration, and activity
reader integration.

## Automated pattern scan

| Pattern | Count | Assessment |
| --- | ---: | --- |
| Synchronous `.Result` | 1 | Safe fast path guarded by `ValueTask.IsCompletedSuccessfully` |
| Blocking `.Wait(...)` | 0 | No finding |
| `GetAwaiter().GetResult()` | 0 | No finding |
| `async void` | 0 | No finding |
| `Task.Run(...)` | 0 | No finding |
| `.AsTask()` | 3 | Used only for incomplete replay/heartbeat `ValueTask` coordination |
| `new JsonSerializerOptions` | 1 | Created once for a cached static instance |
| `new Regex(...)` | 0 | No finding |
| `ThreadPool.UnsafeQueueUserWorkItem(...)` | 1 | Coalesced subscriber wake-up; no request context is consumed |
| Blocking whole-file I/O | 0 | No finding |
| `Parallel.For` / `Parallel.ForEach` | 0 | No finding |

## Hot-path assessment

- Replay retention is fixed by `ReplayCapacity`; a subscriber cannot make the
  publisher allocate an unbounded queue.
- Publication holds a short lock for a ring-buffer update and queues one
  coalesced completion signal. It does not serialize JSON or write to a client.
- A synchronous `ValueTask` fast path avoids task allocation when retained data is
  already available.
- Each non-empty read allocates and copies at most `MaxBatchSize` entries. The
  default bound is 128.
- JSON is serialized directly to the response body with one cached serializer
  configuration. Complete frames are not first materialized as JSON strings.
- Subscriber cancellation does not cancel the shared publication signal. Profile
  rotation cancels retired readers after releasing the profile-state lock.
- Agent/provider command cleanup requests cancellation, waits for at most five
  seconds, and observes a later completion if a runtime ignores cancellation.

## Accepted limitation

All workflow or process subscribers for a profile share one replay lock and
wake-up signal. Readers copy their bounded batch while holding that lock, so high
fan-out can create a reader lock convoy and delay publication or profile rotation.
That is acceptable for the requested local/basic stream, but it is not suitable
evidence for thousands of subscribers or token-rate events.

Before expanding that scale, benchmark publisher latency and allocation under
representative fan-out. Likely follow-up designs are run-keyed partitions,
pre-serialized public envelopes, and a transport intended for cross-process event
distribution.

## Review conclusion

No closure-blocking performance anti-pattern was found for the current scope.
Memory, publisher work, replay batches, and disconnected-command draining are
bounded. The high-fan-out lock-convoy risk remains explicit and should be measured
before the stated operating envelope changes.

This is an AI-assisted static review. Validate material scale decisions with a
targeted benchmark and human review.
