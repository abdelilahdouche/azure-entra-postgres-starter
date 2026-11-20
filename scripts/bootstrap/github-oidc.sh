#!/usr/bin/env bash
# One-time setup of the identity GitHub Actions uses to deploy. It uses OpenID Connect,
# so there is NO client secret stored in GitHub.
#
#   - app registration + service principal "gh-<app>-deployer"
#   - federated credentials for: main branch, pull requests, environments dev/test/prod
#   - Contributor on the subscription
#   - Role Based Access Control Administrator, restricted by a condition to the few roles the
#     templates assign (AcrPull, Storage Blob Data Contributor, Key Vault Secrets User,
#     Key Vault Crypto Service Encryption User)
#
# Usage: scripts/bootstrap/github-oidc.sh <github-owner>/<repo>
set -euo pipefail

GITHUB_REPO="${1:?usage: $0 <owner/repo>}"
APP_NAME="${APP_NAME:-storeops}"
DISPLAY_NAME="gh-${APP_NAME}-deployer"
SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
TENANT_ID="$(az account show --query tenantId -o tsv)"
SCOPE="/subscriptions/${SUBSCRIPTION_ID}"

APP_ID="$(az ad app list --display-name "$DISPLAY_NAME" --query '[0].appId' -o tsv)"
if [[ -z "$APP_ID" ]]; then
  APP_ID="$(az ad app create --display-name "$DISPLAY_NAME" --sign-in-audience AzureADMyOrg --query appId -o tsv)"
fi

SP_ID="$(az ad sp list --filter "appId eq '$APP_ID'" --query '[0].id' -o tsv)"
if [[ -z "$SP_ID" ]]; then
  SP_ID="$(az ad sp create --id "$APP_ID" --query id -o tsv)"
fi

add_federated_credential() {
  local name="$1" subject="$2"
  if [[ -z "$(az ad app federated-credential list --id "$APP_ID" --query "[?name=='$name'].name" -o tsv)" ]]; then
    az ad app federated-credential create --id "$APP_ID" --parameters "{
      \"name\": \"$name\",
      \"issuer\": \"https://token.actions.githubusercontent.com\",
      \"subject\": \"$subject\",
      \"audiences\": [\"api://AzureADTokenExchange\"]
    }" >/dev/null
    echo "Federated credential: $subject"
  fi
}

add_federated_credential "main" "repo:${GITHUB_REPO}:ref:refs/heads/main"
add_federated_credential "pull-request" "repo:${GITHUB_REPO}:pull_request"
for env in dev test prod; do
  add_federated_credential "environment-${env}" "repo:${GITHUB_REPO}:environment:${env}"
done

az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
  --role Contributor --scope "$SCOPE" --only-show-errors >/dev/null

# Delegated role-assignment rights, limited to the roles used by infra/ (least privilege).
ALLOWED_ROLES="7f951dda-4ed3-4680-a7ca-43fe172d538d, ba92f5b4-2d11-453d-a403-e96b0029c9fe, 4633458b-17de-408a-b874-0445c86b69e6, e147488a-f6f5-4113-8e2d-b22465e65bf6"
CONDITION="((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${ALLOWED_ROLES}})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${ALLOWED_ROLES}}))"

az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
  --role "Role Based Access Control Administrator" --scope "$SCOPE" \
  --condition "$CONDITION" --condition-version "2.0" --only-show-errors >/dev/null

cat <<EOF

✅ Deployment identity ready. Add these GitHub repository variables (Settings > Secrets and variables > Actions > Variables):

   AZURE_TENANT_ID        = ${TENANT_ID}
   AZURE_SUBSCRIPTION_ID  = ${SUBSCRIPTION_ID}
   AZURE_CLIENT_ID        = ${APP_ID}
   DEPLOYER_OBJECT_ID     = ${SP_ID}
   DEPLOYER_NAME          = ${DISPLAY_NAME}
   AZURE_LOCATION         = canadacentral   (optional)

Then create the GitHub environments dev, test and prod (add required reviewers on prod).
None of these values is a secret.
EOF
