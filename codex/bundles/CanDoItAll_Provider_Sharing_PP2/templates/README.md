# Evidence templates

Copy these templates into the task's private owned run directory. Never edit the sealed originals.
`evidence.json` is the product group ledger, all NOT_RUN initially. Every result needs its own
commands, built/discovered inputs, source pair/image, actual outcome and limitations.

`model-parity.json` is an optional safe consistency export schema for successful synchronized
snapshots. Set `status` to OBSERVED only after collecting real evidence. Populate:

- `application_commit`, `image_digest`, `phase`: actual producing source/image and scenario.
- `source_publications`: rows with `source_instance_id`, `publication_id`, `revision`,
  `default_route_id`, `models`. Model rows contain `route_id`, `display_name`, `is_suggested`.
- `clients`: at least two distinct `client_id` entries; each has `expected_publications`
  (pairs of source instance/publication IDs) and `imports`.
- Each import has those same identifiers, its `local_provider_id`, `revision`, `default_route_id`,
  `default_display_name`, `models` and `labels`. Label rows have `surface`, `route_id`, `display_name`.

Labels are semantic model labels after accounting for a known 'Provider default (...)' wrapper;
also retain the actual UI assertions proving the wrapper. A selector need not display the full
catalog. Hidden option values remain routing IDs. Prices, Thinking, terminal statuses, network
legs and authorization require separate native/UI evidence; this small checker does not verify them.
Unavailable/conflicting snapshots use the main ledger and dedicated assertions, not an invented
successful parity snapshot. Never strip an unavailable model to make a success schema pass.
The checker does not attest that supplied source data, revision or image digest is authentic.
