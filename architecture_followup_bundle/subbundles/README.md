# Subbundle index

Numbered subbundles are the main execution queue.

Corrective subbundles are playbooks that must be activated whenever a gate fails or a stop rule is triggered. Downstream work must not continue until the corrective subbundle is complete and the failed gate is rerun.
