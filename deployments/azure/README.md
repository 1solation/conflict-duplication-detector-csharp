# Deploying to Azure

This guide covers deploying the Conflict & Duplication Detector API as a container to Azure using **Azure Container Apps** or **Azure Container Instances**.

## Prerequisites

- Azure subscription
- Azure CLI installed (`az --version`)
- Docker installed (for building/pushing images)
- OpenAI API key

## Configuration via Environment Variables

The application can be configured entirely through environment variables, making it ideal for container deployments. These replace the `appsettings.json` configuration.

### Required Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `OPENAI_API_KEY` | Your OpenAI or Azure OpenAI API key | `sk-...` or Azure key |
| `Auth__ApiKey` | Inbound API key clients must send in the `X-Api-Key` header | any strong secret string |

### Optional Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `OpenAI__Provider` | AI provider: `OpenAI` or `AzureOpenAI` | `OpenAI` |
| `OpenAI__Model` | Model deployment name for analysis | `gpt-4o` |
| `OpenAI__EmbeddingModel` | Model for embeddings | `text-embedding-3-small` |
| `OpenAI__AzureEndpoint` | Azure OpenAI endpoint URL | `null` |
| `OpenAI__AzureApiVersion` | Azure OpenAI API version | `2024-02-01` |
| `OpenAI__ApiKeyHeader` | Custom API key header name (e.g., `Api-Key`) | `null` |
| `VectorStore__PersistPath` | Path to persist vector store | `/data/vectors.json` |
| `VectorStore__MaxSearchResults` | Max results from vector search | `10` |
| `Storage__UploadsPath` | Path for uploaded files | `/data/uploads` |
| `Analysis__DuplicationThreshold` | Similarity threshold for duplicates | `0.85` |
| `Analysis__ChunkSize` | Document chunk size | `512` |
| `Analysis__ChunkOverlap` | Overlap between chunks | `50` |
| `Analysis__MaxConcurrentAgents` | Max parallel agent execution | `3` |
| `Jobs__RetentionHours` | How long to keep job results | `24` |

> **Note**: Use double underscores (`__`) to represent nested configuration in environment variables (e.g., `OpenAI__Model` maps to `OpenAI.Model` in appsettings.json).

---

## Option 1: Azure Container Apps (Recommended)

Azure Container Apps provides a fully managed serverless container platform with built-in scaling, HTTPS ingress, and persistent storage.

### Step 1: Build and Push Container Image

```bash
# Login to Azure
az login

# Create a resource group (if needed)
az group create --name rg-conflict-detector --location uksouth

# Create Azure Container Registry
az acr create --resource-group rg-conflict-detector \
  --name crconflictdetector --sku Basic

# Login to ACR
az acr login --name crconflictdetector

# Build and push image
docker build -t crconflictdetector.azurecr.io/conflict-detector:latest .
docker push crconflictdetector.azurecr.io/conflict-detector:latest
```

### Step 2: Create Container Apps Environment

```bash
# Create Container Apps environment
az containerapp env create \
  --name cae-conflict-detector \
  --resource-group rg-conflict-detector \
  --location uksouth
```

### Step 3: Create Azure Files Storage (for persistence)

```bash
# Create storage account
az storage account create \
  --name stconflictdetector \
  --resource-group rg-conflict-detector \
  --location uksouth \
  --sku Standard_LRS

# Get storage account key
STORAGE_KEY=$(az storage account keys list \
  --resource-group rg-conflict-detector \
  --account-name stconflictdetector \
  --query '[0].value' -o tsv)

# Create file share
az storage share create \
  --name data \
  --account-name stconflictdetector \
  --account-key $STORAGE_KEY

# Add storage to Container Apps environment
az containerapp env storage set \
  --name cae-conflict-detector \
  --resource-group rg-conflict-detector \
  --storage-name data \
  --azure-file-account-name stconflictdetector \
  --azure-file-account-key $STORAGE_KEY \
  --azure-file-share-name data \
  --access-mode ReadWrite
```

### Step 4: Deploy Container App

```bash
# Enable ACR admin credentials
az acr update --name crconflictdetector --admin-enabled true

# Get ACR credentials
ACR_PASSWORD=$(az acr credential show \
  --name crconflictdetector \
  --query 'passwords[0].value' -o tsv)

# Create Container App
az containerapp create \
  --name ca-conflict-detector \
  --resource-group rg-conflict-detector \
  --environment cae-conflict-detector \
  --image crconflictdetector.azurecr.io/conflict-detector:latest \
  --registry-server crconflictdetector.azurecr.io \
  --registry-username crconflictdetector \
  --registry-password $ACR_PASSWORD \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 3 \
  --cpu 1.0 \
  --memory 2Gi \
  --env-vars \
    "OPENAI_API_KEY=secretref:openai-key" \
    "Auth__ApiKey=secretref:api-key" \
    "VectorStore__PersistPath=/data/vectors.json" \
    "Storage__UploadsPath=/data/uploads" \
  --secrets "openai-key=YOUR_OPENAI_API_KEY_HERE" "api-key=YOUR_INBOUND_API_KEY_HERE"
```

### Step 5: Configure Environment Variables via Azure Portal

