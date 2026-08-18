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

## Compose E2E and failure injection

`e2e-compose.ps1/.sh` validates the Compose file, builds the current images, waits for the stack, installs Chromium, and runs the Playwright suite from `web/microshop-ui`. It intentionally leaves containers and named volumes running for inspection; it never runs `docker compose down` or deletes data.

PowerShell:

```powershell
./scripts/e2e-compose.ps1 -EnvFile .env.example
```

POSIX shell:

```bash
./scripts/e2e-compose.sh .env.example
```

`failure-injection.ps1` stops and starts only named services and verifies Product dependency failure, Notification recovery, RabbitMQ/outbox recovery, duplicate-message/error-queue behavior, and concurrent last-stock protection. It preserves containers and volumes. Use a single scenario while diagnosing:

```powershell
./scripts/failure-injection.ps1 -Scenario all -EnvFile .env.example
```

The `.sh` entry point delegates to the same PowerShell harness when `pwsh` is installed. Failure scenarios create uniquely named demo Products and Orders so no cleanup or destructive reset is required.

## Security scanning

`security-scan.ps1/.sh` runs the NuGet transitive-package audit, the production-only npm audit, Compose validation/build, and Docker Scout HIGH/CRITICAL scans for the five application images built by this repository. The scan uses local image references and never exports credentials. Docker Scout must be installed locally; CI runs the equivalent application-image scan with Trivy. PostgreSQL and RabbitMQ are upstream infrastructure images and remain subject to their own image-maintenance policy; they are not reported as repository-built images by this gate.

PowerShell:

```powershell
./scripts/security-scan.ps1 -EnvFile .env.example
```

POSIX shell:

```bash
./scripts/security-scan.sh .env.example
```

## Backup and restore drill

Backups are PostgreSQL custom-format archives. The wrappers start only the PostgreSQL Compose service when necessary, read the resolved database name from Compose, write under ignored `TestResults/`, and do not remove named volumes or service databases:

```powershell
./scripts/db-backup.ps1 -Database product -EnvFile .env
./scripts/db-backup.ps1 -Database order -EnvFile .env
./scripts/db-backup.ps1 -Database notification -EnvFile .env
```

```bash
./scripts/db-backup.sh product .env
./scripts/db-backup.sh order .env
./scripts/db-backup.sh notification .env
```

`db-restore-drill.ps1` creates a disposable PostgreSQL container with an isolated `tmpfs` data directory and `--network none`, restores one live demo database archive into it, verifies a service-owned table, and removes only that temporary container and archive. It never drops a Compose database or Docker volume:

```powershell
./scripts/db-restore-drill.ps1 -Database product -EnvFile .env.example
./scripts/db-restore-drill.ps1 -Database order -EnvFile .env.example
./scripts/db-restore-drill.ps1 -Database notification -EnvFile .env.example
```

The POSIX entry point requires PowerShell 7 (`pwsh`) for binary-safe dump handling. Run the drill before changing migrations or image versions, and retain the archive outside the repository when it contains meaningful demo data.
