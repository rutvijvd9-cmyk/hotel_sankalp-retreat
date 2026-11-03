# Hotel Sankalp WhatsApp — platform issues and 15-day cleanup

Non-security review of the current worker (Windows, `localhost:8001`, SQLite).  
Security (auth, secrets, injection, XSS) is out of scope.

**Checked against code:** 3 Nov 2026.

## Implementation status (summary)

| Area | Status |
|---|---|
| 15-day prune of logs, outbox, backups, EventLog, jobs, campaign progress, replies, inbox media | **Done** |
| Temp printer PDFs (`bill_temp_*` etc.) | **Done** (deleted after **2 days**, not 15) |
| Manual `/api/system-logs/clear` default 15 days | **Done** |
| SQLite vacuum until free pages are low | **Done** |
| Hot-path indexes | **Mostly done** (`ConversationReplies` index is migration-only, not in `Schema.sql`; `GetRipeJobs` still has no `LIMIT`) |
| Guest bills in `PDFs\Processed` (non-temp) | **Not done** (intentional unless copied off-box) |
| Off-box backup copy | **Not done** |
| WAL-safe backup only (no `File.Copy` fallback) | **Not done** |
| Outbox parallelism, leases, backoff, Meta rate limit | **Not done** |
| Split API vs worker / autoscaling / drop mutex | **Not done** |
| SQLite cache / checkpoint / connection pool | **Not done** |
| Buffered logs / DB only Warning+ / batched inserts | **Not done** |
| `/health`, metrics, disk-free alerts | **Not done** |
| HttpClient timeout, PMS event queue, real SMS | **Not done** |
| JobProcessor 1s poll, date trigger at 00:05 IST, campaign batch cap | **Not done** |

**Bottom line:** the 15-day cleanup work from sections 2–3 is implemented. Scale, autoscaling, logging cost, health, and backup-off-box from sections 5–8 are **not**.

---

## 1. How the platform is built today

