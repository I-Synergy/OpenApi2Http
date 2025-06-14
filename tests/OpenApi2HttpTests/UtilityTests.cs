using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenApi2Http;
using System.Reflection;

namespace OpenApi2HttpTests;

[TestClass]
public class UtilityTests
{
    [TestMethod]
    [DataRow("https://example.com/api.json", true)]
    [DataRow("http://localhost:8080/openapi.yaml", true)]
    [DataRow("./local-file.yaml", false)]
    [DataRow("C:\\Users\\file.json", false)]
    [DataRow("/usr/local/file.yaml", false)]
    [DataRow("file.json", false)]
    [DataRow("", false)]
    [DataRow("ftp://example.com/file.json", false)]
    public void IsUrl_ShouldReturnCorrectResult(string input, bool expected)
    {
        // Arrange & Act
        var result = CallPrivateMethod<bool>("IsUrl", input);

        // Assert
        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("https://api.example.com/v1/openapi.json", "openapi")]
    [DataRow("https://petstore.swagger.io/v2/swagger.yaml", "swagger")]
    [DataRow("https://github.com/user/repo/api-spec.json", "api-spec")]
    [DataRow("https://api.github.com/", "api.github.com")]
    [DataRow("https://www.example.com/", "example.com")]
    [DataRow("https://subdomain.example.com/complex/path/", "subdomain.example.com")]
    public void GetNameFromUrl_ShouldReturnExpectedName(string url, string expected)
    {
        // Arrange & Act
        var result = CallPrivateMethod<string>("GetNameFromUrl", url);

        // Assert
        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("normal-file", "normal-file")]
    [DataRow("file with spaces", "file_with_spaces")]
    [DataRow("file<>:\"|?*name", "file_name")]
    [DataRow("file/\\name", "file_name")]
    [DataRow("", "openapi")]
    [DataRow("   ", "openapi")]
    public void SanitizeFileName_ShouldRemoveInvalidCharacters(string input, string expected)
    {
        // Arrange & Act
        var result = CallPrivateMethod<string>("SanitizeFileName", input);

        // Assert
        result.Should().Be(expected);
    }

    [TestMethod]
    public void FormatAsComment_ShouldReplaceNewlinesWithCommentPrefix()
    {
        // Arrange
        var input = "Line 1\nLine 2\nLine 3";
        var expected = "Line 1\n# Line 2\n# Line 3";

        // Act
        var result = CallPrivateMethod<string>("FormatAsComment", input);

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
