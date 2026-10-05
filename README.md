# Rezerv Booking Engine

A .NET 8 / MySQL booking API for studio classes, purchased credit packages, cancellation refunds, and FIFO waitlist promotion. Redis is used both for timetable caching and for a distributed per-schedule booking lock.

## Quick start (recommended)

Prerequisite: Docker Desktop with Docker Compose.

```bash
docker compose up --build
```

Then open:

- Swagger UI: <http://localhost:5080/swagger>
- Health check: <http://localhost:5080/health>

The API applies the included EF migration and inserts sample data on its first start. MySQL and Redis data are kept in named Docker volumes.

To stop the stack:

```bash
docker compose down
```

To also reset all database/cache data (destructive):

```bash
docker compose down --volumes
```

## Run locally

Prerequisites:

- .NET SDK 8.0 or newer
- MySQL 8.x on `localhost:3306`
- Redis on `localhost:6379`

The development connection settings are in `src/Rezerv.BookingEngine.Api/appsettings.json`. Start only the dependencies with:

```bash
docker compose up -d mysql redis
dotnet restore
dotnet run --project src/Rezerv.BookingEngine.Api
```

Swagger opens at <http://localhost:5080/swagger>. Configuration can be overridden with standard .NET environment variables, for example `ConnectionStrings__MySql` and `Redis__Host`.

Run verification:

```bash
dotnet build --configuration Release
dotnet test --configuration Release
```

Create a future schema change with:

```bash
dotnet ef migrations add MigrationName \
  --project src/Rezerv.BookingEngine.Infrastructure \
  --startup-project src/Rezerv.BookingEngine.Api \
  --output-dir Persistence/Migrations
```

## Project structure

```text
Rezerv.BookingEngine.sln
├── src
│   ├── Rezerv.BookingEngine.Domain
│   │   └── Entities.cs                     # Entities and lifecycle enums
│   ├── Rezerv.BookingEngine.Application
│   │   ├── Contracts.cs                    # API-neutral commands, responses, interface
│   │   ├── BookingPolicy.cs                # Pure, unit-tested business rules
│   │   └── BookingEngineException.cs       # Typed expected failures
│   ├── Rezerv.BookingEngine.Infrastructure
│   │   ├── Persistence                     # EF context, migration, runtime seed
│   │   ├── Redis                           # Cache client and distributed lock
│   │   └── Services                        # Transactional booking engine/background job
│   └── Rezerv.BookingEngine.Api
│       ├── Controllers                     # Required HTTP endpoints
│       ├── ExceptionHandlingMiddleware.cs  # RFC 7807-style errors
│       └── Program.cs                      # Composition root, Swagger, health
├── tests
│   └── Rezerv.BookingEngine.UnitTests
├── docs/database-schema.md                 # ERD and index documentation
├── requests.http                           # Ready-to-run request examples
├── Dockerfile
└── docker-compose.yml
```

Dependencies point inward: Domain has no dependencies; Application depends only on Domain; Infrastructure implements Application interfaces; API is the composition root. The transaction-heavy orchestration lives in Infrastructure because it must coordinate EF, MySQL, and Redis, while reusable policy decisions remain pure in Application.

## API

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/packages?businessId=1` | List active package definitions |
| `POST` | `/api/packages/purchase` | Mock-purchase a package for a customer |
| `GET` | `/api/timetable?businessId=1&date=2026-06-10` | List schedules with attendance and remaining slots |
| `POST` | `/api/bookings` | Book, or optionally waitlist when full |
| `POST` | `/api/bookings/cancel` | Cancel, conditionally refund, and promote FIFO |
| `POST` | `/api/waitlist` | Explicitly join a full schedule's waitlist |
| `GET` | `/health` | Check both MySQL and Redis |

Expected business failures return an HTTP `ProblemDetails` body with stable codes such as `schedule_full`, `invalid_package`, `overlapping_booking`, and `already_waitlisted`.

The [requests.http](requests.http) file contains complete sample bodies. Swagger documents and executes every endpoint.

## Seeded data

Seed data uses relative future dates, so it remains testable whenever the project is reviewed.

- 10 customers (`customer1@example.com` through `customer10@example.com`)
- 2 businesses: Rezerv Fitness (`1`) and Balance Yoga Studio (`2`)
- 3 purchasable package definitions
- 10 schedules across the two businesses
- Schedule `3` is full with bookings `1` and `2`
- Schedule `3` has FIFO waitlist entries for customers `4` and `5`
- Customer package `9` has zero credits
- Customer package `10` is expired
- Customer package `1` can demonstrate a wrong-business attempt against a business `2` schedule

IDs are intentionally deterministic for easy Swagger testing. Cancel seeded booking `1` as customer `2` to promote customer `4`; the promotion deducts customer `4`'s credit at that moment.

## Business behavior and assumptions

- All persisted instants are UTC. The `date` timetable filter is interpreted as a UTC calendar date.
- A package is valid until, but not including, `ExpiresAtUtc`.
- Classes cannot be booked or waitlisted once their start time has arrived.
- A booking cannot be cancelled after its class has started.
- Time ranges are half-open (`start <= time < end`), so back-to-back classes do not overlap.
- Cancellation must be **more than** four hours before start to refund. Exactly four hours is a late cancellation with no refund.
- A waitlist entry stores a preferred purchased package but neither reserves nor deducts credit. Eligibility is checked on join and checked again on promotion.
- FIFO is ordered by `JoinedAtUtc`, then database ID as a deterministic tie-breaker.
- If the first waiting customer is no longer eligible (expired/no-credit package or a new overlapping booking), that entry is closed with a reason and the next FIFO entry is evaluated.
- A cancellation creates one vacancy, so it promotes at most one eligible customer.
- If the class ends first, a background service marks active waitlist entries expired. There is no credit to release because none was reserved.
- Authentication was not requested, so requests explicitly contain `customerId`. In production this ID must come from an authenticated claim and never be trusted from the body.
- Package purchase is a mock: it creates the entitlement immediately without payment or payment-webhook handling.

## Concurrency strategy

Booking and cancellation follow this sequence:

```text
request
  -> acquire Redis lock "rezerv:lock:schedule:{id}" (SET NX PX)
  -> begin MySQL SERIALIZABLE transaction
  -> re-read schedule, attendance, package and overlapping bookings
  -> update credit + booking/waitlist + version tokens atomically
  -> commit
  -> invalidate timetable cache version
  -> release lock with token-checked Lua script
