#!/usr/bin/env bash
# Creates the PostgreSQL role for the environment's managed identity and grants it the
# database rights it needs. Idempotent: safe to run on every deployment.
#
# It connects as the deployment identity (a PostgreSQL Entra admin, see infra/main.bicep) with a
# short-lived Entra token: no database password exists anywhere.
#
# Required environment variables:
#   RESOURCE_GROUP     resource group of the server
#   PG_SERVER          server name (without .postgres.database.azure.com)
#   PG_DATABASE        application database
#   APP_ROLE           PostgreSQL role = managed identity name (e.g. id-storeops-dev)
#   APP_PRINCIPAL_ID   managed identity principal (object) ID
#   PG_ADMIN_USER      Entra admin name used to connect (the deployer's display name)
set -euo pipefail

: "${RESOURCE_GROUP:?}" "${PG_SERVER:?}" "${PG_DATABASE:?}" "${APP_ROLE:?}" "${APP_PRINCIPAL_ID:?}" "${PG_ADMIN_USER:?}"

command -v psql >/dev/null || { sudo apt-get update -qq && sudo apt-get install -y -qq postgresql-client; }

# Temporarily allow this runner's IP. The rule is removed when the script exits, even on failure.
RUNNER_IP="$(curl -fsS https://api.ipify.org)"
RULE_NAME="deploy-${GITHUB_RUN_ID:-local}-$$"
az postgres flexible-server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" --name "$PG_SERVER" --rule-name "$RULE_NAME" \
  --start-ip-address "$RUNNER_IP" --end-ip-address "$RUNNER_IP" --only-show-errors >/dev/null
trap 'az postgres flexible-server firewall-rule delete --resource-group "$RESOURCE_GROUP" --name "$PG_SERVER" --rule-name "$RULE_NAME" --yes --only-show-errors >/dev/null' EXIT

export PGHOST="${PG_SERVER}.postgres.database.azure.com"
export PGUSER="$PG_ADMIN_USER"
export PGSSLMODE=require
PGPASSWORD="$(az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv)"
export PGPASSWORD

# 1. Map the managed identity to a PostgreSQL role (must run in the "postgres" database).
psql --dbname postgres -v ON_ERROR_STOP=1 -v role="$APP_ROLE" -v oid="$APP_PRINCIPAL_ID" <<'SQL'
SELECT CASE
  WHEN EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role') THEN 'role already exists'
  ELSE pgaadauth_create_principal_with_oid(:'role', :'oid', 'service', false, false)
END AS result;
SQL

# 2. Least privilege: connect to the app database and own its schema objects (for migrations).
#    No superuser, no access to other databases.
psql --dbname "$PG_DATABASE" -v ON_ERROR_STOP=1 -v role="$APP_ROLE" -v db="$PG_DATABASE" <<'SQL'
GRANT CONNECT, TEMPORARY ON DATABASE :"db" TO :"role";
GRANT USAGE, CREATE ON SCHEMA public TO :"role";
SQL

echo "✅ ${APP_ROLE} can use ${PG_DATABASE} with its Entra token."
