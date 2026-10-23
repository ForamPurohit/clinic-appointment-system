using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Extensions;
using ClinicAppointment.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicAppointment.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Create a patient account.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterPatientRequest request) =>
        Ok(await _authService.RegisterPatientAsync(request));

    /// <summary>Sign in as admin, doctor or patient.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request) =>
        Ok(await _authService.LoginAsync(request));

    /// <summary>The signed-in user's profile.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me() =>
        Ok(await _authService.GetUserAsync(User.GetUserId()));
}
