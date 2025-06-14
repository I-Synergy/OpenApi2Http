using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenApi2Http;
using System.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace OpenApi2HttpTests;

[TestClass]
public class IntegrationTests
{
    private string _testDataPath = "";
    private string _tempOutputPath = "";
    private WireMockServer? _server;

    [TestInitialize]
    public void Setup()
    {
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        _tempOutputPath = Path.Combine(Path.GetTempPath(), "openapi2http-tests");

        if (Directory.Exists(_tempOutputPath))
        {
            Directory.Delete(_tempOutputPath, true);
        }
        Directory.CreateDirectory(_tempOutputPath);

        _server = WireMockServer.Start();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempOutputPath))
        {
            Directory.Delete(_tempOutputPath, true);
        }

        _server?.Stop();
        _server?.Dispose();
    }

    [TestMethod]
    public async Task ProcessLocalFile_ShouldGenerateHttpFile()
    {
        // Arrange
        var inputFile = Path.Combine(_testDataPath, "petstore.json");
        var outputFile = new FileInfo(Path.Combine(_tempOutputPath, "petstore.http"));

        // Act
        await CallProcessOpenApiSource(inputFile, null, outputFile, false, false, 30);

        // Assert
        outputFile.Exists.Should().BeTrue();
        var content = await File.ReadAllTextAsync(outputFile.FullName);

        content.Should().Contain("# Pet Store API");
        content.Should().Contain("@endpoint = https://petstore.swagger.io/v2");
        content.Should().Contain("GET {{endpoint}}/pets");
        content.Should().Contain("POST {{endpoint}}/pets");
        content.Should().Contain("Content-Type: application/json");
    }

    [TestMethod]
    public async Task ProcessLocalFile_WithCustomEndpoint_ShouldOverrideServerUrl()
    {
        // Arrange
        var inputFile = Path.Combine(_testDataPath, "petstore.json");
        var outputFile = new FileInfo(Path.Combine(_tempOutputPath, "custom-endpoint.http"));
        var customEndpoint = "https://custom.api.com";

        // Act
        await CallProcessOpenApiSource(inputFile, customEndpoint, outputFile, false, false, 30);

        // Assert
        outputFile.Exists.Should().BeTrue();
        var content = await File.ReadAllTextAsync(outputFile.FullName);
        content.Should().Contain($"@endpoint = {customEndpoint}");
        content.Should().NotContain("@endpoint = https://petstore.swagger.io/v2");
    }

    [TestMethod]
    public async Task ProcessLocalFile_WithDefaultOutput_ShouldCreateFileWithCorrectName()
    {
        // Arrange
        var inputFile = Path.Combine(_testDataPath, "petstore.json");

        // Act
        await CallProcessOpenApiSource(inputFile, null, null, false, false, 30);

        // Assert
        var expectedOutputFile = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), "petstore.http"));
        expectedOutputFile.Exists.Should().BeTrue();

        // Cleanup
        if (expectedOutputFile.Exists)
        {
            expectedOutputFile.Delete();
        }
    }

    [TestMethod]
    public async Task ProcessInvalidFile_WithIgnoreFlag_ShouldGenerateHttpFile()
    {
        // Arrange
        var inputFile = Path.Combine(_testDataPath, "invalid.json");
        var outputFile = new FileInfo(Path.Combine(_tempOutputPath, "invalid.http"));

        // Act
        await CallProcessOpenApiSource(inputFile, "https://api.example.com", outputFile, true, false, 30);

        // Assert
        outputFile.Exists.Should().BeTrue();
        var content = await File.ReadAllTextAsync(outputFile.FullName);
        content.Should().Contain("# Invalid API");
        content.Should().Contain("@endpoint = https://api.example.com");
    }

    [TestMethod]
    public async Task ProcessInvalidFile_WithoutIgnoreFlag_ShouldThrowException()
    {
        // Arrange
        var inputFile = Path.Combine(_testDataPath, "invalid.json");
        var outputFile = new FileInfo(Path.Combine(_tempOutputPath, "invalid.http"));

        // Act & Assert
        await Assert.ThrowsExceptionAsync<SystemException>(
            () => CallProcessOpenApiSource(inputFile, "https://api.example.com", outputFile, false, false, 30));
    }

    [TestMethod]
    public async Task ProcessRemoteUrl_ShouldDownloadAndGenerateHttpFile()
    {
        // Arrange
        var openApiContent = await File.ReadAllTextAsync(Path.Combine(_testDataPath, "petstore.json"));

        _server!.Given(Request.Create().WithPath("/openapi.json").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(HttpStatusCode.OK)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody(openApiContent));

        var url = $"{_server.Urls[0]}/openapi.json";
        var outputFile = new FileInfo(Path.Combine(_tempOutputPath, "remote.http"));

        // Act
        await CallProcessOpenApiSource(url, null, outputFile, false, false, 30);

        // Assert
        outputFile.Exists.Should().BeTrue();
        var content = await File.ReadAllTextAsync(outputFile.FullName);

        content.Should().Contain("# Pet Store API");
        content.Should().Contain($"# Source: {url}");
        content.Should().Contain("# Generated:");
        content.Should().Contain("@endpoint = https://petstore.swagger.io/v2");
    }

    [TestMethod]
    public async Task ProcessRemoteUrl_WithTimeout_ShouldRespectTimeout()
    {
        // Arrange
        _server!.Given(Request.Create().WithPath("/slow.json").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(HttpStatusCode.OK)
                   .WithDelay(TimeSpan.FromSeconds(2))
                   .WithBody("{}"));

        var url = $"{_server.Urls[0]}/slow.json";
        var outputFile = new FileInfo(Path.Combine(_tempOutputPath, "timeout.http"));

        // Act & Assert
        await Assert.ThrowsExceptionAsync<Exception>(
            () => CallProcessOpenApiSource(url, null, outputFile, false, false, 1));
    }

    [TestMethod]
    public async Task ProcessFileWithoutServers_WithoutEndpoint_ShouldThrowException()
    {
        // Arrange
        var noServersContent = """
            {
              "openapi": "3.0.0",
              "info": {
                "title": "No Servers API",
                "version": "1.0.0"
              },
              "paths": {
                "/test": {
                  "get": {
                    "responses": {
                      "200": {
                        "description": "Success"
                      }
                    }
                  }
                }
              }
            }
            """;

        var tempFile = Path.Combine(_tempOutputPath, "no-servers.json");
        await File.WriteAllTextAsync(tempFile, noServersContent);
        var outputFile = new FileInfo(Path.Combine(_tempOutputPath, "no-servers.http"));

        // Act & Assert
        await Assert.ThrowsExceptionAsync<SystemException>(
            () => CallProcessOpenApiSource(tempFile, null, outputFile, false, false, 30));
    }

    [TestMethod]
    public async Task ProcessFileWithoutServers_WithEndpoint_ShouldUseProvidedEndpoint()
    {
        // Arrange
        var noServersContent = """
            {
              "openapi": "3.0.0",
              "info": {
                "title": "No Servers API",
                "version": "1.0.0"
              },
              "paths": {
                "/test": {
                  "get": {
                    "responses": {
                      "200": {
                        "description": "Success"
                      }
                    }
                  }
                }
              }
            }
            """;

        var tempFile = Path.Combine(_tempOutputPath, "no-servers.json");
        await File.WriteAllTextAsync(tempFile, noServersContent);
        var outputFile = new FileInfo(Path.Combine(_tempOutputPath, "no-servers.http"));
        var endpoint = "https://provided.endpoint.com";

        // Act
        await CallProcessOpenApiSource(tempFile, endpoint, outputFile, false, false, 30);

        // Assert
        outputFile.Exists.Should().BeTrue();
        var content = await File.ReadAllTextAsync(outputFile.FullName);
        content.Should().Contain($"@endpoint = {endpoint}");
    }

    [TestMethod]
    public async Task ProcessNonExistentFile_ShouldThrowException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempOutputPath, "does-not-exist.json");
        var outputFile = new FileInfo(Path.Combine(_tempOutputPath, "output.http"));

        // Act & Assert
        await Assert.ThrowsExceptionAsync<SystemException>(
            () => CallProcessOpenApiSource(nonExistentFile, null, outputFile, false, false, 30));
    }

    [TestMethod]
    public async Task ProcessRemoteUrl_WithInvalidUrl_ShouldThrowException()
    {
        // Arrange
        var invalidUrl = "https://definitely-does-not-exist-12345.com/openapi.json";
        var outputFile = new FileInfo(Path.Combine(_tempOutputPath, "invalid-url.http"));

        // Act & Assert
        await Assert.ThrowsExceptionAsync<Exception>(
            () => CallProcessOpenApiSource(invalidUrl, null, outputFile, false, false, 30));
    }

    [TestMethod]
    public async Task ProcessRemoteUrl_WithDefaultOutputName_ShouldGenerateCorrectFileName()
    {
        // Arrange
        var openApiContent = await File.ReadAllTextAsync(Path.Combine(_testDataPath, "petstore.json"));

        _server!.Given(Request.Create().WithPath("/v1/my-api.json").UsingGet())
               .RespondWith(Response.Create()
                   .WithStatusCode(HttpStatusCode.OK)
                   .WithHeader("Content-Type", "application/json")
                   .WithBody(openApiContent));

        var url = $"{_server.Urls[0]}/v1/my-api.json";

        // Act
        await CallProcessOpenApiSource(url, null, null, false, false, 30);

        // Assert
        var expectedFile = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), "my-api.http"));
        expectedFile.Exists.Should().BeTrue();

        // Cleanup
        if (expectedFile.Exists)
        {
            expectedFile.Delete();
        }
    }

    private async Task CallProcessOpenApiSource(string source, string? endpoint, FileInfo? output, bool ignore, bool verbose, int timeout)
    {
        var programType = typeof(Program);
        var method = programType.GetMethod("ProcessOpenApiSource",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method.Should().NotBeNull("ProcessOpenApiSource method should exist");

        // Use a new HttpClient for each call to avoid static timeout issues
        using var customClient = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout) };
        var parameters = new object?[] { source, endpoint, output, ignore, verbose, timeout, customClient };

        try
        {
            await (Task)method!.Invoke(null, parameters)!;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }
}