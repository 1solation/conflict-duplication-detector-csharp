using System.Diagnostics.CodeAnalysis;
using ConflictDuplicationDetector.Api.Services;
using ConflictDuplicationDetector.Core.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace ConflictDuplicationDetector.Api.Pages;

public abstract class DetectorPageModel(AppConfiguration config, IServiceProvider services) : PageModel
{
    protected AppConfiguration AppConfig { get; } = config;

    protected IServiceProvider Services { get; } = services;

    protected bool IsOpenAiConfigured => !string.IsNullOrWhiteSpace(AppConfig.OpenAI.ApiKey);

    protected bool TryGetDetector([NotNullWhen(true)] out DetectorApplicationService? detector)
    {
        if (!IsOpenAiConfigured)
        {
            detector = null;
            ModelState.AddModelError(string.Empty, "OpenAI API key not configured. Set OPENAI_API_KEY.");
            return false;
        }

        detector = Services.GetRequiredService<DetectorApplicationService>();
        return true;
    }
}