1. Navigate to [Azure Portal](https://portal.azure.com)
2. Go to **Container Apps** → **ca-conflict-detector**
3. Click **Containers** in the left menu
4. Click **Edit and deploy** → **Container image**
5. Scroll to **Environment variables**
6. Add or update variables:

   | Name | Source | Value |
   |------|--------|-------|
   | `OPENAI_API_KEY` | Reference a secret | `openai-key` |
   | `Auth__ApiKey` | Reference a secret | `api-key` |
   | `OpenAI__Model` | Manual entry | `gpt-4o` |
   | `OpenAI__EmbeddingModel` | Manual entry | `text-embedding-3-small` |
   | `VectorStore__PersistPath` | Manual entry | `/data/vectors.json` |
   | `Storage__UploadsPath` | Manual entry | `/data/uploads` |
   | `Analysis__DuplicationThreshold` | Manual entry | `0.85` |

7. Click **Create** to deploy the new revision

### Managing Secrets in Azure Portal

1. Go to **Container Apps** → **ca-conflict-detector**
2. Click **Secrets** in the left menu
3. Click **+ Add** and create both secrets:

   | Key | Value |
   |-----|-------|
   | `openai-key` | Your OpenAI / Azure OpenAI API key |
   | `api-key` | The inbound API key clients will send in `X-Api-Key` |

4. Click **Add**

Then reference secrets in environment variables using `secretref:<key>`, e.g. `secretref:api-key`.

---

## Option 2: Azure Container Instances (Simpler, No Scaling)

For simpler deployments without auto-scaling:

### Step 1: Create Container Instance

```bash
# Create ACI with environment variables
az container create \
  --resource-group rg-conflict-detector \
  --name aci-conflict-detector \
  --image crconflictdetector.azurecr.io/conflict-detector:latest \
  --registry-login-server crconflictdetector.azurecr.io \
  --registry-username crconflictdetector \
  --registry-password $ACR_PASSWORD \
  --dns-name-label conflict-detector \
  --ports 8080 \
  --cpu 2 \
  --memory 4 \
  --environment-variables \
    OpenAI__Model=gpt-4o \
    OpenAI__EmbeddingModel=text-embedding-3-small \
    VectorStore__PersistPath=/data/vectors.json \
    Storage__UploadsPath=/data/uploads \
  --secure-environment-variables \
    OPENAI_API_KEY=YOUR_OPENAI_API_KEY_HERE \
  --azure-file-volume-account-name stconflictdetector \
  --azure-file-volume-account-key $STORAGE_KEY \
  --azure-file-volume-share-name data \
  --azure-file-volume-mount-path /data
```

### Configure via Azure Portal

1. Navigate to [Azure Portal](https://portal.azure.com)
2. Go to **Container Instances** → **aci-conflict-detector**
3. Click **Containers** in the left menu
4. Click **Settings** → **Environment variables**
5. View existing variables or recreate with updated values

> **Note**: ACI requires recreating the container to update environment variables. Use the CLI or redeploy.

---

## Using Azure OpenAI Instead of OpenAI

If you're using Azure OpenAI Service instead of the public OpenAI API:

```bash
# Set these environment variables
OpenAI__Provider=AzureOpenAI
OpenAI__AzureEndpoint=https://your-resource.openai.azure.com/
OpenAI__AzureApiVersion=2024-02-01
OpenAI__Model=your-deployment-name
OpenAI__EmbeddingModel=your-embedding-deployment-name
OPENAI_API_KEY=your-azure-openai-key

# If using a custom API key header (e.g., DfE sandbox)
OpenAI__ApiKeyHeader=Api-Key
```

In Azure Portal, add these as environment variables:

| Name | Value |
|------|-------|
| `OpenAI__Provider` | `AzureOpenAI` |
| `OpenAI__AzureEndpoint` | `https://your-resource.openai.azure.com/` |
| `OpenAI__AzureApiVersion` | `2024-02-01` |
| `OpenAI__Model` | Your deployed model name |
| `OpenAI__EmbeddingModel` | Your embedding model deployment name |
| `OpenAI__ApiKeyHeader` | Custom header name (e.g., `Api-Key`) - optional |
| `OPENAI_API_KEY` | (use secret reference) |

### Example: DfE Education Sandbox

For the Department for Education OpenAI sandbox:

```bash
OpenAI__Provider=AzureOpenAI
OpenAI__AzureEndpoint=https://api.education.gov.uk/sandbox/openai
OpenAI__ApiKeyHeader=Api-Key
OpenAI__Model=gpt-4o
OpenAI__EmbeddingModel=text-embedding-3-small
OPENAI_API_KEY=your-dfe-api-key
```

---

## Verify Deployment

Once deployed, access the API:

```bash
# Get the FQDN
az containerapp show \
  --name ca-conflict-detector \
  --resource-group rg-conflict-detector \
  --query properties.configuration.ingress.fqdn -o tsv

# Test health endpoint
curl https://ca-conflict-detector.<region>.azurecontainerapps.io/api/health
```

Open Swagger UI at: `https://ca-conflict-detector.<region>.azurecontainerapps.io/swagger`

---

## Troubleshooting

### View Container Logs

```bash
# Container Apps
az containerapp logs show \
  --name ca-conflict-detector \
  --resource-group rg-conflict-detector \
  --follow

# Container Instances
az container logs \
  --resource-group rg-conflict-detector \
  --name aci-conflict-detector
```

### Common Issues

| Issue | Solution |
|-------|----------|
| `503 Service Unavailable` | Check `OPENAI_API_KEY` is set correctly |
| Container fails to start | Check logs for missing environment variables |
| Data not persisting | Verify Azure Files mount at `/data` |
| Slow response times | Increase CPU/memory allocation |