| Piece | Reality |
|---|---|
| Process | One Windows process: HTTP API + tray icon + all background workers |
| Bind | `http://localhost:8001` only |
| Single instance | Global mutex `HotelAutomation_TheSankalpRetreat_Worker` — second copy exits |
| Database | One SQLite file: `%AppData%\HotelSankalpRetreat\hotel_automation.db` |
| Logs (disk) | `%AppData%\HotelSankalpRetreat\logs\` |
| Backups | `%AppData%\HotelSankalpRetreat\backup_date\` |
| Uploads | `%AppData%\HotelSankalpRetreat\uploads\` |
| PDFs | `{AppDir}\PDFs` watch folder, `{AppDir}\PDFs\Processed` archive |
| Outbox | Poll every 5s, batch 10, send one-by-one, max 5 retries |
| PMS | PDF virtual-printer bridge (default); optional DB poll every 30s |

There is **no autoscaling**, load balancer, replica, or shared queue.

---

## 2. 15-day cleanup — already running

`DatabaseMaintenanceScheduler` runs ~30s after start, then every 24 hours.

| Store | Action today | Safe at 15 days? |
|---|---|---|
| `SystemLogs` | DELETE older than 15 days | Yes |
| `ActivityLog` | DELETE older than 15 days | Yes |
| `OutboxMessages` status `Sent` / `Delivered` / `Read` / `Failed` | DELETE older than 15 days | Yes (keep `Pending`) |
| `ShortenedLinks` for those outbox rows | DELETE with outbox | Yes |
| `backup_date\*.db` | DELETE files older than 15 days | Yes (keep a copy off this PC) |
| `logs\*.log` and crash files | DELETE by last-write older than 15 days | Yes |

---

## 3. Safe to delete every 15 days (add these)

Operational leftovers. Not master data.

| Store | Why it is safe | Status |
|---|---|---|
| `EventLog` | PMS/PDF event JSON after the message is already in Outbox | **Implemented** (`ClearOldLogsAsync`, 15 days) |
| `ScheduledJobs` with status `Processed` or `Cancelled` | Already executed or cancelled | **Implemented** (`ClearOldJobsAsync`, 15 days; `Pending` kept) |
| `ContactCampaignProgress` with status `Sent` | Campaign *definitions* stay; only per-contact sent rows go | **Implemented** (`ClearOldProgressAsync`, 15 days) |
| `ConversationReplies` | Inbox chat text | **Implemented** (DELETE `SentAt` older than 15 days) |
| `uploads\inbox\` media | Inbound WhatsApp images/files | **Implemented** (AppData + `wwwroot\uploads\inbox`) |
| Temp PDFs (`bill_temp_*`, `recipient_temp_*`, `supplier_temp_*`) | Leftovers from the printer bridge | **Implemented** after **2 days** (not 15) |

Manual API: `POST /api/system-logs/clear` now defaults to **15 days**.

Vacuum: maintenance loops `PRAGMA incremental_vacuum(1000)` until the freelist is empty (cap 50 rounds), then `PRAGMA optimize`.

---

## 4. Do not delete every 15 days

| Store | Why keep it |
|---|---|
| `hotel_automation.db` (live database) | The product |
| `Contacts` | Guest list |
| `ContactConsent` | Opt-in / opt-out |
| `UnsubscribedContacts` | Must never re-message |
| `MessageTemplates` / `TemplateVariants` | WhatsApp templates |
| `AutomationRules` | Checkout / event rules |
| `Campaigns` / `CampaignSteps` | Campaign definitions |
| `Users` / `RolePermissions` | Staff accounts |
| `AppConfig` | Settings |
| `PmsMappings` | PMS field mapping |
| `SmartLists` / `ContactCategories` | Lists and categories |
| `OutboxMessages` status `Pending` | Still waiting to send |
| `ScheduledJobs` status `Pending` | Still waiting to fire |
| Startup file `hotel_automation.db.bak` | Keep the latest (it is overwritten each start, not a 15-day set) |

**Bills in `PDFs\Processed`:** OK to drop *local* copies at 15 days for disk, but GST/accounts often need invoices much longer. Copy off-box (NAS/drive), then delete local files. Do not treat guest bills like logs.

**Backups:** 15-day rotation on the hotel PC is fine. Keep at least one copy on **another disk/machine**, or a disk failure takes live DB and backups together.

---

## 5. All issues (non-security)

### P0 — scale / throughput

**P0-1 — Single-process, single-instance — no autoscaling path**  
Evidence: global mutex; Kestrel on `localhost:8001`; SQLite; API + 8 hosted services in one process.  
Why: a second instance is killed. Peak checkout, UI, PDF ingest, and WhatsApp all share one process and one SQLite writer. Kubernetes HPA cannot work.  
Fix: one hotel — keep single instance, split API vs worker, raise outbox throughput. Multi-hotel — Postgres/SQL Server + queue, drop mutex, bind `0.0.0.0` behind a proxy, then add replicas.

**P0-2 — SQLite is the hard ceiling**  
Evidence: one `.db` in AppData; `cache_size=-2000` (2 MB); `wal_autocheckpoint=100`; new connection on every `OpenAsync`.  
Why: one writer. Checkpoints and a tiny cache stall under log + outbox load. Horizontal scale is impossible until the store changes.  
Fix: raise cache, checkpoint less often, pool connections. Plan Postgres if more than one hotel or many concurrent operators.

**P0-3 — Outbox send path is sequential and slow**  
Evidence: interval 5s, batch 10, `await` per message; no Meta rate limiter; failed rows go back to `Pending` immediately.  
Why: ~2 messages/sec cap. A 1,000-contact campaign takes 8+ minutes plus PDF uploads. Immediate retries worsen 429 storms.  
Fix: row leases, limited parallelism (4–8), `Retry-After`, exponential backoff, token-bucket at Meta limits.

### P1 — retention / reliability / cost

**P1-1 — EventLog is never deleted**  
Every PMS/PDF event stores full JSON forever. Largest gap vs the 15-day policy.  
Fix: `DELETE FROM EventLog WHERE OccurredAt < cutoff` in the daily cycle.

**P1-2 — Conversation replies, media, PDF archives never expire**  
`ConversationReplies`, `/uploads/inbox`, `PDFs\Processed` have no TTL. Disk on the hotel PC fills.  
Fix: age-out replies and inbox media at 15 days; archive bills off-box then delete local files.

**P1-3 — Processed jobs and campaign progress never shrink**  
`ScheduledJobs` stay `Processed`/`Cancelled`. `ContactCampaignProgress` stays `Sent`. `GetRipeJobsAsync` has no `LIMIT` and no index. Ripe-job query runs every 1 second.  
Fix: prune after 15 days; add `IX_ScheduledJobs_Status_TriggerTime` and a `LIMIT`.

**P1-4 — 15-day job exists but is incomplete**  
Covers logs, activity, sent/failed outbox, `backup_date`, disk logs only. Vacuum is only 500 pages. Manual clear API is 30 days. Startup `.bak` is a single overwrite. Backups sit on the same disk as the live DB.  
Fix: unify TTL=15; vacuum until free pages are low; copy backups off-box; align the clear API.

**P1-5 — Every log line hits locked disk and SQLite**  
`File.AppendAllText` under a global lock; Information+ queued to `SystemLogs` one INSERT at a time; channel 10,000 DropOldest. Logging competes with outbox; under load logs are dropped.  
Fix: buffered rolling files; DB only Warning+ or sampled Info; batched inserts; keep 15-day prune.

**P1-6 — No health checks, no circuit breaker, stub SMS**  
No `/health`. WhatsApp `HttpClient` has no timeout/retry. `OnPmsEventReceived` is `async void`. `SmsMessageSender` pretends success.  
Fix: `/health` (DB ping + last outbox tick); HttpClient timeout + retries; queue PMS events; implement or disable SMS.

**P1-7 — Background loops are too chatty or too coarse**  
`JobProcessor` delay 1s forever. `CampaignProcessor` 1 min and holds a write transaction for every due step. `DateTriggerProcessor` sleeps 24h from process start (misses midnight IST; loads ALL contacts). `DatabasePmsConnector` polls 30s and is SQLite-only despite SQL Server comments.  
Fix: wait until next due job; cap campaign batches; run date triggers at 00:05 IST with paged contacts; use a real driver if PMS is not SQLite.

### P2 — schema / DR / observability

**P2-1 — Missing indexes on hot paths**  
Only `SystemLogs` is indexed. None on `OutboxMessages(Status, CreatedAt)`, `EventLog(OccurredAt)`, `ScheduledJobs(Status, TriggerTime)`, `ConversationReplies(SentAt)`, `ActivityLog(OccurredAt)`.  
Fix: add those indexes in `Schema.sql` + migration.

**P2-2 — Backups are local-only; WAL copy can be inconsistent**  
`VACUUM INTO` fallback is `File.Copy` of the live `.db` while WAL is active. No restore drill.  
Fix: only `VACUUM INTO` or backup API after `wal_checkpoint(TRUNCATE)`; copy to a second path; test restore monthly.

**P2-3 — No metrics or capacity signals**  
ILogger + email on ERROR only. No queue depth, send latency, SQLite busy rate, disk-free. `Program.cs` is a ~3k-line monolith.  
Fix: gauges for pending outbox, last send, db file size, free disk; alert before cleanup or disk-full.

---

## 6. Extra reliability / scale details (same review)

- API + workers share one process — UI requests block the same SQLite as WhatsApp sends.
- Max request body 250 MB (PDF uploads can stall the box).
- Outbox fallback to SMS still marks the WhatsApp row `Failed` and SMS is a stub.
- Pending outbox is never expired (correct — do not auto-delete `Pending`).
- `DateTriggerProcessor` comment says “check every 1 hour” but the code delays **24 hours**.
- Maintenance cycle is “24 hours after last run”, not a clock time (02:00 IST). If the PC is off, cleanup waits until next start (+30s) — that part is OK.
- `auto_vacuum = INCREMENTAL` only applies if set before tables existed; old DBs may not reclaim space.
- Logging writes **Information** (every WhatsApp send) into SQLite — high write amplification.
- No connection pooling; `busy_timeout` 10s; 5 retries on SQLITE_BUSY.
- Pdf archive `ArchiveProcessed = true` grows forever by date folder.
- Inbox media path in `Program.cs` can use `wwwroot\uploads\inbox` vs AppData uploads — both need TTL.
- Database PMS connector comment claims SQL Server/MySQL/PostgreSQL; implementation opens `Microsoft.Data.Sqlite` only.

---

## 7. Capacity model

**Current**

- 1 Windows process
- 1 SQLite writer
- Outbox ~2 WhatsApp sends/sec theoretical
- Global mutex — replica #2 exits
- Bind localhost only

**What real autoscaling would need**

- Shared DB with many writers (not SQLite)
- Outbox as a queue with row leases
- Stateless API behind a reverse proxy
- Worker replicas sharing a Meta rate-limit budget
- Drop global mutex / “kill other process on port 8001”
- Health + queue-depth metrics to scale on

For **one hotel PC**, Kubernetes autoscaling is the wrong first fix. Finish 15-day TTL, indexes, and outbox throughput first.

---

## 8. Suggested order of work

1. Extend 15-day prune to EventLog, replies, processed jobs, sent campaign progress, inbox media, temp PDFs (bills: off-box then local delete).
2. Add indexes; vacuum until free pages are gone; batch SystemLogs writes.
3. Outbox leases, parallelism, backoff, Meta rate limit.
4. `/health`, queue depth, disk-free, last-send metrics.
5. Off-box backups; WAL-safe `VACUUM INTO` only.
6. Only if multi-hotel: leave SQLite, drop mutex, then scale workers.

---

## 9. File map (where this lives)

| Topic | Files |
|---|---|
| 15-day job | `HotelAutomation.Worker/Services/DatabaseMaintenanceScheduler.cs` |
| Backups | `HotelAutomation.Worker/Services/DatabaseBackupService.cs`, `DatabaseBackupScheduler.cs` |
| Startup `.bak` | `HotelAutomation.Worker/Program.cs` (~startup DB copy) |
| Logging | `HotelAutomation.Worker/Logging/UnifiedLogger.cs`, `CrashLogger.cs` |
| EventLog | `HotelAutomation.Data/Repositories/EventLogRepository.cs` |
| Outbox prune | `HotelAutomation.Data/Repositories/OutboxRepository.cs` |
| Schema / indexes | `HotelAutomation.Data/Schema.sql`, `SqliteConnectionFactory.cs` |
| Outbox send loop | `HotelAutomation.Worker/OutboxProcessor.cs` |
| Mutex / port | `HotelAutomation.Worker/Program.cs`, `Services/PortAndProcessManager.cs` |
| Jobs | `HotelAutomation.Worker/JobProcessor.cs`, `Data/Repositories/JobRepository.cs` |
| Campaigns | `HotelAutomation.Worker/Services/CampaignProcessor.cs` |
| Date triggers | `HotelAutomation.Worker/DateTriggerProcessor.cs` |
| PDF archive | `HotelAutomation.Connectors.Pms/PdfPmsConnector.cs`, `PdfBridgeOptions.cs` |
| SMS stub | `HotelAutomation.Worker/SmsMessageSender.cs` |
| WhatsApp HTTP | `HotelAutomation.Connectors.WhatsApp/WhatsAppApiClient.cs` |
| Config | `HotelAutomation.Worker/appsettings.json` |
