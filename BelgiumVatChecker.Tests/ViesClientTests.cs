using System.Net;
using BelgiumVatChecker.Core.Exceptions;
using BelgiumVatChecker.Core.Services;
using FakeItEasy;
using Shouldly;

namespace BelgiumVatChecker.Tests;

public class ViesClientTests
{
    [Theory]
    [InlineData("BE", "BE0744517956", "0744517956")] // With country prefix
    [InlineData("BE", "0744517956", "0744517956")] // Without country prefix
    [InlineData("BE", "BE 0744.517.956", "0744517956")] // With formatting
    [InlineData("BE", "0744-517-956", "0744517956")] // With dashes
    public async Task CheckVatAsync_ShouldCleanAndAcceptValidVatNumbers(string countryCode, string inputVatNumber, string expectedCleanedVatNumber)
    {
        var httpMessageHandler = A.Fake<HttpMessageHandler>();
        var httpClient = new HttpClient(httpMessageHandler);
        var client = new ViesClient(httpClient);

        var responseContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <ns2:checkVatResponse xmlns:ns2=""urn:ec.europa.eu:taxud:vies:services:checkVat:types"">
            <ns2:countryCode>{countryCode}</ns2:countryCode>
            <ns2:vatNumber>{expectedCleanedVatNumber}</ns2:vatNumber>
            <ns2:requestDate>2023-01-01</ns2:requestDate>
            <ns2:valid>true</ns2:valid>
        </ns2:checkVatResponse>
    </soap:Body>
</soap:Envelope>";

        A.CallTo(httpMessageHandler)
            .Where(x => x.Method.Name == "SendAsync")
            .WithReturnType<Task<HttpResponseMessage>>()
            .Returns(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            });

        var result = await client.CheckVatAsync(countryCode, inputVatNumber);

        result.IsValid.ShouldBeTrue();
        result.VatNumber.ShouldBe(expectedCleanedVatNumber);

