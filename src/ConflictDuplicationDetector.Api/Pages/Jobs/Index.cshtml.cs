using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Api.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ConflictDuplicationDetector.Api.Pages.Jobs;

public class IndexModel(IJobService jobs) : PageModel
{
    public IReadOnlyList<JobStatusResponse> Jobs { get; private set; } = [];

    public void OnGet()
    {
        Jobs = jobs.ListJobs();
    }
}
