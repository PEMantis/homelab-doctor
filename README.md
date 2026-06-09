# Homelab Doctor

**Local-first diagnostics for self-hosted infrastructure.**

Homelab Doctor is a CLI that inspects your homelab and tells you what is obviously wrong, right now. It runs locally, talks to nothing external, and gives you plain-language findings with context and suggested next steps — not just red/yellow/green status.

---

## What it is

- A CLI that runs checks against your local machine and self-hosted services
- Produces a human-readable report with explanations and suggested fixes
- Entirely local: no telemetry, no cloud, no database, no background daemon

## What it is not

- Not a monitoring dashboard (that's Grafana, Netdata, Beszel)
- Not an uptime tracker (that's Uptime Kuma)
- Not a metrics collector (that's Prometheus)
- Not an agent or a SaaS service
- Not a replacement for any of the above

The question it answers is: **"What is obviously wrong with this machine right now?"**

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker (for the Docker check)

### Build and run

```bash
git clone https://github.com/PEMantis/homelab-doctor
cd homelab-doctor
dotnet run --project src/HomelabDoctor.Cli -- check docker
```

### Or build a self-contained binary

```bash
dotnet publish src/HomelabDoctor.Cli -c Release -r linux-x64 --self-contained -o ./publish
./publish/homelab-doctor check docker
```

---

## Usage

```
homelab-doctor check              # Run all checks (currently: Docker)
homelab-doctor check docker       # Inspect Docker environment
homelab-doctor report             # Generate a Markdown report to stdout
homelab-doctor report --output report.md   # Write report to file
homelab-doctor report --format console     # Render in terminal instead
```

---

## Example Output

```
Homelab Doctor
Docker Diagnosis

✓  Docker daemon is reachable
✓  14 containers found
⚠  3 containers have no healthcheck
✗  1 container is restarting
⚠  2 containers expose ports on 0.0.0.0

Findings

[CRITICAL] Container aeris-api is restarting
  Evidence:        State: restarting, Restart count: 14
  Why it matters:  Docker can restart a failed process, but repeated restarts
                   usually mean the application is crashing at startup.
  Fix:             Check logs for the startup error.
  Try:
    $ docker logs --tail=100 aeris-api
    $ docker inspect aeris-api

[WARNING] 3 running containers have no healthcheck
  Evidence:        traefik, redis, postgres
  Why it matters:  Docker will show these as 'running' even if the service
                   inside has silently crashed.
  Fix:             Add a HEALTHCHECK to the Dockerfile or compose file.
```

---

## MVP Scope (v0.1)

The current release covers:

- [x] Docker daemon reachability
- [x] Running / stopped / restarting / unhealthy container counts
- [x] Containers missing healthchecks
- [x] Containers exposing ports on 0.0.0.0
- [x] Restart counts with warnings for high counts
- [x] Clear findings: severity, evidence, explanation, suggested commands
- [x] Spectre.Console terminal output
- [x] Markdown report generation (`homelab-doctor report`)

---

## Roadmap

### v0.1 — The Stethoscope _(current)_
- Docker environment inspection
- Markdown report output

### v0.2 — DNS Goblin Finder
- Expected DNS record checks
- Local vs public DNS comparison
- Hostname resolution validation

### v0.3 — Reverse Proxy Rounds
- Traefik router/service/middleware inspection
- TLS expiry warnings
- Dead backend detection

### v0.4 — Fleet-ish
- Optional SSH inventory
- Multi-host diagnostic reports

### v0.5 — Doctor Notes
- Richer explanations
- Copyable remediation commands
- Links to upstream documentation

---

## Privacy and Local-First Promise

Homelab Doctor collects nothing. It:

- Does not send data anywhere
- Does not write to any remote service
- Does not collect environment variable values
- Does not include telemetry of any kind
- Has no authentication surface
- Has no background daemon or scheduled tasks

Everything runs locally. The only network activity is the tool reading from your local Docker socket.

---

## Contributing

Contributions are welcome. This project is early-stage, so the best way to help is:

1. Open an issue describing a new check you'd like to see
2. Open an issue for a finding you think is noisy, missing, or misleading
3. Submit a PR with a new check module following the existing `ICheck` / `IDockerInspector` pattern

Please keep checks:
- **Local-first**: no cloud calls
- **Non-destructive**: read-only inspection only
- **Clearly explained**: every finding should have a `Why it matters` and ideally a `SuggestedFix`

---

## License

MIT — see [LICENSE](LICENSE)