        // Verify the SOAP request was sent with the cleaned VAT number
        A.CallTo(httpMessageHandler)
            .Where(x => x.Method.Name == "SendAsync")
            .WithReturnType<Task<HttpResponseMessage>>()
            .MustHaveHappened();
    }

    [Fact]
    public async Task CheckVatAsync_ShouldParseValidResponse()
    {
        var httpMessageHandler = A.Fake<HttpMessageHandler>();
        var httpClient = new HttpClient(httpMessageHandler);
        var client = new ViesClient(httpClient);

        var responseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <ns2:checkVatResponse xmlns:ns2=""urn:ec.europa.eu:taxud:vies:services:checkVat:types"">
            <ns2:countryCode>BE</ns2:countryCode>
            <ns2:vatNumber>0477472701</ns2:vatNumber>
            <ns2:requestDate>2023-01-01</ns2:requestDate>
            <ns2:valid>true</ns2:valid>
            <ns2:name>Test Company</ns2:name>
            <ns2:address>Test Address</ns2:address>
        </ns2:checkVatResponse>
    </soap:Body>
</soap:Envelope>";

        A.CallTo(httpMessageHandler)
            .Where(x => x.Method.Name == "SendAsync")
            .WithReturnType<Task<HttpResponseMessage>>()
            .Returns(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            });

        var result = await client.CheckVatAsync("BE", "0477472701");

        result.IsValid.ShouldBeTrue();
        result.CountryCode.ShouldBe("BE");
        result.VatNumber.ShouldBe("0477472701");
        result.Name.ShouldBe("Test Company");
        result.Address.ShouldBe("Test Address");
    }

    [Fact]
    public async Task CheckVatAsync_ShouldThrowViesServiceUnavailable_OnSoapFault()
    {
        var httpMessageHandler = A.Fake<HttpMessageHandler>();
        var httpClient = new HttpClient(httpMessageHandler);
        var client = new ViesClient(httpClient);

        var faultResponse = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <soap:Fault>
            <faultcode>soap:Server</faultcode>
            <faultstring>SERVICE_UNAVAILABLE</faultstring>
        </soap:Fault>
    </soap:Body>
</soap:Envelope>";

        A.CallTo(httpMessageHandler)
            .Where(x => x.Method.Name == "SendAsync")
            .WithReturnType<Task<HttpResponseMessage>>()
            .Returns(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(faultResponse)
            });

        var exception = await Should.ThrowAsync<ViesServiceUnavailableException>(
            () => client.CheckVatAsync("BE", "0477472701"));
        
        exception.Message.ShouldContain("VIES service is temporarily unavailable");
    }

    [Theory]
    [InlineData("BE", "")] // Empty VAT number
    [InlineData("BE", "123456789012345")] // Too long (>12 chars after cleaning)
    [InlineData("BE", "1")] // Too short (<2 chars)
    [InlineData("BE", "123@456#789")] // Invalid characters that won't be cleaned
    [InlineData("B", "123456789")] // Invalid country code
    [InlineData("BEL", "123456789")] // Invalid country code
    [InlineData("be", "123456789")] // Lowercase country code
    public async Task CheckVatAsync_ShouldThrowArgumentException_OnInvalidFormat(string countryCode, string vatNumber)
    {
        var httpMessageHandler = A.Fake<HttpMessageHandler>();
        var httpClient = new HttpClient(httpMessageHandler);
        var client = new ViesClient(httpClient);

        await Should.ThrowAsync<ArgumentException>(() => client.CheckVatAsync(countryCode, vatNumber));
    }

    [Fact]
    public async Task CheckVatAsync_ShouldThrowVatValidationException_OnInvalidInput()
    {
        var httpMessageHandler = A.Fake<HttpMessageHandler>();
        var httpClient = new HttpClient(httpMessageHandler);
        var client = new ViesClient(httpClient);

        var faultResponse = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <soap:Fault>
            <faultcode>soap:Client</faultcode>
            <faultstring>INVALID_INPUT</faultstring>
        </soap:Fault>
    </soap:Body>
</soap:Envelope>";

        A.CallTo(httpMessageHandler)
            .Where(x => x.Method.Name == "SendAsync")
            .WithReturnType<Task<HttpResponseMessage>>()
            .Returns(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(faultResponse)
            });

        // The client now validates input before sending, so we get ArgumentException instead of SOAP fault
        var exception = await Should.ThrowAsync<ArgumentException>(
            () => client.CheckVatAsync("BE", ""));
        
        exception.Message.ShouldContain("VAT number must be 2-12 characters");
    }

    [Fact]
    public async Task IsServiceAvailableAsync_ShouldReturnTrue_WhenServiceResponds()
    {
        var httpMessageHandler = A.Fake<HttpMessageHandler>();
        var httpClient = new HttpClient(httpMessageHandler);
        var client = new ViesClient(httpClient);

        var responseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <ns2:checkVatResponse xmlns:ns2=""urn:ec.europa.eu:taxud:vies:services:checkVat:types"">
            <ns2:countryCode>BE</ns2:countryCode>
            <ns2:vatNumber>0477472701</ns2:vatNumber>
            <ns2:requestDate>2023-01-01</ns2:requestDate>
            <ns2:valid>true</ns2:valid>
        </ns2:checkVatResponse>
    </soap:Body>
</soap:Envelope>";

        A.CallTo(httpMessageHandler)
            .Where(x => x.Method.Name == "SendAsync")
            .WithReturnType<Task<HttpResponseMessage>>()
            .Returns(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            });

        var isAvailable = await client.IsServiceAvailableAsync();

        isAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task IsServiceAvailableAsync_ShouldReturnFalse_WhenServiceUnavailable()
    {
        var httpMessageHandler = A.Fake<HttpMessageHandler>();
        var httpClient = new HttpClient(httpMessageHandler);
        var client = new ViesClient(httpClient);

        var faultResponse = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <soap:Fault>
            <faultcode>soap:Server</faultcode>
            <faultstring>SERVICE_UNAVAILABLE</faultstring>
        </soap:Fault>
    </soap:Body>
</soap:Envelope>";

        A.CallTo(httpMessageHandler)
            .Where(x => x.Method.Name == "SendAsync")
            .WithReturnType<Task<HttpResponseMessage>>()
            .Returns(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(faultResponse)
            });

        var isAvailable = await client.IsServiceAvailableAsync();

        isAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task IsServiceAvailableAsync_ShouldReturnFalse_OnException()
    {
        var httpMessageHandler = A.Fake<HttpMessageHandler>();
        var httpClient = new HttpClient(httpMessageHandler);
        var client = new ViesClient(httpClient);

        A.CallTo(httpMessageHandler)
            .Where(x => x.Method.Name == "SendAsync")
            .WithReturnType<Task<HttpResponseMessage>>()
            .Throws(new HttpRequestException());

        var isAvailable = await client.IsServiceAvailableAsync();

        isAvailable.ShouldBeFalse();
    }
}