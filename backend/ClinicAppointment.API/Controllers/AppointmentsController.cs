using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Extensions;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicAppointment.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _service;
    private readonly IReviewService _reviews;

    public AppointmentsController(IAppointmentService service, IReviewService reviews)
    {
        _service = service;
        _reviews = reviews;
    }

    /// <summary>Book an appointment (patients only).</summary>
    [Authorize(Roles = Roles.Patient)]
    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Book(CreateAppointmentRequest request)
    {
        var created = await _service.BookAsync(User.GetUserId(), request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>The signed-in patient's appointments.</summary>
    [Authorize(Roles = Roles.Patient)]
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> Mine() =>
        Ok(await _service.GetForPatientAsync(User.GetUserId()));

    /// <summary>The signed-in doctor's schedule, optionally for one date.</summary>
    [Authorize(Roles = Roles.Doctor)]
    [HttpGet("schedule")]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> Schedule([FromQuery] DateOnly? date) =>
        Ok(await _service.GetForDoctorAsync(User.GetUserId(), date));

    /// <summary>All appointments with optional filters (admin only).</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> GetAll([FromQuery] AppointmentFilter filter) =>
        Ok(await _service.GetAllAsync(filter));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AppointmentDto>> GetById(int id) =>
        Ok(await _service.GetByIdAsync(id, User.GetUserId(), User.GetRole()));

    /// <summary>Move an upcoming appointment to another open slot with the same doctor (patient or admin).</summary>
    [Authorize(Roles = $"{Roles.Patient},{Roles.Admin}")]
    [HttpPatch("{id:int}/reschedule")]
    public async Task<ActionResult<AppointmentDto>> Reschedule(int id, RescheduleAppointmentRequest request) =>
        Ok(await _service.RescheduleAsync(id, User.GetUserId(), User.GetRole(), request));

    /// <summary>Rate a completed visit (patient only, once per appointment).</summary>
    [Authorize(Roles = Roles.Patient)]
    [HttpPost("{id:int}/review")]
    public async Task<ActionResult<ReviewDto>> AddReview(int id, CreateReviewRequest request) =>
        Ok(await _reviews.CreateAsync(User.GetUserId(), id, request));

    [HttpPatch("{id:int}/cancel")]
    public async Task<ActionResult<AppointmentDto>> Cancel(int id) =>
        Ok(await _service.CancelAsync(id, User.GetUserId(), User.GetRole()));

    /// <summary>Mark an appointment Completed, Cancelled or NoShow (doctor or admin).</summary>
    [Authorize(Roles = $"{Roles.Doctor},{Roles.Admin}")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<AppointmentDto>> UpdateStatus(int id, UpdateAppointmentStatusRequest request) =>
        Ok(await _service.UpdateStatusAsync(id, User.GetUserId(), User.GetRole(), request));
}
