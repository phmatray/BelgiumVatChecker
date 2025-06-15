namespace BelgiumVatChecker.Core.Models;

public class ViesServiceStatus
{
    public bool IsAvailable { get; set; }
    public Dictionary<string, bool> CountryAvailability { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}