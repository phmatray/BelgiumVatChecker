namespace BelgiumVatChecker.Core.Models;

public class VatValidationResponse
{
    public bool IsValid { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Address { get; set; }
    public DateTime? RequestDate { get; set; }
    public string? ErrorMessage { get; set; }
}