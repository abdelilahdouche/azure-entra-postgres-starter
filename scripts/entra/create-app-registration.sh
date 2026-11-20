#!/usr/bin/env bash
# Creates (or updates) the Entra app registration used for USER SIGN-IN in one environment:
#   - single-tenant web app, no client secret
#   - app roles from app-roles.json
#   - "assignment required": only users/groups assigned to a role can sign in
#   - optional: assigns your Entra groups to the roles
#
# Usage:
#   STAFF_GROUP_ID=<objectId> MANAGER_GROUP_ID=<objectId> ADMIN_GROUP_ID=<objectId> \
#     scripts/entra/create-app-registration.sh <dev|test|prod>
#
# Requires: az (logged in as a user allowed to create app registrations), jq.
# Assigning groups to app roles requires Microsoft Entra ID P1 (or assign users directly).
set -euo pipefail

ENV_NAME="${1:?usage: $0 <dev|test|prod>}"
APP_NAME="${APP_NAME:-storeops}"
DISPLAY_NAME="${APP_NAME}-${ENV_NAME}"
ROLES_FILE="$(cd "$(dirname "$0")" && pwd)/app-roles.json"
GRAPH="https://graph.microsoft.com/v1.0"
GRAPH_APP_ID="00000003-0000-0000-c000-000000000000"
GRAPH_USER_READ="e1fe6dd8-ba31-4d61-89e7-88639da4683d" # delegated User.Read

APP_ID="$(az ad app list --display-name "$DISPLAY_NAME" --query '[0].appId' -o tsv)"
if [[ -z "$APP_ID" ]]; then
  echo "Creating app registration '$DISPLAY_NAME'..."
  APP_ID="$(az ad app create --display-name "$DISPLAY_NAME" --sign-in-audience AzureADMyOrg --query appId -o tsv)"
else
  echo "App registration '$DISPLAY_NAME' already exists ($APP_ID), updating."
fi

az ad app update --id "$APP_ID" --app-roles "@${ROLES_FILE}" --enable-id-token-issuance false
az ad app permission add --id "$APP_ID" --api "$GRAPH_APP_ID" --api-permissions "${GRAPH_USER_READ}=Scope" 2>/dev/null || true

SP_ID="$(az ad sp list --filter "appId eq '$APP_ID'" --query '[0].id' -o tsv)"
if [[ -z "$SP_ID" ]]; then
  SP_ID="$(az ad sp create --id "$APP_ID" --query id -o tsv)"
fi
az ad sp update --id "$SP_ID" --set appRoleAssignmentRequired=true

az ad app permission admin-consent --id "$APP_ID" 2>/dev/null \
  || echo "⚠️  Could not grant admin consent for User.Read (needs an admin). Users will be asked to consent at first sign-in."

assign_group() {
  local group_id="$1" role_value="$2" role_id existing
  [[ -z "$group_id" ]] && return 0
  role_id="$(jq -r --arg v "$role_value" '.[] | select(.value == $v) | .id' "$ROLES_FILE")"
  existing="$(az rest --method GET --url "$GRAPH/servicePrincipals/$SP_ID/appRoleAssignedTo" \
    --query "length(value[?principalId=='$group_id' && appRoleId=='$role_id'])" -o tsv)"
  if [[ "$existing" == "0" ]]; then
    az rest --method POST --url "$GRAPH/servicePrincipals/$SP_ID/appRoleAssignedTo" \
      --headers "Content-Type=application/json" \
      --body "{\"principalId\":\"$group_id\",\"resourceId\":\"$SP_ID\",\"appRoleId\":\"$role_id\"}" >/dev/null
    echo "Assigned group $group_id -> $role_value"
  fi
}

assign_group "${STAFF_GROUP_ID:-}" "Store.Staff"
assign_group "${MANAGER_GROUP_ID:-}" "Store.Manager"
assign_group "${ADMIN_GROUP_ID:-}" "StoreOps.Admin"

ENV_UPPER="$(echo "$ENV_NAME" | tr '[:lower:]' '[:upper:]')"
cat <<EOF

✅ Done. Add this GitHub repository variable:
   ENTRA_CLIENT_ID_${ENV_UPPER} = ${APP_ID}

After the first deployment of '${ENV_NAME}', run:
   scripts/entra/register-environment.sh ${ENV_NAME}
EOF
