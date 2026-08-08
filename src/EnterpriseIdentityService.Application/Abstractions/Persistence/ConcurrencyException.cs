namespace EnterpriseIdentityService.Application.Abstractions.Persistence;

public sealed class ConcurrencyException(string message, Exception? innerException = null)
    : Exception(message, innerException);
