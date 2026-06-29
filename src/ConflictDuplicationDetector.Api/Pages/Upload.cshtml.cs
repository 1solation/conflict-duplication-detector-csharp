using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Api.Services;
using ConflictDuplicationDetector.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace ConflictDuplicationDetector.Api.Pages;

public class UploadModel(AppConfiguration config, IServiceProvider services, IJobService jobs)
    : DetectorPageModel(config, services)
{
    [BindProperty]
    public List<IFormFile> Files { get; set; } = [];

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Files.Count == 0 || Files.All(file => file.Length == 0))
            ModelState.AddModelError(nameof(Files), "Select at least one document to upload.");

        if (!ModelState.IsValid)
            return Page();

        if (!TryGetDetector(out var detector))
            return Page();

        try
        {
            var formFiles = new FormFileCollection();
            foreach (var file in Files.Where(file => file.Length > 0))
                formFiles.Add(file);

            var savedPaths = await detector.SaveUploadsAsync(formFiles, cancellationToken);
            var accepted = await jobs.EnqueueAsync(
                JobType.Ingest,
                async ct => await detector.IngestFilesAsync(savedPaths, ct),
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
