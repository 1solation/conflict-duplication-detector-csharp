using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ConflictDuplicationDetector.Api.Pages.Jobs;

public class DetailsModel(IJobService jobs) : PageModel
{
    public JobStatusResponse? Job { get; private set; }

    public bool ShouldRefresh => Job?.Status is JobStatus.Pending or JobStatus.Running;

    public IActionResult OnGet(Guid id)
    {
        Job = jobs.GetJob(id);
        return Job is null ? NotFound() : Page();
    }
}
