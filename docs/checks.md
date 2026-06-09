# Checks Reference

This document describes all checks available in Homelab Doctor, the findings each produces, and the evidence used.

---

## `check docker`

Inspects the local Docker environment by shelling out to the `docker` CLI.

**Commands used internally:**
- `docker info --format json` — daemon availability, server version, container counts
- `docker ps -a --format '{{json .}}'` — full container list with state, status, ports
- `docker inspect <ids>` — per-container details: restart count, healthcheck config, port bindings, health state

**No root required.** If permission is denied, Homelab Doctor explains that your user may need to be in the `docker` group.

---

### Findings

#### DOCKER_AVAILABLE _(Info)_
The Docker daemon responded to `docker info`. Confirms the socket is reachable and the daemon is running.

---

#### DOCKER_CONTAINER_COUNTS _(Info)_
Summary of running, stopped, and restarting container counts from `docker ps -a`.

---

#### DOCKER_IMAGE_COUNT _(Info)_
Number of images stored on the host, from `docker info`.

---

#### DOCKER_NO_CONTAINERS _(Info)_
No containers exist on this host. Not a problem, just informational.

---

#### DOCKER_RESTARTING_\<NAME\> _(Critical)_
A container is in the `restarting` state and has a non-trivial restart count.

**Why it matters:** Docker's restart policy will keep relaunching a crashed container, masking the real failure. A high restart count with no manual intervention usually means the application is failing at boot.

**Suggested commands:**
```bash
docker logs --tail=100 <name>
docker inspect <name>
```

---

#### DOCKER_UNHEALTHY_\<NAME\> _(Critical)_
A container's healthcheck is reporting `unhealthy`.

**Why it matters:** The container process is running, but the service is not responding to its own health probe. Downstream services or load balancers may still be routing traffic to it.

**Suggested commands:**
```bash
docker inspect --format='{{json .State.Health}}' <name>
docker logs --tail=100 <name>
```

---

#### DOCKER_NO_HEALTHCHECK _(Warning)_
One or more running containers have no healthcheck configured.

**Why it matters:** Without a healthcheck, Docker has no way to distinguish a running process from a running-but-broken service. The container will appear healthy even if the application has crashed internally.

**Fix:** Add a `HEALTHCHECK` instruction to the Dockerfile, or define `healthcheck:` in your compose file.

---

#### DOCKER_EXPOSED_PORTS _(Warning)_
One or more containers are binding ports to `0.0.0.0`, exposing them on all network interfaces.

**Why it matters:** Binding to `0.0.0.0` makes a port reachable on every interface, including any public ones. This is intentional for services like a reverse proxy, but can be unintended for internal databases, caches, or admin panels.

**Fix:** Bind to `127.0.0.1` instead of `0.0.0.0` if the service should only be reachable locally, or remove the port mapping and use an internal Docker network.

---

#### DOCKER_HIGH_RESTARTS_\<NAME\> _(Warning)_
A container that is currently stable has a high historical restart count (5+).

**Why it matters:** The container is not currently restarting, but its history suggests it has experienced repeated failures. Worth investigating whether the root cause has been fixed or just temporarily resolved.

---

### Unavailability cases

If Docker cannot be inspected, the check returns an `Unavailable` result instead of findings:

| Condition | Message |
|-----------|---------|
| `docker` not on PATH | Docker is not installed or not on PATH |
| Permission denied on socket | Your user may need to be added to the docker group |
| Daemon not running | Cannot connect to the Docker daemon. Is the Docker service running? |
| Unknown error | Stderr from the failed command |
