# Document Conflict & Duplication Detector

A multi-agent system built with C#/.NET that analyzes document collections to detect conflicts, duplications, and inconsistencies using semantic search and AI-powered analysis.

## Features

- **Multi-Agent Architecture**: Orchestrator coordinates specialized agents for parallel analysis
  - **Orchestrator Agent**: Coordinates workflow and aggregates results
  - **Duplication Agent**: Detects exact and semantic duplicates
  - **Conflict Agent**: Identifies contradictions and policy conflicts
  - **Inconsistency Agent**: Finds terminology and structural inconsistencies
- **Document Processing**: Supports PDF, DOCX, HTML, and text formats
- **Knowledge Base Guardrails**: Agents only use information from the vector store, never training data
- **Performance Metrics**: Tracks network time, calculation time, and token usage per agent
- **Persistent Storage**: SharpVector in-memory vector database with file persistence
- **Interactive Chat**: Multi-agent chat interface with automatic workflow routing
- **CLI Interface**: Full command-line interface for batch processing

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      CLI Interface                           │
│              (ingest, analyze, chat commands)                │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                  Orchestrator Agent                          │
│            (routes queries, aggregates results)              │
└──────┬──────────────────┬──────────────────┬────────────────┘
       │                  │                  │
┌──────▼──────┐   ┌───────▼───────┐   ┌──────▼──────┐
│ Duplication │   │   Conflict    │   │Inconsistency│
│   Agent     │   │    Agent      │   │   Agent     │
└──────┬──────┘   └───────┬───────┘   └──────┬──────┘
       │                  │                  │
       └──────────────────┼──────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                    Vector Store                              │
│           (SharpVector + OpenAI Embeddings)                  │
└─────────────────────────────────────────────────────────────┘
```

## Prerequisites

- .NET 8.0 SDK or later
- OpenAI API key (or Azure OpenAI)

## Installation

1. Clone the repository:
```bash
git clone https://github.com/yourusername/conflict-duplication-detector-csharp.git
cd conflict-duplication-detector-csharp
```

2. Build the solution:
```bash
dotnet build
```

3. Set your OpenAI API key:
```bash
export OPENAI_API_KEY="your-api-key-here"
```

## Usage

### Ingest Documents

Add documents to the knowledge base:

```bash
# Ingest a single file
dotnet run --project src/ConflictDuplicationDetector.Cli -- ingest ./documents/policy.pdf

# Ingest a directory recursively
dotnet run --project src/ConflictDuplicationDetector.Cli -- ingest ./documents --recursive
```

### Analyze Documents

Run analysis on ingested documents:

```bash
# Run full analysis
dotnet run --project src/ConflictDuplicationDetector.Cli -- analyze

# Analyze specific type
dotnet run --project src/ConflictDuplicationDetector.Cli -- analyze --type duplications
dotnet run --project src/ConflictDuplicationDetector.Cli -- analyze --type conflicts
dotnet run --project src/ConflictDuplicationDetector.Cli -- analyze --type inconsistencies

# Save results to file
dotnet run --project src/ConflictDuplicationDetector.Cli -- analyze --output results.json

# Focus on specific topic
dotnet run --project src/ConflictDuplicationDetector.Cli -- analyze --type conflicts --topic "pricing policy"
```

### Interactive Chat

Start an interactive session:

```bash
dotnet run --project src/ConflictDuplicationDetector.Cli -- chat
```

Example chat queries:
- "Find all duplicate content"
- "Are there any conflicts related to pricing?"
- "What terminology inconsistencies exist?"
- "Run full analysis"

## Configuration

Configuration can be set via `appsettings.json` or environment variables:

```json
{
  "OpenAI": {
    "ApiKey": "env:OPENAI_API_KEY",
    "Model": "gpt-4o",
    "EmbeddingModel": "text-embedding-3-small",
    "AzureEndpoint": null
  },
  "VectorStore": {
    "PersistPath": "./data/vectors.json",
    "MaxSearchResults": 10
  },
  "Analysis": {
    "DuplicationThreshold": 0.85,
    "ChunkSize": 512,
    "ChunkOverlap": 50
  }
}
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `OPENAI_API_KEY` | OpenAI API key (required) |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI endpoint (optional) |

## Project Structure

```
ConflictDuplicationDetector/
├── ConflictDuplicationDetector.sln
├── src/
│   ├── ConflictDuplicationDetector.Core/
│   │   ├── Agents/           # AI agents (Orchestrator, Duplication, Conflict, Inconsistency)
│   │   ├── Documents/        # Document parsers (PDF, DOCX, HTML)
│   │   ├── Models/           # Data models and configuration
│   │   ├── Services/         # Business logic services
│   │   └── VectorStore/      # Vector database wrapper
│   └── ConflictDuplicationDetector.Cli/
│       ├── Commands/         # CLI commands
│       └── Program.cs        # Entry point
└── tests/
    └── ConflictDuplicationDetector.Tests/
```

## Supported Document Formats

| Format | Extensions | Parser |
|--------|------------|--------|
| PDF | `.pdf` | UglyToad.PdfPig |
| Word | `.docx`, `.docm` | DocumentFormat.OpenXml |
| HTML | `.html`, `.htm`, `.xhtml` | HtmlAgilityPack |
| Text | `.txt`, `.md` | Built-in |

## Analysis Types

### Duplication Detection
- **Exact duplicates**: Identical content (hash-based)
- **Semantic duplicates**: Same meaning, different wording (vector similarity ≥85%)
- **Near duplicates**: Minor variations like typos or formatting

### Conflict Detection
- **Contradictions**: Statements that directly oppose each other
- **Policy conflicts**: Incompatible requirements ("must" vs "must not")
- **Version mismatches**: Different values for the same data
- **Logical inconsistencies**: Conclusions contradicting premises

### Inconsistency Detection
- **Terminology**: Same concept with different names
- **Formatting**: Date, number, unit format variations
- **Structure**: Different organizational patterns
- **Naming conventions**: Inconsistent identifier patterns

## Performance Metrics

The system tracks detailed metrics for each agent:
- **Network Time**: Time spent on API calls
- **Calculation Time**: Local processing time
- **Token Usage**: Input and output tokens consumed
- **Call Count**: Number of API invocations

## Knowledge Base Guardrails

Agents are constrained to only use information from the vector store through:
1. System prompts that explicitly forbid using training data
2. RAG pattern: retrieve relevant chunks before each query
3. Context injection: all queries include retrieved document chunks
4. Agents are instructed to say "No evidence found" when context is insufficient

## Development

### Running Tests

```bash
dotnet test
```

### Building for Release

```bash
dotnet publish -c Release
```

## Technologies

- **Framework**: .NET 8.0
- **AI Integration**: Microsoft.Extensions.AI, OpenAI SDK
- **Vector Database**: Build5Nines.SharpVector
- **Document Parsing**: UglyToad.PdfPig, DocumentFormat.OpenXml, HtmlAgilityPack
- **CLI**: System.CommandLine

## License

MIT License - see [LICENSE](LICENSE) for details.
