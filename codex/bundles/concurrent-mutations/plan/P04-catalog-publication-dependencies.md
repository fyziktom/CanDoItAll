# Catalog publication after independent execution writes

The selected-authority SQL probe failed. All six writers completed while the SQL
holder remained, but both grant writers threw `SandboxWorkspaceConcurrencyException`;
three writer times exceeded the unchanged one-second criterion. Retain candidate
`c4-selected-publication-cost-validation` and its measurements as failed evidence.

The catalog-only writer currently witnesses the whole workspace index and all execution
projections while releasing the workspace guard to acquire selected Agent keys. An
independent checkpoint legitimately changes those projections, so it invalidates a
catalog proposal which never read them. Its caller also retains a pre-wait workspace
index, so simply removing that comparison would lose a revision increment.

Use an explicit private catalog-publication dependency choice. Only catalog-only APIs
without an expected workspace revision, and the separately catalog-revision-bound
policy writer, may witness catalog bytes plus pending recovery evidence. Continue
checking pending atomic publications for every shared path. Catalog bytes must remain
exact, the prepared callback executes once, and final policy validation still runs
after reacquiring the workspace guard. Any intervening catalog edit still refuses.
Legacy expected-workspace-revision APIs and full-document operations retain the broad
witness. No retry, rebase, new lock domain or dependency is introduced.

After successful catalog admission, read the current workspace index under the held
workspace guard and increment it. Do not replace it with the pre-wait revision. Keep
the complete ordered selected Agent keys through publication. This does not weaken
source revocation or grant a capability from the decision snapshot.

Prove with real file locks that an independent run checkpoint during catalog key wait
allows the catalog-only update, preserves both outcomes and both workspace increments,
and invokes the update once. The same interleaving must still reject an API carrying
the old expected workspace revision. A concurrent catalog edit and pending publication
must still refuse without overwrite. Run the existing selected-authority, catalog
validation and recovery regressions, then repeat the exact SQL cost criterion. A
passing cost sample is not a p95, maximum-size or fairness qualification.
