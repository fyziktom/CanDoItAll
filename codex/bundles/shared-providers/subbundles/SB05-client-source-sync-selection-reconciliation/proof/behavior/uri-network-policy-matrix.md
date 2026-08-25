# SB05 source URI and network policy behavior

State: `PASS`.

| Input or destination | Public policy | Approved trusted/private policy |
| --- | --- | --- |
| absolute public HTTPS | allowed after canonicalization and connection-time DNS validation | allowed when policy permits |
| loopback HTTP | allowed only by the explicit loopback development rule | allowed |
| private HTTP/HTTPS | rejected | requires explicit private-network approval |
| public plain HTTP | rejected | rejected |
| userinfo, query, fragment, relative or non-HTTP(S) URI | rejected | rejected |
| link-local, multicast, unspecified, documentation, benchmarking, protocol-assignment, 6to4 and other special-use destinations | rejected | rejected where never safe; private ranges only under the explicit trusted policy |
| mixed public/private DNS answers | rejected | validated against the selected policy |
| DNS answer changed on a later connection | revalidated; no cached trust | revalidated; no cached trust |
| redirect | disabled | disabled |

Base-path canonicalization preserves reverse-proxy roots. Named clients disable automatic redirects,
proxy use, and cookies, set bounded connect/pool settings, install a connection callback for DNS
revalidation, and retain the platform certificate validator. Current special-use classifications were
checked against the IANA IPv4 and IPv6 special-purpose registries.

The exact unit selection records 18 discovered and 18 passed.
