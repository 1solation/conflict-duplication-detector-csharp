using ConflictDuplicationDetector.Core.Models;

namespace ConflictDuplicationDetector.Cli.Commands;

public static class ConfigurationLoader
{
    public static AppConfiguration Load(string? configPath = null, string? provider = null)
    {
        var config = Core.Configuration.ConfigurationLoader.Load(configPath);
        
        if (!string.IsNullOrEmpty(provider))
        {
            if (Enum.TryParse<AIProvider>(provider.Replace(" ", ""), ignoreCase: true, out var aiProvider))
            {
                config.OpenAI.Provider = aiProvider;
            }
            else
            {
                throw new ArgumentException($"Invalid provider: '{provider}'. Valid values are: OpenAI, AzureOpenAI");
            }
        }
        
        return config;
    }
}