```

The Redis lock prevents normal contenders across API instances from entering the critical section together. It has a bounded wait (5 seconds), a 15-second lease, a unique owner token, and compare-and-delete release so one request cannot release another request's lock.

The lock is not the sole correctness boundary. MySQL uses a serializable transaction, capacity is counted again inside that transaction, and schedule/package `Version` fields are EF concurrency tokens. A lock lease expiry therefore cannot silently overwrite a credit or capacity update; a conflicting request receives HTTP 409 and can retry. Booking writes fail closed if Redis is unavailable, while timetable reads degrade to MySQL.

Trade-offs:

- A per-schedule lock deliberately serializes writes for one class, which is simple and appropriate for typical studio capacity. A very high-demand event platform would partition inventory or use an atomic inventory counter/outbox design.
- The included RESP client is intentionally small and dependency-light for the assessment. It opens one TCP connection per operation. Production should use StackExchange.Redis with connection pooling, lease renewal, cluster-aware retries, metrics, and carefully evaluated Redlock/fencing-token semantics.
- Serializable transactions can reduce throughput under contention, but provide a clear database safety net. Deadlock/transient retry policy would be added in production.
- Timetable results are cached for two minutes and keyed by a Redis version. Mutations increment the version, giving cheap broad invalidation. Old values disappear by TTL; a production system may use event-based targeted invalidation.

## Waitlist consistency

Cancellation and promotion occur in the same MySQL transaction. This means there is no visible vacant-slot intermediate state and no credit can be deducted without its promoted booking. `promoted_from_waitlist_entry_id` is unique, adding an idempotency guard. The background expiry worker only updates waitlists; since waiting never deducts credit, class-end cleanup cannot accidentally double-refund.

For a production deployment, promotion notifications would be written to an outbox in the same transaction and delivered asynchronously. Email/SMS must not run inside the booking transaction.

## Database

The initial EF migration is committed and applied automatically at startup. The full Mermaid ERD, relationships, and important indexes are in [docs/database-schema.md](docs/database-schema.md).

The model separates a reusable `PackageDefinition` from a purchased `CustomerPackage`. Credits, validity, business scope, and expiry are snapshotted on purchase, preserving historical correctness if the catalog definition later changes.

## Testing

Unit tests cover:

- valid, expired, depleted, and wrong-business packages
- overlap boundary behavior, including back-to-back classes
- early, exact-four-hour, and late cancellation refund rules

The next production-hardening layer would be MySQL/Redis integration tests launched with Testcontainers, including 10+ concurrent booking requests against a low-capacity schedule and assertions over booking count and total deducted credits.

## Scaling and production hardening

- Run multiple stateless API instances behind a load balancer; all coordinate through shared Redis/MySQL.
- Move waitlist expiry to a durable scheduler such as Hangfire/Quartz with distributed execution ownership.
- Add an outbox for promotion notifications and payment/package-purchase events.
- Add OpenTelemetry traces, lock-wait duration, booking conflict rate, waitlist depth, promotion latency, and credit-ledger metrics.
- Replace the mutable credit balance alone with an append-only credit ledger plus a materialized balance for auditability.
- Add JWT/OIDC authorization, customer ownership checks, rate limiting, idempotency keys, secret management, and audit logs.
- Add transient MySQL retry around whole idempotent transactions, Redis cluster/sentinel support, and database read replicas for timetable queries.
- Keep migrations as a deployment step rather than allowing every application replica to migrate on startup.

## Requirement coverage

- .NET 8, EF Core, MySQL, Swagger: included
- Redis caching and concurrency use: included
- Clean architecture: four-layer solution
- Required package/timetable/booking/cancellation/waitlist APIs: included
- FIFO automatic promotion and class-end cleanup: included
- 10 users, 2 businesses, 10 schedules, full/mixed/expired/waitlist sample data: included
- Unit tests and background processing bonus: included
- ERD/schema documentation: included
