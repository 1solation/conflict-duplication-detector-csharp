using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Api.Services;
using ConflictDuplicationDetector.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace ConflictDuplicationDetector.Api.Pages;

public class CheckModel(AppConfiguration config, IServiceProvider services, IJobService jobs)
    : DetectorPageModel(config, services)
{
    [BindProperty]
    public IFormFile? UploadFile { get; set; }

    [BindProperty]
    public string Type { get; set; } = "all";

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (UploadFile is null || UploadFile.Length == 0)
            ModelState.AddModelError(nameof(UploadFile), "Select a document to check.");

        var allowedTypes = new[] { "all", "duplications", "conflicts", "inconsistencies" };
        if (!allowedTypes.Contains(Type, StringComparer.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(Type), "Select a valid check type.");

        if (!ModelState.IsValid)
            return Page();

        if (!TryGetDetector(out var detector))
            return Page();

        try
        {
            var savedPath = await detector.SaveUploadAsync(UploadFile!, cancellationToken);
            var accepted = await jobs.EnqueueAsync(
                JobType.Check,
                async ct => await detector.CheckFileAsync(savedPath, Type, ct),
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
