using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly ReportExportService _reports;

    public ReportsController(ReportExportService reports)
    {
        _reports = reports;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);

        return Ok(await _reports.BuildSummaryAsync(userId, role));
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf()
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var bytes = await _reports.BuildPdfAsync(userId, role);

        return File(bytes, "application/pdf", $"helpdesk-report-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf");
    }

    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportExcel()
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var bytes = await _reports.BuildExcelAsync(userId, role);

        return File(bytes, "application/vnd.ms-excel", $"helpdesk-report-{DateTime.UtcNow:yyyyMMdd-HHmm}.xls");
    }
}
