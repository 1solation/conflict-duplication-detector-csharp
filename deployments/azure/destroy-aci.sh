#!/usr/bin/env bash
set -euo pipefail

# Tear down ACI resources created by create-aci.sh, while leaving the resource group intact.
#
# Usage:
#   ./deployments/azure/destroy-aci.sh --yes
#   AZURE_RESOURCE_GROUP=my-rg ./deployments/azure/destroy-aci.sh --yes

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STATE_FILE="$SCRIPT_DIR/.aci-deploy.env"

CONFIRM=false
for arg in "$@"; do
  case "$arg" in
    -y|--yes) CONFIRM=true ;;
    -h|--help)
      sed -n '2,8p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *)
      echo "Unknown option: $arg" >&2
      exit 1
      ;;
  esac
done

if [[ "$CONFIRM" != true ]]; then
  echo "This will delete the ACI container and Azure Container Registry, but leave the resource group intact." >&2
  echo "Re-run with --yes to confirm." >&2
  exit 1
fi

if [[ -f "$STATE_FILE" ]]; then
  # shellcheck disable=SC1090
  source "$STATE_FILE"
fi

RG="${AZURE_RESOURCE_GROUP:-}"
CONTAINER_NAME="${ACI_CONTAINER_NAME:-aci-document-analysis}"
ACR="${ACI_ACR:-}"

if [[ -z "$RG" ]]; then
  echo "Error: AZURE_RESOURCE_GROUP is not set and $STATE_FILE was not found." >&2
  echo "Set AZURE_RESOURCE_GROUP or run create-aci.sh first." >&2
  exit 1
fi

if [[ -z "$ACR" ]]; then
  echo "Error: ACI_ACR is not set and $STATE_FILE was not found." >&2
  echo "Set ACI_ACR or run create-aci.sh first." >&2
  exit 1
fi

if ! command -v az >/dev/null 2>&1; then
  echo "Error: 'az' is required but not installed." >&2
  exit 1
fi

az account show >/dev/null

if ! az group show --name "$RG" >/dev/null 2>&1; then
  echo "Resource group '$RG' does not exist."
  rm -f "$STATE_FILE"
  exit 0
fi

if az container show --resource-group "$RG" --name "$CONTAINER_NAME" >/dev/null 2>&1; then
  echo "==> Deleting container instance '$CONTAINER_NAME'..."
  az container delete --resource-group "$RG" --name "$CONTAINER_NAME" --yes --output none
else
  echo "Container instance '$CONTAINER_NAME' was not found in resource group '$RG'."
fi

if az acr show --name "$ACR" >/dev/null 2>&1; then
  ACR_RG="$(az acr show --name "$ACR" --query resourceGroup -o tsv)"
  echo "==> Deleting Azure Container Registry '$ACR' from resource group '$ACR_RG'..."
  az acr delete --resource-group "$ACR_RG" --name "$ACR" --yes --output none
else
  echo "Azure Container Registry '$ACR' was not found."
fi

rm -f "$STATE_FILE"

echo ""
echo "Deleted ACI container and registry resources where present."
echo "Resource group left intact: $RG"
echo "Removed local state: $STATE_FILE"
