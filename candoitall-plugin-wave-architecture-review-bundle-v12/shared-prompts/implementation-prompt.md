Use this bundle as a recovery-first execution package.

Rules:
1. Work on the current repo only.
2. Restore phase10 closure first.
3. Run the phase10 gate and keep iterating until it is green.
4. Only then implement the runtime-plane work required for the next plugin wave.
5. Do not rename required symbols/tests.
6. Update EF migrations/snapshots for all durable runtime records.
7. Finish by providing current runs of:
   - phase10 gate,
   - phase11 gate,
   - phase12 gate.
