using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Extensions;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicAppointment.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorsController : ControllerBase
{
    private readonly IDoctorService _service;
    private readonly ILeaveService _leaves;
    private readonly IReviewService _reviews;

    public DoctorsController(IDoctorService service, ILeaveService leaves, IReviewService reviews)
    {
        _service = service;
        _leaves = leaves;
        _reviews = reviews;
    }

    /// <summary>List doctors. Admins can pass includeInactive=true.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DoctorDto>>> GetAll(
        [FromQuery] int? specializationId, [FromQuery] bool includeInactive = false)
    {
        var showInactive = includeInactive && User.IsInRole(Roles.Admin);
        return Ok(await _service.GetAllAsync(specializationId, showInactive));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DoctorDto>> GetById(int id) =>
        Ok(await _service.GetByIdAsync(id));

    /// <summary>Time slots for a doctor on a date (yyyy-MM-dd).</summary>
    [HttpGet("{id:int}/slots")]
    public async Task<ActionResult<IReadOnlyList<TimeSlotDto>>> GetSlots(int id, [FromQuery] DateOnly date) =>
        Ok(await _service.GetSlotsAsync(id, date));

    /// <summary>Current and upcoming leave, so the booking page can disable those days.</summary>
    [HttpGet("{id:int}/leaves")]
    public async Task<ActionResult<IReadOnlyList<LeaveDto>>> GetLeaves(int id) =>
        Ok(await _leaves.GetUpcomingAsync(id));

    /// <summary>Average rating and latest reviews.</summary>
    [HttpGet("{id:int}/reviews")]
    public async Task<ActionResult<DoctorReviewsDto>> GetReviews(int id, [FromQuery] int take = 20) =>
        Ok(await _reviews.GetForDoctorAsync(id, take));

    [Authorize(Roles = Roles.Doctor)]
    [HttpGet("me/leaves")]
    public async Task<ActionResult<IReadOnlyList<LeaveDto>>> MyLeaves() =>
        Ok(await _leaves.GetMineAsync(User.GetUserId()));

    [Authorize(Roles = Roles.Doctor)]
    [HttpPost("me/leaves")]
    public async Task<ActionResult<LeaveDto>> AddLeave(CreateLeaveRequest request) =>
        Ok(await _leaves.AddAsync(User.GetUserId(), request));

    [Authorize(Roles = Roles.Doctor)]
    [HttpDelete("me/leaves/{leaveId:int}")]
    public async Task<IActionResult> DeleteLeave(int leaveId)
    {
        await _leaves.DeleteAsync(User.GetUserId(), leaveId);
        return NoContent();
    }

    /// <summary>The signed-in doctor's own profile.</summary>
    [Authorize(Roles = Roles.Doctor)]
    [HttpGet("me")]
    public async Task<ActionResult<DoctorDto>> Me() =>
        Ok(await _service.GetByUserIdAsync(User.GetUserId()));

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<DoctorDto>> Create(CreateDoctorRequest request)
    {
        var created = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<DoctorDto>> Update(int id, UpdateDoctorRequest request) =>
        Ok(await _service.UpdateAsync(id, request));
}
