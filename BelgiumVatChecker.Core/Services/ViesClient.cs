using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;
using BelgiumVatChecker.Core.Exceptions;
using BelgiumVatChecker.Core.Interfaces;
using BelgiumVatChecker.Core.Models;

namespace BelgiumVatChecker.Core.Services;

public class ViesClient : IViesClient
{
    private const string ViesServiceUrl = "https://ec.europa.eu/taxation_customs/vies/services/checkVatService";
    private readonly HttpClient _httpClient;

    public ViesClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<VatValidationResponse> CheckVatAsync(string countryCode, string vatNumber)
    {
        try
        {
            // Validate input according to WSDL patterns
            if (!System.Text.RegularExpressions.Regex.IsMatch(countryCode, @"^[A-Z]{2}$"))
            {
                throw new ArgumentException("Country code must be exactly 2 uppercase letters");
            }
            
            // Clean the VAT number: remove spaces, dashes, dots, and country prefix if present
            vatNumber = vatNumber.Trim().ToUpperInvariant();
            vatNumber = System.Text.RegularExpressions.Regex.Replace(vatNumber, @"[\s\-\.]", "");
            
            // Remove country code prefix if present
            if (vatNumber.StartsWith(countryCode))
            {
                vatNumber = vatNumber.Substring(countryCode.Length);
            }
            
            // Now validate the cleaned VAT number
            if (!System.Text.RegularExpressions.Regex.IsMatch(vatNumber, @"^[0-9A-Za-z\+\*\.]{2,12}$"))
            {
                throw new ArgumentException("VAT number must be 2-12 characters containing only alphanumeric characters, +, *, or .");
            }

            var soapEnvelope = BuildSoapEnvelope(countryCode, vatNumber);
            
            var request = new HttpRequestMessage(HttpMethod.Post, ViesServiceUrl)
            {
                Content = new StringContent(soapEnvelope, System.Text.Encoding.UTF8, "text/xml")
            };
            request.Headers.Add("SOAPAction", "");

            var response = await _httpClient.SendAsync(request);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Check for SOAP fault
            if (responseContent.Contains("soap:Fault") || responseContent.Contains("faultstring"))
            {
                HandleSoapFault(responseContent);
            }
            
            response.EnsureSuccessStatusCode();
            
            return ParseSoapResponse(responseContent, countryCode, vatNumber);
        }
        catch (HttpRequestException ex)
        {
            throw new ViesServiceUnavailableException($"Unable to connect to VIES service: {ex.Message}", ex);
        }
        catch (ViesServiceUnavailableException)
        {
            throw; // Re-throw our custom exceptions
        }
        catch (VatValidationException)
        {
            throw; // Re-throw our custom exceptions
        }
        catch (ArgumentException)
        {
            throw; // Re-throw argument exceptions
        }
        catch (Exception ex)
        {
            throw new VatValidationException($"Error validating VAT number: {ex.Message}", ex);
        }
    }

    public async Task<bool> IsServiceAvailableAsync()
    {
        try
        {
            // Test with a known valid EU VAT number (use a test number that should always exist)
            // Using BE as it's always available and 0477472701 as a test number
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            
            var soapEnvelope = BuildSoapEnvelope("BE", "0477472701");
            
            var request = new HttpRequestMessage(HttpMethod.Post, ViesServiceUrl)
            {
                Content = new StringContent(soapEnvelope, System.Text.Encoding.UTF8, "text/xml")
            };
            request.Headers.Add("SOAPAction", "");

            var response = await _httpClient.SendAsync(request, cts.Token);
            
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Check if it's a SOAP fault indicating service issues
            if (responseContent.Contains("soap:Fault") || responseContent.Contains("faultstring"))
            {
                // Check if it's a service unavailability fault
                if (responseContent.Contains("SERVICE_UNAVAILABLE") || 
                    responseContent.Contains("MS_UNAVAILABLE") || 
                    responseContent.Contains("TIMEOUT"))
                {
                    return false;
                }
            }
            
            // If we get here, the service responded (even if the VAT is invalid)
            return true;
        }
        catch
        {
            // Any exception means the service is not available
            return false;
        }
    }

    private string BuildSoapEnvelope(string countryCode, string vatNumber)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" 
               xmlns:urn=""urn:ec.europa.eu:taxud:vies:services:checkVat:types"">
    <soap:Body>
        <urn:checkVat>
            <urn:countryCode>{countryCode}</urn:countryCode>
            <urn:vatNumber>{vatNumber}</urn:vatNumber>
        </urn:checkVat>
    </soap:Body>
</soap:Envelope>";
    }

    private VatValidationResponse ParseSoapResponse(string soapResponse, string countryCode, string vatNumber)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var namespaceManager = new XmlNamespaceManager(doc.NameTable);
        namespaceManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
        namespaceManager.AddNamespace("ns2", "urn:ec.europa.eu:taxud:vies:services:checkVat:types");

        var validNode = doc.SelectSingleNode("//ns2:valid", namespaceManager);
        var nameNode = doc.SelectSingleNode("//ns2:name", namespaceManager);
        var addressNode = doc.SelectSingleNode("//ns2:address", namespaceManager);
        var requestDateNode = doc.SelectSingleNode("//ns2:requestDate", namespaceManager);

        var response = new VatValidationResponse
        {
            CountryCode = countryCode,
            VatNumber = vatNumber,
            IsValid = bool.Parse(validNode?.InnerText ?? "false"),
            Name = nameNode?.InnerText,
            Address = addressNode?.InnerText
        };

        if (DateTime.TryParse(requestDateNode?.InnerText, out var requestDate))
        {
            response.RequestDate = requestDate;
        }

        return response;
    }

    private void HandleSoapFault(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var namespaceManager = new XmlNamespaceManager(doc.NameTable);
        namespaceManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");

        var faultStringNode = doc.SelectSingleNode("//soap:Fault/faultstring", namespaceManager) 
                           ?? doc.SelectSingleNode("//faultstring", namespaceManager);
        
        if (faultStringNode != null)
        {
            var faultString = faultStringNode.InnerText;
            
            // Handle specific fault strings according to WSDL documentation
            if (faultString.Contains("INVALID_INPUT"))
            {
                throw new VatValidationException("Invalid input: The provided CountryCode is invalid or the VAT number is empty");
            }
            else if (faultString.Contains("GLOBAL_MAX_CONCURRENT_REQ"))
            {
                throw new ViesServiceUnavailableException("Your request cannot be processed due to high traffic on the web application. Please try again later.");
            }
            else if (faultString.Contains("MS_MAX_CONCURRENT_REQ"))
            {
                throw new ViesServiceUnavailableException("Your request cannot be processed due to high traffic towards the Member State you are trying to reach. Please try again later.");
            }
            else if (faultString.Contains("SERVICE_UNAVAILABLE"))
            {
                throw new ViesServiceUnavailableException("VIES service is temporarily unavailable. An error was encountered either at the network level or the Web application level. Please try again later.");
            }
            else if (faultString.Contains("MS_UNAVAILABLE"))
            {
                throw new ViesServiceUnavailableException("The application at the Member State is not replying or not available. Please try again later.");
            }
            else if (faultString.Contains("TIMEOUT"))
            {
                throw new ViesServiceUnavailableException("The application did not receive a reply within the allocated time period. Please try again later.");
            }
            else
            {
                throw new VatValidationException($"VIES service returned an error: {faultString}");
            }
        }
    }
}