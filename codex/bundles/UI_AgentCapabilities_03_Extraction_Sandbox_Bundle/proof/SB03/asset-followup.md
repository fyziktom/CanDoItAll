# Asset follow-up acceptance

Status: PASS for the asset correction; complete SB03 measurement closure remains separate.

The frozen direct browser test `Responsive_filter_layout_matches_full_app_at_large_desktop` failed in both modes before source changes. The attempted inline-generated responsive utility correction also failed in both modes because the unlayered BaseLib flex-col rule won the cascade. Neither failed run is labeled GREEN despite the historical directory name browser-responsive-green.

Move the existing production 25-rule/five-breakpoint compatibility block into one shared unlayered Tailwind input, remove its old Web app.css copy, and import it from production and Fast inputs. Do not modify BaseLib or add broad source roots. The final browser-responsive-cascade-green matrix passes all 29 scenarios in each mode with row/center/space-between computed layout, unchanged matched x/y geometry, no horizontal overflow, preserved real actions, query behavior and owned shutdown.

Fresh direct builds: both generated themes, UI, Web, sandbox Parity/Fast and owning Unit/Components projects all passed. Existing 60 Unit and 38 Component cases were discovered then executed successfully. The first wrapper invocation supplied incorrect runner arguments and executed no test; cascade-runner-setup-error.json records this setup error. Four production DLL hashes are byte-identical to the broad stable checkpoint; the asset-only correction does not justify rerunning unchanged executable tests beyond the owning UI checks.

Visual inspection after correction: Parity/Fast baseline screenshots show the same inline filter and result-count row. Fast long-content keeps header statistics readable and long card children inside their parent. Parity committed-warning keeps the reconciliation action legible and mutation controls disabled. The repeated production calibration screenshot preserves the same measured three-card fixture and control geometry. Earlier normal/details/recovery browser acceptance remains independently retained.

The first complete post-extraction full-app/Parity/Fast timing series is excluded as non-comparable. The full repeat uses unchanged timing code and edit definitions with newly frozen shared asset inputs. Its numbers are reported only after all configurations finish and restoration is verified.
