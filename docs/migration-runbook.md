# Runbook: moving a live app onto this platform

A generic plan for moving an existing web app (Replit, Heroku, a VM...) with a PostgreSQL
database, file uploads and scheduled jobs onto this platform, **without data loss and with a
tested way back**.

## 1. Discovery (week 1)

- [ ] Stack, runtime version, build and start commands, environment variables
- [ ] Database: PostgreSQL version, size, extensions (`SELECT * FROM pg_extension`), largest tables, write rate during store hours
- [ ] File uploads: where they are stored, total size, how files are referenced in the database
- [ ] Scheduled jobs: what runs, when, and whether each job is idempotent
- [ ] Outgoing email: provider and sender domain (SPF/DKIM)
- [ ] Current logins: users, roles, and the target mapping to Entra groups and app roles
- [ ] Quiet window: when the app can be read-only (for example after store close)
- [ ] Integrations: webhooks, callbacks and IP allow-lists that will change

## 2. Build the target (weeks 2–4)

- [ ] Deploy dev and test with this repository's pipeline
- [ ] Containerize the app (Dockerfile, port 8080, non-root user, `/healthz/live` and `/healthz/ready` endpoints)
- [ ] Replace the app's own login with Entra sign-in; map the old roles to app roles
- [ ] Move secrets to Key Vault and the database connection to managed identity
- [ ] Move file storage to Blob Storage (or a storage abstraction that reads both during the transition)
- [ ] Move scheduled jobs to Container Apps Jobs
- [ ] Move email to Azure Communication Services or keep the existing SMTP provider with its secret in Key Vault

## 3. Rehearsal in test (at least once, timed)

1. Restore a **copy of production** into the test server:
   ```bash
   pg_dump --format=custom --no-owner --no-acl "$SOURCE_URL" > prod.dump
   pg_restore --no-owner --no-acl --dbname "$TARGET_URL" prod.dump
   ```
   Restore as the Entra admin, then make sure the app role owns or can use the tables.
2. Copy the files: `azcopy copy <source> "https://<account>.blob.core.windows.net/uploads" --recursive`
3. Run the validation queries (below) on the source and on the target, and compare them.
4. Have 2–3 store users run their daily tasks on test.
5. **Rehearse the rollback** too.
6. Write down how long each step took. That sets the cutover window and the go/no-go time.

### Validation queries

```sql
-- Row counts per table
SELECT relname, n_live_tup FROM pg_stat_user_tables ORDER BY relname;

-- Checksums on business-critical tables (run on source and target, compare)
SELECT md5(string_agg(t::text, '' ORDER BY id)) FROM reports t;

-- Sequences: make sure new IDs won't collide
SELECT sequencename, last_value FROM pg_sequences;
```

## 4. Cutover (after store hours)

| T | Step | Owner |
|---|---|---|
| T-24h | Lower DNS TTL to 60 s. Announce the maintenance window to stores | Tech lead / IT |
| T-0 | **Freeze**: old app in maintenance / read-only mode. Stop old scheduled jobs | Tech lead |
| T+5 | Final `pg_dump` → `pg_restore` into prod. Final file sync (`azcopy sync`) | Tech lead |
| T+20 | Validation queries: counts and checksums must match | Tech lead |
| T+30 | Run migrations job. Smoke test `/healthz/ready`. Test logins with 1 user per role | Tech lead + in-house dev |
| T+40 | **Go / no-go** | Business owner |
| T+45 | Switch DNS to the Container App (custom domain + managed certificate) | Tech lead |
| T+60 | Enable the new scheduled jobs. Watch Application Insights | Tech lead |
| Next morning | Hypercare: on call when stores open, watch error rate and support tickets | Tech lead |

For large databases or very short windows, replace the dump/restore with **logical replication**
or **Azure Database Migration Service (online)**. The target then stays in sync, and the freeze
only lasts as long as the final catch-up.

## 5. Rollback plan

The old app and its database are **never modified** during the cutover. They stay frozen and
intact.

- **Before go/no-go:** cancel. Remove maintenance mode on the old app. No data was written on Azure.
- **After DNS switch, within the window:** put Azure in maintenance, switch DNS back (TTL 60 s), remove maintenance on the old app.
- **After stores have written data on Azure:** export the rows created since the cutover (`WHERE created_at > :cutover`) and replay them on the old database before switching back. This script is prepared and tested during the rehearsal.

Decision rule: roll back if a critical store workflow (sign-in, daily report, an order) is
broken and can't be fixed within 30 minutes.

## 6. After the cutover

- [ ] Keep the old app read-only for 2 weeks, then archive a final dump and shut it down
- [ ] Raise DNS TTL back to normal
- [ ] Handover to the in-house developer: walkthrough of the repo, pipeline, runbooks and alerts
- [ ] Remove temporary access (firewall rules, guest accounts no longer needed)
