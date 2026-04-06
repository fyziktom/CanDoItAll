# Suggested sequencing

1. **Write the missing tests first**
   - add the zero-write/read-path tests,
   - add the explicit repair test,
   - add the unknown-plugin manifest tests.

2. **Remove load-path mutations**
   - delete the `RetireLegacyProjectionRowsAsync(...)` call from `LoadAsync(...)`,
   - remove stale-layout deletion from `LoadAsync(...)`.

3. **Introduce the explicit repair seam**
   - move stale projection cleanup there,
   - make it idempotent and testable.

4. **Upgrade the gate**
   - add phase10 static checks for direct/transitive load-path writes,
   - add required-test presence checks.

5. **Run the full validation**
   - build,
   - required test projects,
   - phase10 gate,
   - attach evidence.
