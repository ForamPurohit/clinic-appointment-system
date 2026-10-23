using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Services;
using Microsoft.Extensions.Options;
using ClinicAppointment.Tests.Helpers;
using Microsoft.IdentityModel.Tokens;

namespace ClinicAppointment.Tests.Services;

/// <summary>Covers UC-02 (sign in): the JWT the API hands out.</summary>
public class TokenServiceTests
{
    private static readonly User SampleUser = new()
    {
        Id = 42,
        FullName = "Pat One",
        Email = "p1@test.com",
        Role = Roles.Patient
    };

    private static TokenService Service() => new(Options.Create(TestData.Jwt));

    private static ClaimsPrincipal Validate(string token, string key)
    {
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = TestData.Jwt.Issuer,
            ValidAudience = TestData.Jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true
        };
        return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
    }

    [Fact]
    public void CreateToken_ContainsIdEmailNameAndRole()
    {
        var (token, _) = Service().CreateToken(SampleUser);

        var principal = Validate(token, TestData.Jwt.Key);

        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("p1@test.com", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("Pat One", principal.FindFirstValue(ClaimTypes.Name));
        Assert.True(principal.IsInRole(Roles.Patient));
    }

    [Fact]
    public void CreateToken_UsesConfiguredIssuerAudienceAndExpiry()
    {
        var before = DateTime.UtcNow;

        var (token, expiresAt) = Service().CreateToken(SampleUser);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(TestData.Jwt.Issuer, jwt.Issuer);
        Assert.Contains(TestData.Jwt.Audience, jwt.Audiences);
        Assert.InRange(expiresAt, before.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
    }

    [Fact]
    public void CreateToken_ValidatedWithDifferentKey_IsRejected()
    {
        var (token, _) = Service().CreateToken(SampleUser);

        Assert.ThrowsAny<SecurityTokenException>(() =>
            Validate(token, "a-completely-different-signing-key-of-sufficient-length"));
    }
}
