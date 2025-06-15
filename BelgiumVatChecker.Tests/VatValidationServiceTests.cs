using BelgiumVatChecker.Core.Interfaces;
using BelgiumVatChecker.Core.Models;
using BelgiumVatChecker.Core.Services;
using BelgiumVatChecker.Core.Exceptions;
using FakeItEasy;
using Shouldly;

namespace BelgiumVatChecker.Tests;

public class VatValidationServiceTests
{
    private readonly IViesClient _viesClient;
    private readonly VatValidationService _service;

    public VatValidationServiceTests()
    {
        _viesClient = A.Fake<IViesClient>();
        _service = new VatValidationService(_viesClient);
    }

    [Fact]
    public async Task ValidateVatNumberAsync_ShouldThrowArgumentException_WhenCountryCodeIsEmpty()
    {
        var request = new VatValidationRequest { CountryCode = "", VatNumber = "0123456789" };

        var exception = await Should.ThrowAsync<ArgumentException>(() => _service.ValidateVatNumberAsync(request));
        exception.Message.ShouldContain("Country code is required");
    }

    [Fact]
    public async Task ValidateVatNumberAsync_ShouldThrowArgumentException_WhenVatNumberIsEmpty()
    {
        var request = new VatValidationRequest { CountryCode = "BE", VatNumber = "" };

        var exception = await Should.ThrowAsync<ArgumentException>(() => _service.ValidateVatNumberAsync(request));
        exception.Message.ShouldContain("VAT number is required");
    }

    [Theory]
    [InlineData("BE0477472701", true)] // Valid Belgian VAT
    [InlineData("BE0123456789", false)] // Invalid checksum
    [InlineData("BE477472701", true)] // Valid without leading zero
    public async Task ValidateBelgianVatNumberAsync_ShouldValidateChecksum(string vatNumber, bool expectedValid)
    {
        A.CallTo(() => _viesClient.CheckVatAsync("BE", A<string>._))
            .Returns(new VatValidationResponse { IsValid = expectedValid });

        var result = await _service.ValidateBelgianVatNumberAsync(vatNumber);

        if (!expectedValid && vatNumber == "BE0123456789")
        {
            result.IsValid.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("Invalid Belgian VAT number checksum");
        }
        else
        {
            result.IsValid.ShouldBe(expectedValid);
        }
    }

    [Theory]
    [InlineData("BE12345")]
    [InlineData("BE12345678901")]
    [InlineData("BEABC123456")]
    public async Task ValidateBelgianVatNumberAsync_ShouldReturnInvalid_ForInvalidFormat(string vatNumber)
    {
        var result = await _service.ValidateBelgianVatNumberAsync(vatNumber);

        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Invalid Belgian VAT number format");
    }

    [Fact]
    public async Task ValidateVatNumberAsync_ShouldHandleViesServiceUnavailable()
    {
        var request = new VatValidationRequest { CountryCode = "BE", VatNumber = "0477472701" };
        
        A.CallTo(() => _viesClient.CheckVatAsync(A<string>._, A<string>._))
            .Throws(new ViesServiceUnavailableException());

        var result = await _service.ValidateVatNumberAsync(request);

        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("VIES service is currently unavailable");
    }

    [Fact]
    public async Task CheckServiceStatusAsync_ShouldReturnAvailable_WhenServiceIsUp()
    {
        A.CallTo(() => _viesClient.IsServiceAvailableAsync()).Returns(true);

        var status = await _service.CheckServiceStatusAsync();

        status.IsAvailable.ShouldBeTrue();
        status.CountryAvailability.ShouldContainKey("BE");
        status.CountryAvailability["BE"].ShouldBeTrue();
    }

    [Fact]
    public async Task CheckServiceStatusAsync_ShouldReturnUnavailable_WhenServiceIsDown()
    {
        A.CallTo(() => _viesClient.IsServiceAvailableAsync()).Returns(false);

        var status = await _service.CheckServiceStatusAsync();

        status.IsAvailable.ShouldBeFalse();
        status.CountryAvailability.ShouldBeEmpty();
    }
}