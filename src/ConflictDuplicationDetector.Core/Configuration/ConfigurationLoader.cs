using ConflictDuplicationDetector.Core.Models;
using Microsoft.Extensions.Configuration;

namespace ConflictDuplicationDetector.Core.Configuration;

public static class ConfigurationLoader
{
    public static AppConfiguration Bind(IConfiguration configuration)
    {
        LoadEnvFile();

        var appConfig = new AppConfiguration();

        appConfig.OpenAI.ApiKey = GetConfigValue(configuration, "OpenAI:ApiKey", "OPENAI_API_KEY") ?? string.Empty;
        appConfig.OpenAI.AzureEndpoint = GetConfigValue(configuration, "OpenAI:AzureEndpoint", "AZURE_OPENAI_ENDPOINT");
        appConfig.OpenAI.AzureApiVersion = configuration["OpenAI:AzureApiVersion"] ?? "2024-02-01";
        appConfig.OpenAI.ApiKeyHeader = configuration["OpenAI:ApiKeyHeader"];
        appConfig.OpenAI.Model = configuration["OpenAI:Model"] ?? "gpt-4o";
        appConfig.OpenAI.EmbeddingModel = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
        
        var providerStr = configuration["OpenAI:Provider"];
        if (!string.IsNullOrEmpty(providerStr) && Enum.TryParse<AIProvider>(providerStr, ignoreCase: true, out var provider))
        {
            appConfig.OpenAI.Provider = provider;
        }

        appConfig.VectorStore.PersistPath = configuration["VectorStore:PersistPath"] ?? "./data/vectors.json";
        if (int.TryParse(configuration["VectorStore:MaxSearchResults"], out var maxResults))
            appConfig.VectorStore.MaxSearchResults = maxResults;

        if (double.TryParse(configuration["Analysis:DuplicationThreshold"], out var threshold))
            appConfig.Analysis.DuplicationThreshold = threshold;
        if (int.TryParse(configuration["Analysis:ChunkSize"], out var chunkSize))
            appConfig.Analysis.ChunkSize = chunkSize;
        if (int.TryParse(configuration["Analysis:ChunkOverlap"], out var overlap))
            appConfig.Analysis.ChunkOverlap = overlap;
        if (int.TryParse(configuration["Analysis:MaxConcurrentAgents"], out var maxAgents))
            appConfig.Analysis.MaxConcurrentAgents = maxAgents;

        var uploadsPath = configuration["Storage:UploadsPath"];
        appConfig.Storage.UploadsPath = string.IsNullOrEmpty(uploadsPath)
            ? Path.Combine(Path.GetDirectoryName(appConfig.VectorStore.PersistPath) ?? "./data", "uploads")
            : uploadsPath;

        return appConfig;
    }

    public static AppConfiguration Load(string? configPath = null)
    {
        LoadEnvFile();

        var builder = new ConfigurationBuilder();
        var basePath = AppContext.BaseDirectory;
        var currentDir = Directory.GetCurrentDirectory();

        AddJsonFileIfExists(builder, Path.Combine(basePath, "appsettings.json"));

        var currentDirConfig = Path.Combine(currentDir, "appsettings.json");
        if (currentDirConfig != Path.Combine(basePath, "appsettings.json"))
            AddJsonFileIfExists(builder, currentDirConfig);

        AddJsonFileIfExists(builder, Path.Combine(basePath, "appsettings.local.json"));

        var currentDirLocalConfig = Path.Combine(currentDir, "appsettings.local.json");
        if (currentDirLocalConfig != Path.Combine(basePath, "appsettings.local.json"))
            AddJsonFileIfExists(builder, currentDirLocalConfig);

        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            builder.AddJsonFile(configPath, optional: false);

        builder.AddEnvironmentVariables();
        return Bind(builder.Build());
    }

    private static void AddJsonFileIfExists(ConfigurationBuilder builder, string path)
    {
        if (File.Exists(path))
            builder.AddJsonFile(path, optional: true);
    }

    private static string? GetConfigValue(IConfiguration configuration, string configKey, string envKey)
    {
        var value = configuration[configKey];

        if (!string.IsNullOrEmpty(value) && value.StartsWith("env:"))
        {
            var envVarName = value.Substring(4);
            value = Environment.GetEnvironmentVariable(envVarName);
        }

        if (string.IsNullOrEmpty(value))
            value = Environment.GetEnvironmentVariable(envKey);

        return value;
    }

    private static void LoadEnvFile()
    {
        var envPaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(AppContext.BaseDirectory, ".env")
        };

        foreach (var envPath in envPaths)
        {
            if (!File.Exists(envPath))
                continue;

            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#'))
                    continue;

                var separatorIndex = trimmedLine.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                var key = trimmedLine.Substring(0, separatorIndex).Trim();
                var value = trimmedLine.Substring(separatorIndex + 1).Trim();

                if (value.StartsWith('"') && value.EndsWith('"'))
                    value = value.Substring(1, value.Length - 2);
                else if (value.StartsWith('\'') && value.EndsWith('\''))
                    value = value.Substring(1, value.Length - 2);

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    Environment.SetEnvironmentVariable(key, value);
            }

            break;
        }
    }
}
