using System.Text.RegularExpressions;
using BelgiumVatChecker.Core.Exceptions;
using BelgiumVatChecker.Core.Interfaces;
using BelgiumVatChecker.Core.Models;

namespace BelgiumVatChecker.Core.Services;

public class VatValidationService : IVatValidationService
{
    private readonly IViesClient _viesClient;
    private static readonly Regex BelgianVatRegex = new(@"^(BE)?0?[0-9]{9}$", RegexOptions.IgnoreCase);

    public VatValidationService(IViesClient viesClient)
    {
        _viesClient = viesClient;
    }

    public async Task<VatValidationResponse> ValidateVatNumberAsync(VatValidationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CountryCode))
        {
            throw new ArgumentException("Country code is required");
        }

        if (string.IsNullOrWhiteSpace(request.VatNumber))
        {
            throw new ArgumentException("VAT number is required");
        }

        var countryCode = request.CountryCode.ToUpperInvariant();
        var vatNumber = CleanVatNumber(request.VatNumber, countryCode);

        if (countryCode == "BE")
        {
            if (!IsValidBelgianVatFormat(vatNumber))
            {
                return new VatValidationResponse
                {
                    IsValid = false,
                    CountryCode = countryCode,
                    VatNumber = vatNumber,
                    ErrorMessage = "Invalid Belgian VAT number format. Expected format: BE0123456789 (10 digits)"
                };
            }

            if (!IsValidBelgianVatChecksum(vatNumber))
            {
                return new VatValidationResponse
                {
                    IsValid = false,
                    CountryCode = countryCode,
                    VatNumber = vatNumber,
                    ErrorMessage = "Invalid Belgian VAT number checksum"
                };
            }
        }

        try
        {
            return await _viesClient.CheckVatAsync(countryCode, vatNumber);
        }
        catch (ViesServiceUnavailableException)
        {
            return new VatValidationResponse
            {
                IsValid = false,
                CountryCode = countryCode,
                VatNumber = vatNumber,
                ErrorMessage = "VIES service is currently unavailable. Please try again later."
            };
        }
        catch (Exception ex)
        {
            return new VatValidationResponse
            {
                IsValid = false,
                CountryCode = countryCode,
                VatNumber = vatNumber,
                ErrorMessage = $"Error validating VAT number: {ex.Message}"
            };
        }
    }

    public async Task<VatValidationResponse> ValidateBelgianVatNumberAsync(string vatNumber)
    {
        return await ValidateVatNumberAsync(new VatValidationRequest
        {
            CountryCode = "BE",
            VatNumber = vatNumber
        });
    }

    public async Task<ViesServiceStatus> CheckServiceStatusAsync()
    {
        var status = new ViesServiceStatus
        {
            CheckedAt = DateTime.UtcNow
        };

        try
        {
            status.IsAvailable = await _viesClient.IsServiceAvailableAsync();
            
            if (status.IsAvailable)
            {
                var euCountries = new[] { "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "EL", "ES", 
                    "FI", "FR", "HR", "HU", "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL", "PT", 
                    "RO", "SE", "SI", "SK" };

                foreach (var country in euCountries)
                {
                    status.CountryAvailability[country] = true;
                }
            }
        }
        catch
        {
            status.IsAvailable = false;
        }

        return status;
    }

    private string CleanVatNumber(string vatNumber, string countryCode)
    {
        vatNumber = vatNumber.Trim().ToUpperInvariant();
        
        // Remove country code prefix if present
        if (vatNumber.StartsWith(countryCode))
        {
            vatNumber = vatNumber.Substring(countryCode.Length);
        }

        // For Belgian VAT numbers, remove spaces and dashes but keep the format simple
        if (countryCode == "BE")
        {
            vatNumber = Regex.Replace(vatNumber, @"[^0-9]", "");
        }
        else
        {
            // For other countries, only allow characters that match VIES pattern: [0-9A-Za-z\+\*\.]{2,12}
            vatNumber = Regex.Replace(vatNumber, @"[^0-9A-Za-z\+\*\.]", "");
        }

        return vatNumber;
    }

    private bool IsValidBelgianVatFormat(string vatNumber)
    {
        vatNumber = vatNumber.TrimStart('0');
        return vatNumber.Length == 9 && vatNumber.All(char.IsDigit);
    }

    private bool IsValidBelgianVatChecksum(string vatNumber)
    {
        vatNumber = vatNumber.TrimStart('0');
        
        if (vatNumber.Length != 9)
            return false;

        var checkDigits = int.Parse(vatNumber.Substring(7, 2));
        var numberToCheck = long.Parse(vatNumber.Substring(0, 7));
        
        var remainder = 97 - (numberToCheck % 97);
        
        return remainder == checkDigits;
    }
}