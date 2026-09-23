using HillApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace HillApp.Pages;

public class IndexModel : PageModel
{
    private readonly UserSessionService _sessions;

    public IndexModel(UserSessionService sessions)
    {
        _sessions = sessions;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            return await _sessions.ValidateAndTouchAsync(HttpContext) is null
                ? RedirectToPage("/Login")
                : RedirectToPage("/Records");
        }
        catch (MySqlException)
        {
            return RedirectToPage("/Login");
        }
    }
}
