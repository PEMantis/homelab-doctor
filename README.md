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
dotnet run --project src/HomelabDoctor.Cli -- check
```

### Or build a self-contained binary

```bash
dotnet publish src/HomelabDoctor.Cli -c Release -r linux-x64 --self-contained -o ./publish
./publish/homelab-doctor check
```

---

## Usage

```
homelab-doctor check              # Run all checks (Docker + DNS)
homelab-doctor check docker       # Inspect Docker environment only
homelab-doctor check dns          # Inspect DNS configuration only
homelab-doctor report             # Generate a Markdown report to stdout
homelab-doctor report --output report.md   # Write report to file
homelab-doctor report --format console     # Render in terminal instead
```

### Optional config file

Place a `homelab-doctor.yml` in your working directory to add expected DNS record validation:

```yaml
dns:
  records:
    - hostname: grafana.home.arpa
      type: A
      expected: 192.168.1.50
    - hostname: traefik.home.arpa
      type: A
      expected: 192.168.1.50
```

---

## Example Output

```
Homelab Doctor

✓ Docker Check
  Docker looks healthy. 14 containers found, no issues detected.

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

✓ DNS Check
  DNS looks healthy. Local resolution is working.

  ✓  DNS server(s) configured: 192.168.1.1
  ✓  github.com resolves via local DNS
  ✓  cloudflare.com resolves via local DNS
```

---

## What's in v0.2

- [x] Docker daemon reachability
- [x] Running / stopped / restarting / unhealthy container counts
- [x] Containers missing healthchecks
- [x] Containers exposing ports on 0.0.0.0
- [x] Restart counts with warnings for high counts
- [x] DNS nameserver discovery from `/etc/resolv.conf`
- [x] Local resolution checks for public hostnames
- [x] Public DNS comparison via `dig` — sinkhole and split-horizon detection
- [x] Config-driven expected DNS record validation (`homelab-doctor.yml`)
- [x] Spectre.Console terminal output
- [x] Markdown report generation (`homelab-doctor report`)

---

## Roadmap

### v0.1 — The Stethoscope ✓
- Docker environment inspection
- Markdown report output

### v0.2 — DNS Goblin Finder ✓
- DNS nameserver discovery
- Local resolution validation
- Public DNS comparison (sinkhole / split-horizon detection)
- Config-driven expected record checks

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

Everything runs locally. DNS checks resolve hostnames using your machine's configured resolver and optionally shell to `dig` — no data leaves your network.

---

## Contributing

Contributions are welcome. This project is early-stage, so the best way to help is:

1. Open an issue describing a new check you'd like to see
2. Open an issue for a finding you think is noisy, missing, or misleading
3. Submit a PR with a new check module following the existing `ICheck` / `IDnsInspector` / `IDockerInspector` pattern

Please keep checks:
- **Local-first**: no cloud calls
- **Non-destructive**: read-only inspection only
- **Clearly explained**: every finding should have a `Why it matters` and ideally a `SuggestedFix`

---

## License

MIT — see [LICENSE](LICENSE)
