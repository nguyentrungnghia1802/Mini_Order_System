# Repository scripts

This directory is reserved for explicit migration, seed, and local operations wrappers described in the project documentation.

Product, Order, and Notification migration wrappers live beside their owning service migrations. `db-migrate-all.*` runs the three Compose migration one-shots in order and requires an existing local env file; it does not delete data.

`db-outbox-status.*` is a read-only operator query. It reports the Order outbox backlog/dead-letter summary and the first 20 pending or dead-lettered rows without exposing database credentials or deleting data.

PowerShell:

```powershell
./scripts/db-migrate-all.ps1 -EnvFile .env
./scripts/db-outbox-status.ps1 -EnvFile .env
```

POSIX shell:

```bash
./scripts/db-migrate-all.sh .env
./scripts/db-outbox-status.sh .env
```

`db-reset-local.*` is intentionally destructive. It refuses to run unless the caller supplies the explicit reset/volume-deletion flags, permits a non-Development environment when applicable, and types `DELETE MICROSHOP LOCAL VOLUMES`. Do not run it without owner approval; normal `docker compose down` preserves named volumes.
