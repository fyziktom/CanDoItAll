# Safe failure outcome refinement

The MAF provider boundary deliberately removes inner exceptions for shared/imported profiles. A history-only walk of InnerException therefore cannot identify HttpClient deadlines once sanitized. Reintroducing the raw exception would undo the disclosure boundary.

ProviderFailureBoundaryException now carries a readonly IsTimeout boolean derived inside the existing disclosure policy. It preserves an explicit timeout cause, including an already sanitized timeout boundary, without retaining private text, headers, URI or exception objects. MAF's small ProviderHistoryFailureOutcome translator consumes this typed fact and explicit TimeoutException causes; an arbitrary independent OperationCanceledException is not fabricated into TimedOut. Caller cancellation and already observed terminal success/usage remain separate contracts.

No protocol field, serialized exception, new DI abstraction or database migration. The failing-first boundary test has two sanitized cases fail and two unsanitized controls pass; the repaired unit suite and actual OpenAiProviderDriver HttpClient-deadline persistence test pass. Source and evidence are indexed in proof/SB09/semantic-invariants.md.
