# Prompt: SSH transport

Implementuj transportní vrstvu pro vzdálený host.

Požadavky:
- rozhraní `ISshTransport`,
- MVP implementace `SshNetTransport`,
- načítání private key z env proměnných,
- podpora passphrase z env,
- host key verification přes fingerprint nebo known_hosts-like entry,
- timeouty pro connect/exec/upload/download,
- bezpečná mapa výjimek na doménové chyby.

Neimplementuj:
- interaktivní shell,
- SSH agent forwarding,
- password auth.

Dále:
- přidej unit testy pro host key verification a config validation,
- připrav fake transport pro integration testy.
