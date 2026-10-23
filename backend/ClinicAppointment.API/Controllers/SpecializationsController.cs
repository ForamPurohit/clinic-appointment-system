using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicAppointment.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpecializationsController : ControllerBase
{
    private readonly ISpecializationService _service;

    public SpecializationsController(ISpecializationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SpecializationDto>>> GetAll() =>
        Ok(await _service.GetAllAsync());

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<SpecializationDto>> Create(SpecializationRequest request)
    {
        var created = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<SpecializationDto>> Update(int id, SpecializationRequest request) =>
        Ok(await _service.UpdateAsync(id, request));

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
