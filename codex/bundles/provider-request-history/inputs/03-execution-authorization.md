# Implementation authorization — 2026-08-28

User authorized implementing the prepared bundle and proper testing. The earlier preparation-only limit is superseded for execution; its historical evidence remains valid as preparation evidence only.

Required acceptance targets are the standard application at http://localhost:5032 and the existing Docker shared publisher at http://localhost:5210 with client at http://localhost:5212. Isolated deterministic upstream tests supplement these targets; they do not replace them. Preserve existing data, provider configuration, credentials and Docker volumes. Do not record secrets in artifacts. Any paid/provider call needs an identified, bounded test path; no broad paid-provider suite.

Entry source: ce9ea0612020010e12d0af058ba8ce02d158364c. Source and tests unchanged since preparation. Managed 5032 baseline reached healthy state. Docker containers candoitall-spui-shared and candoitall-spui-client were already running and have not been recreated. Browser automation startup currently fails before navigation; no UI pass is claimed.
