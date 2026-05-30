# SensorMon

A homelab learning project: a .NET 10 Worker Service polls LibreHardwareMonitor
on a Windows desktop, stores readings in Postgres, runs in k3s, with a k8s
CronJob for cleanup and a GitHub Actions CI/CD pipeline.

Built to get hands-on with **Kubernetes**, **CI/CD**, and (optionally)
**event-driven architecture** before an interview.

## Architecture

```
[Windows desktop]                 [Ubuntu server: k3s cluster]
LibreHardwareMonitor              ┌───────────────────────────────────┐
  /data.json  <───── poll ─────── │  worker (Deployment, .NET 10)     │
  (reserved LAN IP)               │       │ writes via EF Core        │
                                  │       ▼                           │
                                  │  postgres (Deployment + PVC + Svc)│
                                  │       ▲                           │
                                  │  cleanup (CronJob, nightly DELETE)│
                                  └───────────────────────────────────┘
            GitHub Actions: build → test → push GHCR → kubectl set image
```

## Build order (the week plan)

### Phase 1 — get it running locally (no k8s yet)
1. Install the .NET 10 SDK. On your Mint laptop:
   `sudo apt-get install -y dotnet-sdk-10.0` (or use the install script if the
   feed lags).
2. On your **Windows desktop**: open LibreHardwareMonitor →
   Options → Remote Web Server → Run. Confirm `http://<desktop-ip>:8085/data.json`
   loads in a browser. Make sure Windows Firewall allows it on your LAN.
3. Put the real desktop IP in `appsettings.json` (or compose env).
4. Create the EF migration, then run with compose:
   ```bash
   cd src/SensorMon.Worker
   dotnet tool install --global dotnet-ef        # one-time
   dotnet ef migrations add InitialCreate
   cd ../..
   docker compose up --build
   ```
   You should see "Stored N readings" logs and rows in Postgres.

### Phase 2 — k3s
5. Install k3s on the server: `curl -sfL https://get.k3s.io | sh -`
   Then `sudo k3s kubectl get nodes` should show it Ready.
   Copy `/etc/rancher/k3s/k3s.yaml` to `~/.kube/config` (edit `server:` to the
   server's IP) so plain `kubectl` works.
6. Edit `k8s/10-config.yaml` (desktop IP) and `k8s/30-worker.yaml` (your GHCR
   image path). Then:
   ```bash
   kubectl apply -f k8s/00-namespace.yaml
   kubectl apply -f k8s/10-config.yaml
   kubectl apply -f k8s/20-postgres.yaml
   kubectl apply -f k8s/30-worker.yaml
   kubectl apply -f k8s/40-cleanup-cronjob.yaml
   ```
7. Watch it: `kubectl -n sensormon get pods -w`, `kubectl -n sensormon logs deploy/worker -f`.
   Practice the k8s muscle memory:
   - scale: `kubectl -n sensormon scale deploy/worker --replicas=2` (then set back to 1)
   - self-heal: `kubectl -n sensormon delete pod -l app=worker` and watch it return
   - inspect: `kubectl -n sensormon describe pod <name>`
   - manually trigger the cleanup: `kubectl -n sensormon create job --from=cronjob/cleanup-old-readings manualrun`

### Phase 3 — CI/CD
8. Push to GitHub. Add repo secret `KUBECONFIG_B64`
   (`base64 -w0 ~/.kube/config`). The workflow builds, pushes to GHCR, and
   rolls out on every push to main.
   - **If your cluster isn't internet-reachable** (likely): the GitHub-hosted
     runner can't reach your LAN. Two options: (a) put the server on Tailscale
     and add a Tailscale step to the job, or (b) run a **self-hosted runner** on
     the server itself, which already has cluster access. The self-hosted runner
     is the simpler homelab answer and a good thing to have done.

### Phase 4 (stretch) — event-driven alerting
9. Add Redis or NATS to the cluster. Have the worker publish a `HighTempAlert`
   when a temperature reading exceeds the threshold (hook is marked in
   `PollingWorker.cs`). Add a SECOND tiny service that subscribes and sends a
   notification (ntfy/Discord webhook). Make the consumer idempotent/debounced.

## Interview talking points this earns you

- **Containers/k8s**: Deployment, Service, ConfigMap, Secret, PVC, CronJob,
  namespaces, rollouts, self-healing, why the poller is single-replica, why a
  Worker needs no HTTP liveness probe.
- **CI/CD**: build→test→push→deploy, image tagging by SHA, `rollout status` as
  a gate, the runner-can't-reach-LAN problem and how you solved it.
- **Cloud-native**: config via env, secrets out of the image, stateless worker +
  stateful DB split, 12-factor thinking.
- **EDD/microservices** (if done): producer/consumer decoupling via a broker,
  at-least-once delivery, idempotency, why publish is outside the DB transaction.

## Honest caveats to say out loud
- Plaintext secrets in git are for learning only; real setup uses sealed-secrets
  / External Secrets / SOPS.
- Single-replica Postgres with a local-path PVC is not HA; production would use a
  StatefulSet, an operator (CloudNativePG), or a managed DB.
```
