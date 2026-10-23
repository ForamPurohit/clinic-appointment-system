namespace ClinicAppointment.API.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(string message, int statusCode = StatusCodes.Status400BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, StatusCodes.Status404NotFound) { }
}

public class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message, StatusCodes.Status400BadRequest) { }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message, StatusCodes.Status409Conflict) { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message, StatusCodes.Status403Forbidden) { }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message, StatusCodes.Status401Unauthorized) { }
}
