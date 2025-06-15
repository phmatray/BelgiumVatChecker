using Microsoft.AspNetCore.Mvc;
using BelgiumVatChecker.Core.Interfaces;
using BelgiumVatChecker.Core.Models;
using BelgiumVatChecker.Core.Exceptions;

namespace BelgiumVatChecker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VatController : ControllerBase
{
    private readonly IVatValidationService _vatValidationService;
    private readonly ILogger<VatController> _logger;

    public VatController(IVatValidationService vatValidationService, ILogger<VatController> logger)
    {
        _vatValidationService = vatValidationService;
        _logger = logger;
    }

    [HttpPost("validate")]
    public async Task<ActionResult<VatValidationResponse>> ValidateVat([FromBody] VatValidationRequest request)
    {
        try
        {
            var result = await _vatValidationService.ValidateVatNumberAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request parameters");
            return BadRequest(new { error = ex.Message });
        }
        catch (VatValidationException ex)
        {
            _logger.LogError(ex, "VAT validation error");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("validate/belgium/{vatNumber}")]
    public async Task<ActionResult<VatValidationResponse>> ValidateBelgianVat(string vatNumber)
    {
        try
        {
            var result = await _vatValidationService.ValidateBelgianVatNumberAsync(vatNumber);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid VAT number format");
            return BadRequest(new { error = ex.Message });
        }
        catch (VatValidationException ex)
        {
            _logger.LogError(ex, "VAT validation error");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("status")]
    public async Task<ActionResult<ViesServiceStatus>> GetServiceStatus()
    {
        try
        {
            var status = await _vatValidationService.CheckServiceStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking service status");
            return StatusCode(500, new { error = "Unable to check service status" });
        }
    }
}