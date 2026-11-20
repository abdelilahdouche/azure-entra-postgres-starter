#!/usr/bin/env bash
# Run once per environment, after its first deployment. It connects the app registration to the
# deployed app:
#   - redirect URI = https://<container app FQDN>/signin-oidc
#   - federated identity credential trusting the environment's managed identity, so the app can
#     sign users in WITHOUT a client secret (Microsoft.Identity.Web "SignedAssertionFromManagedIdentity")
#
# Usage: scripts/entra/register-environment.sh <dev|test|prod>
set -euo pipefail

ENV_NAME="${1:?usage: $0 <dev|test|prod>}"
APP_NAME="${APP_NAME:-storeops}"
RG="rg-${APP_NAME}-${ENV_NAME}"
DISPLAY_NAME="${APP_NAME}-${ENV_NAME}"

az config set extension.use_dynamic_install=yes_without_prompt --only-show-errors >/dev/null

TENANT_ID="$(az account show --query tenantId -o tsv)"
APP_ID="$(az ad app list --display-name "$DISPLAY_NAME" --query '[0].appId' -o tsv)"
[[ -z "$APP_ID" ]] && { echo "App registration '$DISPLAY_NAME' not found. Run create-app-registration.sh first." >&2; exit 1; }

FQDN="$(az containerapp show -g "$RG" -n "ca-${APP_NAME}-${ENV_NAME}" --query properties.configuration.ingress.fqdn -o tsv)"
MI_PRINCIPAL_ID="$(az identity show -g "$RG" -n "id-${APP_NAME}-${ENV_NAME}" --query principalId -o tsv)"

REDIRECT_URIS=("https://${FQDN}/signin-oidc")
if [[ "$ENV_NAME" == "dev" ]]; then
  REDIRECT_URIS+=("https://localhost:7093/signin-oidc") # local development (launchSettings.json)
fi

az ad app update --id "$APP_ID" --web-redirect-uris "${REDIRECT_URIS[@]}"
echo "Redirect URIs: ${REDIRECT_URIS[*]}"

FIC_NAME="managed-identity-${ENV_NAME}"
if [[ -z "$(az ad app federated-credential list --id "$APP_ID" --query "[?name=='$FIC_NAME'].name" -o tsv)" ]]; then
  az ad app federated-credential create --id "$APP_ID" --parameters "{
    \"name\": \"$FIC_NAME\",
    \"issuer\": \"https://login.microsoftonline.com/${TENANT_ID}/v2.0\",
    \"subject\": \"${MI_PRINCIPAL_ID}\",
    \"audiences\": [\"api://AzureADTokenExchange\"],
    \"description\": \"Lets the ${ENV_NAME} managed identity act as this app's client credential (no secret).\"
  }" >/dev/null
  echo "Federated credential '$FIC_NAME' created for managed identity $MI_PRINCIPAL_ID"
else
  echo "Federated credential '$FIC_NAME' already exists"
fi

echo "✅ https://${FQDN} is ready for Entra sign-in."
