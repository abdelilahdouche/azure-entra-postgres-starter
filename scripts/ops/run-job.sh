#!/usr/bin/env bash
# Starts a Container Apps Job execution (e.g. database migrations) and waits for the result.
# Usage: scripts/ops/run-job.sh <resource-group> <job-name> [timeout-seconds]
set -euo pipefail

RG="${1:?usage: $0 <resource-group> <job-name> [timeout-seconds]}"
JOB="${2:?usage: $0 <resource-group> <job-name> [timeout-seconds]}"
TIMEOUT="${3:-900}"

EXECUTION="$(az containerapp job start --resource-group "$RG" --name "$JOB" --query name -o tsv)"
echo "Started ${JOB} execution ${EXECUTION}"

deadline=$((SECONDS + TIMEOUT))
while (( SECONDS < deadline )); do
  STATUS="$(az containerapp job execution show --resource-group "$RG" --name "$JOB" \
    --job-execution-name "$EXECUTION" --query properties.status -o tsv)"
  case "$STATUS" in
    Succeeded)
      echo "✅ ${EXECUTION} succeeded"
      exit 0 ;;
    Failed|Stopped|Degraded)
      echo "::error::${JOB} execution ${EXECUTION} ended with status ${STATUS}."
      echo "Logs (Log Analytics): ContainerAppConsoleLogs_CL | where ContainerGroupName_s startswith '${EXECUTION}'"
      exit 1 ;;
    *)
      echo "… ${STATUS:-Pending}"
      sleep 10 ;;
  esac
done

echo "::error::${JOB} execution ${EXECUTION} did not finish within ${TIMEOUT}s."
exit 1
