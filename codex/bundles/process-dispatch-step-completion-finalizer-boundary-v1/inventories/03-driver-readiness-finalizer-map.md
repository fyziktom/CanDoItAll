# Driver Readiness Finalizer Map

Documentation only. Do not implement driver API.

| Finalizer concept | Future driver relevance | Current owner |
| --- | --- | --- |
| Artifact producer kind | Future drivers may declare producer/source family | local finalizer vocabulary |
| Artifact expectation mode | Future drivers may state what evidence they can satisfy | local finalizer vocabulary |
| Artifact validation status | Future drivers may return candidate evidence requiring validation | finalizer validation result |
| Failure ownership | Future recovery/manager drivers may route blame/rework | finalizer validation result |
| Runtime invariant violation | Future drivers must not bypass invariant checks | finalizer invariant audit |
| Artifact content read result | Future document/spreadsheet drivers may provide content facts | artifact content reader boundary |
| Transition artifact validation context | Future driver outputs must preserve source/run lineage | transition request builder |
