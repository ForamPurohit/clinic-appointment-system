using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Models;
using ClinicAppointment.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.Tests.Services;

/// <summary>Covers UC-01 (register) and UC-02 (sign in).</summary>
public class AuthServiceTests
{
    private static RegisterPatientRequest Registration(string email = "new@patient.com") => new()
    {
        FullName = "  New Patient  ",
        Email = email,
        Password = "Patient@123",
        PhoneNumber = "9876543210",
        DateOfBirth = new DateOnly(2000, 5, 20),
        Gender = "Female"
    };

    // ======================= Register (UC-01) =======================

    [Fact]
    public async Task Register_NewEmail_CreatesPatientAndReturnsToken()
    {
        using var data = new TestData();

        var res = await data.Auth().RegisterPatientAsync(Registration());

        Assert.False(string.IsNullOrWhiteSpace(res.Token));
        Assert.Equal(Roles.Patient, res.User.Role);
        Assert.Equal("New Patient", res.User.FullName);

        await using var check = data.NewContext();
        var patient = await check.Patients.Include(p => p.User).SingleAsync(p => p.User.Email == "new@patient.com");
        Assert.Equal(new DateOnly(2000, 5, 20), patient.DateOfBirth);
        Assert.Equal("Female", patient.Gender);
    }

    [Fact]
    public async Task Register_StoresHashedPasswordNotPlainText()
    {
        using var data = new TestData();

        await data.Auth().RegisterPatientAsync(Registration());

        var user = await data.Db.Users.SingleAsync(u => u.Email == "new@patient.com");
        Assert.NotEqual("Patient@123", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Patient@123", user.PasswordHash));
    }

    [Fact]
    public async Task Register_NormalizesEmailToTrimmedLowercase()
    {
        using var data = new TestData();

        var res = await data.Auth().RegisterPatientAsync(Registration("  Mixed.Case@Mail.COM "));

        Assert.Equal("mixed.case@mail.com", res.User.Email);
    }

    [Fact]
    public async Task Register_ExistingEmailInDifferentCase_ThrowsConflict()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<ConflictException>(() => data.Auth().RegisterPatientAsync(Registration("P1@Test.com")));
    }

    // ======================= Sign in (UC-02) =======================

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsTokenAndUser()
    {
        using var data = new TestData();

        var res = await data.Auth().LoginAsync(new LoginRequest { Email = "p1@test.com", Password = TestData.Password });

        Assert.False(string.IsNullOrWhiteSpace(res.Token));
        Assert.Equal(data.Patient.UserId, res.User.Id);
        Assert.True(res.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        using var data = new TestData();

        var res = await data.Auth().LoginAsync(new LoginRequest { Email = " P1@TEST.com ", Password = TestData.Password });

        Assert.Equal("p1@test.com", res.User.Email);
    }

    [Theory]
    [InlineData(Roles.Admin, "admin@test.com")]
    [InlineData(Roles.Doctor, "dr@test.com")]
    [InlineData(Roles.Patient, "p2@test.com")]
    public async Task Login_EachRole_ReturnsMatchingRole(string role, string email)
    {
        using var data = new TestData();

        var res = await data.Auth().LoginAsync(new LoginRequest { Email = email, Password = TestData.Password });

        Assert.Equal(role, res.User.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        using var data = new TestData();

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            data.Auth().LoginAsync(new LoginRequest { Email = "p1@test.com", Password = "wrong" }));
        Assert.Equal("Email or password is incorrect.", ex.Message);
    }

    [Fact]
    public async Task Login_UnknownEmail_ThrowsSameMessageAsWrongPassword()
    {
        using var data = new TestData();

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            data.Auth().LoginAsync(new LoginRequest { Email = "nobody@test.com", Password = TestData.Password }));
        Assert.Equal("Email or password is incorrect.", ex.Message); // doesn't reveal which emails exist
    }

    [Fact]
    public async Task Login_InactiveDoctor_ThrowsForbidden()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            data.Auth().LoginAsync(new LoginRequest { Email = "retired@test.com", Password = TestData.Password }));
    }

    [Fact]
    public async Task GetUser_Existing_ReturnsProfile()
    {
        using var data = new TestData();

        var user = await data.Auth().GetUserAsync(data.Admin.Id);

        Assert.Equal(Roles.Admin, user.Role);
    }

    [Fact]
    public async Task GetUser_Missing_ThrowsNotFound()
    {
        using var data = new TestData();
        await Assert.ThrowsAsync<NotFoundException>(() => data.Auth().GetUserAsync(999));
    }
}
