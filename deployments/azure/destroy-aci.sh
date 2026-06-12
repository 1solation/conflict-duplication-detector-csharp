#!/usr/bin/env bash
set -euo pipefail

# Tear down an ACI deployment created by create-aci.sh.
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
  echo "This will delete the Azure resource group and all resources inside it." >&2
  echo "Re-run with --yes to confirm." >&2
  exit 1
fi

if [[ -f "$STATE_FILE" ]]; then
  # shellcheck disable=SC1090
  source "$STATE_FILE"
fi

RG="${AZURE_RESOURCE_GROUP:-}"

if [[ -z "$RG" ]]; then
  echo "Error: AZURE_RESOURCE_GROUP is not set and $STATE_FILE was not found." >&2
  echo "Set AZURE_RESOURCE_GROUP or run create-aci.sh first." >&2
  exit 1
fi

if ! command -v az >/dev/null 2>&1; then
  echo "Error: 'az' is required but not installed." >&2
  exit 1
fi

az account show >/dev/null

if ! az group show --name "$RG" >/dev/null 2>&1; then
  echo "Resource group '$RG' does not exist (already deleted?)."
  rm -f "$STATE_FILE"
  exit 0
fi

echo "==> Deleting resource group '$RG' (this may take several minutes)..."
az group delete --name "$RG" --yes --no-wait

rm -f "$STATE_FILE"

echo ""
echo "Delete initiated for resource group '$RG'."
echo "Check progress: az group show --name $RG"
echo "Removed local state: $STATE_FILE"
