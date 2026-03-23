# Ubuntu a Docker základy

## Cíl
Server má bezpečně připravit Ubuntu host pro Docker-based provoz.

## Praktické zásady
- preferuj oficiální Docker repository,
- ověř, že běží Docker Engine i Compose plugin,
- počítej s tím, že členství v `docker` group je velmi silné oprávnění,
- kontroluj disk, memory a port conflicts,
- měj zvlášť shared `proxy` network a interní backend sítě.

## Pozor
Docker port publishing na Linuxu může obcházet některé firewall očekávání.  
Proto nespoléhej jen na firewall; validuj i reálně publikované porty a compose konfiguraci.
