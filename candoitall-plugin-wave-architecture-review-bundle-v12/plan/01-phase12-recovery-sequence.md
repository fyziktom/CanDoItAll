# Phase12 recovery sequence

## Non-negotiable order
1. **Restore phase10 closure first**
   - zero-write Workbench reads,
   - projection repair boundary,
   - unknown-manifest shared editor proof.

2. **Run the phase10 gate on the current repo until it is green**

3. **Only then implement the runtime-plane work from phase11**
   - execution-plane separation,
   - trigger registry + Quartz,
   - durable message plane,
   - hosted workers,
   - ingress inbox,
   - observability.

4. **Run phase11 gate and phase12 gate on the current repo**

5. **Provide evidence**
   - current gate outputs,
   - required tests present,
   - migrations/snapshots updated for durable runtime records.

## Why the order matters
The current uploaded ZIP is below the previous validated baseline.
Continuing phase11 work without first restoring phase10 would make the runtime plane sit on top of a non-deterministic Workbench core.
