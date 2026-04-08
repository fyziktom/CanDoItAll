# Required implementation evidence

- no production call sites remain for `EnqueueTrackedAsync(...)`, or they route into the durable runtime plane,
- the bridge does more than observational logging if legacy producers remain,
- plugin/runtime guidance points to the durable scheduler/message plane instead of the old queue.
