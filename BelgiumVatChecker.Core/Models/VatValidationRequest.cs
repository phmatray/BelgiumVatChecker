namespace BelgiumVatChecker.Core.Models;

public class VatValidationRequest
{
    public string CountryCode { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
}