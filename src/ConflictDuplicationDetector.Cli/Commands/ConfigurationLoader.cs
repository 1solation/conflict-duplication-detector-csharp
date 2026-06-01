using ConflictDuplicationDetector.Core.Models;

namespace ConflictDuplicationDetector.Cli.Commands;

public static class ConfigurationLoader
{
    public static AppConfiguration Load(string? configPath = null) =>
        Core.Configuration.ConfigurationLoader.Load(configPath);
}
