using api.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>docs/07-pages.md `/admin/loglar`: mevcut log altyapısının filtrelenebilir okuma ekranı.</summary>
[ApiController]
[AdminAuth]
[Route("api/admin/logs")]
public class AdminLogsController : ControllerBase
{
    private readonly InMemoryLogStore _store;

    public AdminLogsController(InMemoryLogStore store)
    {
        _store = store;
    }

    [HttpGet]
    public ActionResult<List<LogEntry>> Get([FromQuery] string? level, [FromQuery] string? search)
    {
        return Ok(_store.GetRecent(level, search));
    }
}
