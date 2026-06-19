using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ConflictDuplicationDetector.Api.Pages;

public class ErrorModel : PageModel
{
    public string? RequestId { get; private set; }

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}
