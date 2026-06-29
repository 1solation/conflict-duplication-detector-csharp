using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Api.Services;
using ConflictDuplicationDetector.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace ConflictDuplicationDetector.Api.Pages;

public class ChatModel(AppConfiguration config, IServiceProvider services, IJobService jobs)
    : DetectorPageModel(config, services)
{
    [BindProperty]
    public string Message { get; set; } = string.Empty;

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Message))
            ModelState.AddModelError(nameof(Message), "Enter a question.");

        if (!ModelState.IsValid)
            return Page();

        if (!TryGetDetector(out var detector))
            return Page();

        try
        {
            var accepted = await jobs.EnqueueAsync(
                JobType.Chat,
                async ct => await detector.ChatAsync(Message, ct),
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
