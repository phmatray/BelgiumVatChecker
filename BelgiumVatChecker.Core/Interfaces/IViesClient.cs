using BelgiumVatChecker.Core.Models;

namespace BelgiumVatChecker.Core.Interfaces;

public interface IViesClient
{
    Task<VatValidationResponse> CheckVatAsync(string countryCode, string vatNumber);
    Task<bool> IsServiceAvailableAsync();
}