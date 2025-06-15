using BelgiumVatChecker.Core.Models;

namespace BelgiumVatChecker.Core.Interfaces;

public interface IVatValidationService
{
    Task<VatValidationResponse> ValidateVatNumberAsync(VatValidationRequest request);
    Task<VatValidationResponse> ValidateBelgianVatNumberAsync(string vatNumber);
    Task<ViesServiceStatus> CheckServiceStatusAsync();
}