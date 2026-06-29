using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Api.Services;
using ConflictDuplicationDetector.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace ConflictDuplicationDetector.Api.Pages;

public class AnalyseModel(AppConfiguration config, IServiceProvider services, IJobService jobs)
    : DetectorPageModel(config, services)
{
    [BindProperty]
    public string Type { get; set; } = "all";

    [BindProperty]
    public string? Topic { get; set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var allowedTypes = new[] { "all", "duplications", "conflicts", "inconsistencies" };
        if (!allowedTypes.Contains(Type, StringComparer.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(Type), "Select a valid analysis type.");

        if (!ModelState.IsValid)
            return Page();

        if (!TryGetDetector(out var detector))
            return Page();

        try
        {
            var accepted = await jobs.EnqueueAsync(
                JobType.Analysis,
                async ct => await detector.RunAnalysisAsync(Type, Topic, ct),
                cancellationToken);

            return RedirectToPage("/Jobs/Details", new { id = accepted.JobId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }
}
