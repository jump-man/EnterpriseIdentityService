namespace EnterpriseIdentityService.Application.Abstractions.Mailing;

public sealed class EmailDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
