using System.CommandLine;
using ConflictDuplicationDetector.Cli.Commands;

var rootCommand = new RootCommand("Document Conflict & Duplication Detector - Multi-agent document analysis tool")
{
    new IngestCommand(),
    new AnalyzeCommand(),
    new CheckCommand(),
    new ChatCommand()
};

rootCommand.Description = @"
A multi-agent system for analyzing document collections to detect:
  - Duplications: Exact and semantic duplicates across documents
  - Conflicts: Contradictions and policy conflicts
  - Inconsistencies: Terminology, formatting, and structural issues

Usage Examples:
  detector ingest ./documents --recursive
  detector analyze --type all --output results.json
  detector check ./new-document.docx --type all
  detector chat

Environment Variables:
  OPENAI_API_KEY - Your OpenAI API key (required)
  AZURE_OPENAI_ENDPOINT - Azure OpenAI endpoint (optional, for Azure)
";

return await rootCommand.InvokeAsync(args);
