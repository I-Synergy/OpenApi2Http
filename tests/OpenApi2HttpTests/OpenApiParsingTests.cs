using FluentAssertions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenApi2Http;
using System.Reflection;

namespace OpenApi2HttpTests;

[TestClass]
public class OpenApiParsingTests
{
    private string _testDataPath = "";

    [TestInitialize]
    public void Setup()
    {
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
    }

    [TestMethod]
    public async Task ValidOpenApiSpec_ShouldParseSuccessfully()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "petstore.json");
        var content = await File.ReadAllTextAsync(filePath);

        // Act
        var reader = new OpenApiStringReader();
        var document = reader.Read(content, out var diagnostic);

        // Assert
        document.Should().NotBeNull();
        document.Info.Title.Should().Be("Pet Store API");
        document.Info.Version.Should().Be("1.0.0");
        document.Servers.Should().HaveCount(1);
        document.Servers[0].Url.Should().Be("https://petstore.swagger.io/v2");
        document.Paths.Should().HaveCount(2);
        diagnostic.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public async Task InvalidOpenApiSpec_ShouldHaveValidationErrors()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "invalid.json");
        var content = await File.ReadAllTextAsync(filePath);

        // Act
        var reader = new OpenApiStringReader();
        var document = reader.Read(content, out var diagnostic);

        // Assert
        document.Should().NotBeNull();
        diagnostic.Errors.Should().NotBeEmpty();
        diagnostic.Errors.Should().Contain(e => e.Message.Contains("NonExistentSchema"));
    }

    [TestMethod]
    public async Task GenerateHttpFile_ShouldCreateValidHttpContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "petstore.json");
        var content = await File.ReadAllTextAsync(filePath);
        var reader = new OpenApiStringReader();
        var document = reader.Read(content, out _);
        var endpoint = "https://api.example.com";
        var source = filePath;

        // Act
        var result = CallPrivateMethod<string>("GenerateHttpFile", document, endpoint, source, false);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("# Pet Store API");
        result.Should().Contain("# Version: 1.0.0");
        result.Should().Contain("@endpoint = https://api.example.com");
        result.Should().Contain("### List all pets");
        result.Should().Contain("GET {{endpoint}}/pets");
        result.Should().Contain("### Create a pet");
        result.Should().Contain("POST {{endpoint}}/pets");
        result.Should().Contain("Content-Type: application/json");
        result.Should().Contain("### Get a pet by ID");
        result.Should().Contain("GET {{endpoint}}/pets/{petId}");
        result.Should().Contain("### Update a pet");
        result.Should().Contain("PUT {{endpoint}}/pets/{petId}");
        result.Should().Contain("### Delete a pet");
        result.Should().Contain("DELETE {{endpoint}}/pets/{petId}");
    }

    [TestMethod]
    public async Task GenerateHttpFile_WithAuthentication_ShouldIncludeAuthHeaders()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "petstore.json");
        var content = await File.ReadAllTextAsync(filePath);
        var reader = new OpenApiStringReader();
        var document = reader.Read(content, out _);
        var endpoint = "https://api.example.com";
        var source = filePath;

        // Act
        var result = CallPrivateMethod<string>("GenerateHttpFile", document, endpoint, source, false);

        // Assert
        result.Should().Contain("# Authorization: Bearer {{token}}");
        result.Should().Contain("# X-API-Key: {{apiKey}}");
    }

    [TestMethod]
    public async Task GenerateHttpFile_WithUrlSource_ShouldIncludeSourceUrl()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "petstore.json");
        var content = await File.ReadAllTextAsync(filePath);
        var reader = new OpenApiStringReader();
        var document = reader.Read(content, out _);
        var endpoint = "https://api.example.com";
        var source = "https://example.com/openapi.json";

        // Act
        var result = CallPrivateMethod<string>("GenerateHttpFile", document, endpoint, source, false);

        // Assert
        result.Should().Contain("# Source: https://example.com/openapi.json");
        result.Should().Contain("# Generated:");
    }

    [TestMethod]
    public async Task CountRequests_ShouldReturnCorrectNumber()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "petstore.json");
        var content = await File.ReadAllTextAsync(filePath);
        var reader = new OpenApiStringReader();
        var document = reader.Read(content, out _);

        // Act
        var result = CallPrivateMethod<int>("CountRequests", document);

        // Assert
        result.Should().Be(5); // GET, POST, GET by ID, PUT, DELETE
    }

    [TestMethod]
    public void GetOperationComment_ShouldPrioritizeSummaryOverDescription()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Summary = "Test Summary",
            Description = "Test Description",
            OperationId = "testOperation"
        };

        // Act
        var result = CallPrivateMethod<string>("GetOperationComment", operation, "GET", "/test");

        // Assert
        result.Should().Be("Test Summary");
    }

    [TestMethod]
    public void GetOperationComment_ShouldFallbackToDescription()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Description = "Test Description",
            OperationId = "testOperation"
        };

        // Act
        var result = CallPrivateMethod<string>("GetOperationComment", operation, "GET", "/test");

        // Assert
        result.Should().Be("Test Description");
    }

    [TestMethod]
    public void GetOperationComment_ShouldFallbackToOperationId()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            OperationId = "testOperation"
        };

        // Act
        var result = CallPrivateMethod<string>("GetOperationComment", operation, "GET", "/test");

        // Assert
        result.Should().Be("testOperation");
    }

    [TestMethod]
    public void GetOperationComment_ShouldFallbackToMethodAndPath()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        var result = CallPrivateMethod<string>("GetOperationComment", operation, "GET", "/test");

        // Assert
        result.Should().Be("GET /test");
    }

    [TestMethod]
    public void HasJsonContent_WithJsonRequestBody_ShouldReturnTrue()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType()
                }
            }
        };

        // Act
        var result = CallPrivateMethod<bool>("HasJsonContent", operation);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void HasJsonContent_WithoutJsonRequestBody_ShouldReturnFalse()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        var result = CallPrivateMethod<bool>("HasJsonContent", operation);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("post", true)]
    [DataRow("put", true)]
    [DataRow("patch", true)]
    [DataRow("get", false)]
    [DataRow("delete", false)]
    public void ShouldIncludeExampleBody_ShouldReturnCorrectResult(string method, bool expected)
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType()
                }
            }
        };

        // Act
        var result = CallPrivateMethod<bool>("ShouldIncludeExampleBody", method, operation);

        // Assert
        result.Should().Be(expected);
    }

    private T CallPrivateMethod<T>(string methodName, params object[] parameters)
    {
        var programType = typeof(Program);
        var method = programType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull($"Method {methodName} should exist");

        var result = method!.Invoke(null, parameters);
        return (T)result!;
    }
}
