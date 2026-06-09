# Roadmap

Homelab Doctor is intentionally scope-limited. It is a diagnostic tool, not a monitoring platform. The roadmap adds new _check domains_ rather than new architectural complexity.

---

## v0.1 — The Stethoscope _(current)_

**Goal:** Prove the model. One useful check, clean output, clear findings.

- [x] `homelab-doctor check docker` — Docker environment inspection
- [x] Finding model: Id, Title, Severity, Evidence, Explanation, SuggestedFix, SuggestedCommands
- [x] CheckResult model with status, summary, timing
- [x] Spectre.Console terminal renderer
- [x] Markdown report generator
- [x] `homelab-doctor report --format markdown`
- [x] Unit tests for check logic behind `IDockerInspector`

---

## v0.2 — DNS Goblin Finder

**Goal:** Catch the most common "why isn't my domain working" problems.

- [ ] Resolve hostnames against local DNS
- [ ] Compare local vs public DNS responses
- [ ] Expected record checks from optional config file
- [ ] Detect split-horizon mismatches
- [ ] `homelab-doctor check dns`

---

## v0.3 — Reverse Proxy Rounds

**Goal:** Inspect Traefik and surface common misconfiguration.

- [ ] Traefik API inspection (local socket or HTTP API)
- [ ] Router/service/middleware inventory
- [ ] Dead backend detection
- [ ] TLS certificate expiry warnings
- [ ] `homelab-doctor check traefik`

---

## v0.4 — Fleet-ish

**Goal:** Expand beyond the local machine without building a full orchestration layer.

- [ ] Optional SSH inventory file (YAML)
- [ ] Run checks against remote hosts via SSH
- [ ] Aggregate multi-host report
- [ ] `homelab-doctor check --host <alias>`

---

## v0.5 — Doctor Notes

**Goal:** Make the findings more actionable and easier to act on.

- [ ] Richer explanations with links to relevant documentation
- [ ] Copyable remediation commands in report output
- [ ] JSON output format for piping into other tools
- [ ] `homelab-doctor check --output-format json`

---

## Explicitly out of scope

- Web UI
- Prometheus metrics endpoint
- Background daemon / scheduled scanning
- Remote SaaS reporting
- AI-generated summaries
- Authentication
- Database persistence
- Plugin system

If you need a continuous monitoring solution, look at [Uptime Kuma](https://github.com/louislam/uptime-kuma), [Netdata](https://github.com/netdata/netdata), [Beszel](https://github.com/henrygd/beszel), or [Grafana](https://grafana.com).
