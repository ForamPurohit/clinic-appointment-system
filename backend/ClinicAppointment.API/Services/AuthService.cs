using ClinicAppointment.API.Data;
using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.API.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterPatientAsync(RegisterPatientRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<UserDto> GetUserAsync(int userId);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;

    public AuthService(AppDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> RegisterPatientAsync(RegisterPatientRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var patient = new Patient
        {
            User = new User
            {
                FullName = request.FullName.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = Roles.Patient,
                PhoneNumber = request.PhoneNumber
            },
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender
        };

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        return BuildResponse(patient.User);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Email or password is incorrect.");
        }

        if (user.Role == Roles.Doctor)
        {
            var isActive = await _db.Doctors.AnyAsync(d => d.UserId == user.Id && d.IsActive);
            if (!isActive)
            {
                throw new ForbiddenException("This doctor account is inactive. Contact the clinic admin.");
            }
        }

        return BuildResponse(user);
    }

    public async Task<UserDto> GetUserAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId)
                   ?? throw new NotFoundException("User not found.");
        return ToDto(user);
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = _tokenService.CreateToken(user);
        return new AuthResponse(token, expiresAt, ToDto(user));
    }

    private static UserDto ToDto(User u) => new(u.Id, u.FullName, u.Email, u.Role, u.PhoneNumber);
}
