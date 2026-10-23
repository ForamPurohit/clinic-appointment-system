using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicAppointment.API.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _service;

    public AdminController(IAdminService service)
    {
        _service = service;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> Stats() =>
        Ok(await _service.GetStatsAsync());

    [HttpGet("patients")]
    public async Task<ActionResult<IReadOnlyList<PatientDto>>> Patients([FromQuery] string? search) =>
        Ok(await _service.GetPatientsAsync(search));
}
