using System.CommandLine;
using ConflictDuplicationDetector.Cli.Commands;

var providerOption = new Option<string?>(
    name: "--provider",
    description: "AI provider: OpenAI or AzureOpenAI");
providerOption.AddAlias("-p");

var rootCommand = new RootCommand("Document Conflict & Duplication Detector - Multi-agent document analysis tool")
{
    new IngestCommand(providerOption),
    new AnalyseCommand(providerOption),
    new CheckCommand(providerOption),
    new ChatCommand(providerOption)
};

rootCommand.AddGlobalOption(providerOption);

rootCommand.Description = @"
A multi-agent system for analyzing document collections to detect:
  - Duplications: Exact and semantic duplicates across documents
  - Conflicts: Contradictions and policy conflicts
  - Inconsistencies: Terminology, formatting, and structural issues

Usage Examples:
  detector ingest ./documents --recursive
  detector analyse --type all --output results.json
  detector check ./new-document.docx --type all
  detector chat

  # Use Azure OpenAI provider
  detector --provider AzureOpenAI analyse --type all
  detector -p AzureOpenAI ingest ./documents

Environment Variables:
  OPENAI_API_KEY - Your OpenAI/Azure OpenAI API key (required)
  AZURE_OPENAI_ENDPOINT - Azure OpenAI endpoint (required for AzureOpenAI provider)
";

return await rootCommand.InvokeAsync(args);
