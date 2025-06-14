using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenApi2Http;
using System.Net;
using System.Reflection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace OpenApi2HttpTests;

[TestClass]
public class HttpClientTests
{
    private WireMockServer? _server;
    private string _baseUrl = "";

    [TestInitialize]
    public void Setup()
    {
        _server = WireMockServer.Start();
        _baseUrl = _server.Urls[0];
    }

    [TestCleanup]
    public void Cleanup()
    {
        _server?.Stop();
        _server?.Dispose();
    }

    [TestMethod]
    public async Task DownloadOpenApiSpec_WithValidUrl_ShouldReturnContent()
    {
        // Arrange
        var openApiContent = """
            {
              "openapi": "3.0.0",
              "info": {
                "title": "Test API",
                "version": "1.0.0"
              },
              "paths": {}
            }
            """;

        _server!.Given(Request.Create().WithPath("/openapi.json").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(HttpStatusCode.OK)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody(openApiContent));

        var url = $"{_baseUrl}/openapi.json";

        // Act
        var result = await CallPrivateMethodAsync<string>("DownloadOpenApiSpec", url, false, null);

        // Assert
        result.Should().Be(openApiContent);
    }

    [TestMethod]
    public async Task DownloadOpenApiSpec_WithNotFoundUrl_ShouldThrowException()
    {
        // Arrange
        _server!.Given(Request.Create().WithPath("/notfound.json").UsingGet())
               .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.NotFound));

        var url = $"{_baseUrl}/notfound.json";

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => CallPrivateMethodAsync<string>("DownloadOpenApiSpec", url, false, null));

        exception.Message.Should().Contain("HTTP request failed");
    }

    [TestMethod]
    public async Task DownloadOpenApiSpec_WithSlowResponse_ShouldRespectTimeout()
    {
        // Arrange
        _server!.Given(Request.Create().WithPath("/slow.json").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(HttpStatusCode.OK)
                   .WithDelay(TimeSpan.FromSeconds(2))
                   .WithBody("{}"));

        var url = $"{_baseUrl}/slow.json";

        // Use a new HttpClient with a short timeout
        using var customClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => CallPrivateMethodAsync<string>("DownloadOpenApiSpec", url, false, customClient));

        exception.Message.Should().Contain("timed out");
    }

    [TestMethod]
    public async Task DownloadOpenApiSpec_WithYamlContent_ShouldReturnYamlString()
    {
        // Arrange
        var yamlContent = """
            openapi: '3.0.0'
            info:
              title: Test API
              version: '1.0.0'
            paths: {}
            """;

        _server!.Given(Request.Create().WithPath("/openapi.yaml").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(HttpStatusCode.OK)
                   .WithHeader("Content-Type", "application/yaml")
                   .WithBody(yamlContent));

        var url = $"{_baseUrl}/openapi.yaml";

        // Act
        var result = await CallPrivateMethodAsync<string>("DownloadOpenApiSpec", url, false, null);

        // Assert
        result.Should().Be(yamlContent);
    }

    [TestMethod]
    public async Task DownloadOpenApiSpec_WithRedirect_ShouldFollowRedirect()
    {
        // Arrange
        var finalContent = """
            {
              "openapi": "3.0.0",
              "info": {
                "title": "Redirected API",
                "version": "1.0.0"
              },
              "paths": {}
            }
            """;

        _server!.Given(Request.Create().WithPath("/redirect").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(HttpStatusCode.Redirect)
                   .WithHeader("Location", $"{_baseUrl}/final"));

        _server!.Given(Request.Create().WithPath("/final").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(HttpStatusCode.OK)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody(finalContent));

        var url = $"{_baseUrl}/redirect";

        // Act
        var result = await CallPrivateMethodAsync<string>("DownloadOpenApiSpec", url, false, null);

        // Assert
        result.Should().Be(finalContent);
    }

    [TestMethod]
    public async Task DownloadOpenApiSpec_WithServerError_ShouldThrowException()
    {
        // Arrange
        _server!.Given(Request.Create().WithPath("/error.json").UsingGet())
               .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

        var url = $"{_baseUrl}/error.json";

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => CallPrivateMethodAsync<string>("DownloadOpenApiSpec", url, false, null));

        exception.Message.Should().Contain("HTTP request failed");
    }

    [TestMethod]
    public async Task DownloadOpenApiSpec_WithCustomUserAgent_ShouldIncludeUserAgent()
    {
        // Arrange
        var openApiContent = """{"openapi": "3.0.0", "info": {"title": "Test", "version": "1.0.0"}, "paths": {}}""";

        _server!.Given(Request.Create().WithPath("/openapi.json").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(HttpStatusCode.OK)
                   .WithBody(openApiContent));

        var url = $"{_baseUrl}/openapi.json";

        // Use a custom HttpClient with a custom User-Agent
        using var customClient = new HttpClient();
        customClient.DefaultRequestHeaders.UserAgent.ParseAdd("OpenApi2HttpTestAgent/1.0");

        // Act
        var result = await CallPrivateMethodAsync<string>("DownloadOpenApiSpec", url, false, customClient);

        // Assert
        result.Should().Be(openApiContent);

        // Verify the request was made (WireMock logs would show user agent in real scenario)
        var requests = _server.LogEntries.Select(x => x.RequestMessage).ToList();
        requests.Should().HaveCount(1);
        requests[0].Path.Should().Be("/openapi.json");
        requests[0].Headers["User-Agent"].Should().Contain(h => h.Contains("OpenApi2HttpTestAgent/1.0"));
    }

    private async Task<T> CallPrivateMethodAsync<T>(string methodName, params object[] parameters)
    {
        var programType = typeof(Program);
        var method = programType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull($"Method {methodName} should exist");

        var task = (Task)method!.Invoke(null, parameters)!;
        await task;

        var resultProperty = task.GetType().GetProperty("Result");
        return (T)resultProperty!.GetValue(task)!;
    }
}
