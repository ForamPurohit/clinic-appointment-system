using System.Text.Json;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClinicAppointment.Tests.Middleware;

public class ExceptionMiddlewareTests
{
    private static async Task<(int Status, JsonElement Body)> RunAsync(RequestDelegate next)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var body = text.Length > 0 ? JsonDocument.Parse(text).RootElement : default;
        return (context.Response.StatusCode, body);
    }

    public static TheoryData<AppException, int> KnownErrors => new()
    {
        { new BadRequestException("bad"), 400 },
        { new UnauthorizedException("who?"), 401 },
        { new ForbiddenException("no"), 403 },
        { new NotFoundException("gone"), 404 },
        { new ConflictException("taken"), 409 }
    };

    [Theory]
    [MemberData(nameof(KnownErrors))]
    public async Task AppException_ReturnsItsStatusAndMessage(AppException exception, int expectedStatus)
    {
        var (status, body) = await RunAsync(_ => throw exception);

        Assert.Equal(expectedStatus, status);
        Assert.Equal(expectedStatus, body.GetProperty("status").GetInt32());
        Assert.Equal(exception.Message, body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UnexpectedException_Returns500WithoutLeakingDetails()
    {
        var (status, body) = await RunAsync(_ => throw new InvalidOperationException("connection string secret"));

        Assert.Equal(500, status);
        Assert.DoesNotContain("secret", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task NoException_LeavesResponseUntouched()
    {
        var (status, _) = await RunAsync(ctx =>
        {
            ctx.Response.StatusCode = 204;
            return Task.CompletedTask;
        });

        Assert.Equal(204, status);
    }
}
