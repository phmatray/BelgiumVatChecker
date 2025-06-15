namespace BelgiumVatChecker.Core.Exceptions;

public class VatValidationException : Exception
{
    public string? VatNumber { get; }
    public string? CountryCode { get; }

    public VatValidationException(string message) : base(message)
    {
    }

    public VatValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public VatValidationException(string message, string vatNumber, string countryCode) : base(message)
    {
        VatNumber = vatNumber;
        CountryCode = countryCode;
    }
}