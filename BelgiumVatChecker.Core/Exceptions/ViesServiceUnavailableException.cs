namespace BelgiumVatChecker.Core.Exceptions;

public class ViesServiceUnavailableException : Exception
{
    public ViesServiceUnavailableException() : base("VIES service is currently unavailable")
    {
    }

    public ViesServiceUnavailableException(string message) : base(message)
    {
    }

    public ViesServiceUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}