# deploy/

Post-MVP packaging. Empty in wave 1.

The contents that will land here:

- `systemd/sonrisa.service` — unit file for the AppHost process on Linux.
- `windows-service/install.ps1` — installer for the Windows Service.
- `nginx/sonrisa.conf` — reverse-proxy config (TLS, gzip, static-file cache).

For the MVP, run `scripts/dev.sh` (Linux/macOS) or `scripts/dev.ps1` (Windows). Production deployment is a post-MVP concern.

See [`docs/roadmap/2-stack.md` §9](../../docs/roadmap/2-stack.md) for the deployment shape.
