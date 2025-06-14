using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

namespace OpenApi2HttpTests;

[TestClass]
public class CommandLineTests
{
    [TestMethod]
    public void CommandLine_WithValidFileSource_ShouldParseSuccessfully()
    {
        // Arrange
        var args = new[] { "-s", "test.yaml" };

        // Act
        var parser = CreateParser();
        var parseResult = parser.Parse(args);

        // Assert
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValueForOption(GetSourceOption(parser)).Should().Be("test.yaml");
    }

    [TestMethod]
    public void CommandLine_WithValidUrlSource_ShouldParseSuccessfully()
    {
        // Arrange
        var args = new[] { "-s", "https://example.com/openapi.json" };

        // Act
        var parser = CreateParser();
        var parseResult = parser.Parse(args);

        // Assert
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValueForOption(GetSourceOption(parser)).Should().Be("https://example.com/openapi.json");
    }

    [TestMethod]
    public void CommandLine_WithFileOption_ShouldParseSuccessfully()
    {
        // Arrange
        var args = new[] { "-f", "test.yaml" };

        // Act
        var parser = CreateParser();
        var parseResult = parser.Parse(args);

        // Assert
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValueForOption(GetFileOption(parser)).Should().Be("test.yaml");
    }

    [TestMethod]
    public void CommandLine_WithBothSourceAndFile_ShouldHaveValidationError()
    {
        // Arrange
        var args = new[] { "-s", "test.yaml", "-f", "another.yaml" };

        // Act
        var parser = CreateParser();
        var parseResult = parser.Parse(args);

        // Assert
        parseResult.Errors.Should().NotBeEmpty();
        parseResult.Errors.Should().Contain(e => e.Message.Contains("Cannot specify both --source and --file"));
    }

    [TestMethod]
    public void CommandLine_WithoutSourceOrFile_ShouldHaveValidationError()
    {
        // Arrange
        var args = new[] { "-e", "https://api.example.com" };

        // Act
        var parser = CreateParser();
        var parseResult = parser.Parse(args);

        // Assert
        parseResult.Errors.Should().NotBeEmpty();
        parseResult.Errors.Should().Contain(e => e.Message.Contains("Either --source or --file must be specified"));
    }

    [TestMethod]
    public void CommandLine_WithAllOptions_ShouldParseSuccessfully()
    {
        // Arrange
        var args = new[] {
            "-s", "test.yaml",
            "-e", "https://api.example.com",
            "-o", "output.http",
            "--ignore",
            "--verbose",
            "--timeout", "60"
        };

        // Act
        var parser = CreateParser();
        var parseResult = parser.Parse(args);

        // Assert
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValueForOption(GetSourceOption(parser)).Should().Be("test.yaml");
        parseResult.GetValueForOption(GetEndpointOption(parser)).Should().Be("https://api.example.com");
        parseResult.GetValueForOption(GetOutputOption(parser))?.Name.Should().Be("output.http");
        parseResult.GetValueForOption(GetIgnoreOption(parser)).Should().BeTrue();
        parseResult.GetValueForOption(GetVerboseOption(parser)).Should().BeTrue();
        parseResult.GetValueForOption(GetTimeoutOption(parser)).Should().Be(60);
    }

    [TestMethod]
    public void CommandLine_WithShortOptions_ShouldParseSuccessfully()
    {
        // Arrange
        var args = new[] { "-f", "test.yaml", "-e", "https://api.com", "-o", "out.http", "-i", "-v", "-t", "45" };

        // Act
        var parser = CreateParser();
        var parseResult = parser.Parse(args);

        // Assert
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValueForOption(GetFileOption(parser)).Should().Be("test.yaml");
        parseResult.GetValueForOption(GetEndpointOption(parser)).Should().Be("https://api.com");
        parseResult.GetValueForOption(GetOutputOption(parser))?.Name.Should().Be("out.http");
        parseResult.GetValueForOption(GetIgnoreOption(parser)).Should().BeTrue();
        parseResult.GetValueForOption(GetVerboseOption(parser)).Should().BeTrue();
        parseResult.GetValueForOption(GetTimeoutOption(parser)).Should().Be(45);
    }

    [TestMethod]
    public void CommandLine_WithLongOptions_ShouldParseSuccessfully()
    {
        // Arrange
        var args = new[] {
            "--source", "test.yaml",
            "--endpoint", "https://api.com",
            "--output", "out.http",
            "--ignore",
            "--verbose",
            "--timeout", "45"
        };

        // Act
        var parser = CreateParser();
        var parseResult = parser.Parse(args);

        // Assert
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValueForOption(GetSourceOption(parser)).Should().Be("test.yaml");
        parseResult.GetValueForOption(GetEndpointOption(parser)).Should().Be("https://api.com");
        parseResult.GetValueForOption(GetOutputOption(parser))?.Name.Should().Be("out.http");
        parseResult.GetValueForOption(GetIgnoreOption(parser)).Should().BeTrue();
        parseResult.GetValueForOption(GetVerboseOption(parser)).Should().BeTrue();
        parseResult.GetValueForOption(GetTimeoutOption(parser)).Should().Be(45);
    }

    [TestMethod]
    public void CommandLine_WithDefaultTimeout_ShouldReturn30()
    {
        // Arrange
        var args = new[] { "-s", "test.yaml" };

        // Act
        var parser = CreateParser();
        var parseResult = parser.Parse(args);

        // Assert
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValueForOption(GetTimeoutOption(parser)).Should().Be(30);
    }

    [TestMethod]
    public void CommandLine_WithInvalidTimeout_ShouldHaveValidationError()
    {
        // Arrange
        var args = new[] { "-s", "test.yaml", "--timeout", "invalid" };

        // Act
        var parser = CreateParser();
        var parseResult = parser.Parse(args);

        // Assert
        parseResult.Errors.Should().NotBeEmpty();
    }

    [TestMethod]
    public void CommandLine_WithNegativeTimeout_ShouldParseButBeInvalid()
    {
        // Arrange
        var args = new[] { "-s", "test.yaml", "--timeout", "-5" };

        // Act
        var parser = CreateParser();
        var parseResult = parser.Parse(args);

        // Assert
        parseResult.Errors.Should().BeEmpty(); // Parser doesn't validate negative numbers
        parseResult.GetValueForOption(GetTimeoutOption(parser)).Should().Be(-5);
    }

    [TestMethod]
    public async Task CommandLine_WithHelpFlag_ShouldShowHelp()
    {
        // Arrange
        var args = new[] { "--help" };
        var console = new TestConsole();

        // Act
        var parser = CreateParser();
        var exitCode = await parser.InvokeAsync(args, console);

        // Assert
        exitCode.Should().Be(0);
        var output = console.GetOutput();
        output.Should().Contain("Convert OpenAPI specifications to .http files");
        output.Should().Contain("--source");
        output.Should().Contain("--endpoint");
        output.Should().Contain("--output");
    }

    [TestMethod]
    public async Task CommandLine_WithVersionFlag_ShouldShowVersion()
    {
        // Arrange
        var args = new[] { "--version" };
        var console = new TestConsole();

        // Act
        var parser = CreateParser();
        var exitCode = await parser.InvokeAsync(args, console);

        // Assert
        exitCode.Should().Be(0);
        var output = console.GetOutput();
        output.Should().NotBeEmpty();
    }

    private Parser CreateParser()
    {
        // Create a test version of the command line parser
        var sourceOption = new Option<string>(
            aliases: new[] { "-s", "--source" },
            description: "OpenAPI source (file path or HTTP/HTTPS URL)");

        var fileOption = new Option<string>(
            aliases: new[] { "-f", "--file" },
            description: "Input OpenAPI file path (use --source for URLs)");

        var endpointOption = new Option<string>(
            aliases: new[] { "-e", "--endpoint" },
            description: "Base URL for the API endpoint (overrides servers in spec)");

        var outputOption = new Option<FileInfo>(
            aliases: new[] { "-o", "--output" },
            description: "Output .http file (defaults to generated name based on source)");

        var ignoreOption = new Option<bool>(
            aliases: new[] { "-i", "--ignore" },
            description: "Ignore validation errors and generate the .http file anyway");

        var verboseOption = new Option<bool>(
            aliases: new[] { "-v", "--verbose" },
            description: "Show verbose output");

        var timeoutOption = new Option<int>(
            aliases: new[] { "-t", "--timeout" },
            description: "HTTP timeout in seconds (default: 30)",
            getDefaultValue: () => 30);

        var rootCommand = new RootCommand("Convert OpenAPI specifications to .http files for API testing")
        {
            sourceOption,
            fileOption,
            endpointOption,
            outputOption,
            ignoreOption,
            verboseOption,
            timeoutOption
        };

        // Add the same validation as in the main program
        rootCommand.AddValidator(result =>
        {
            var source = result.GetValueForOption(sourceOption);
            var file = result.GetValueForOption(fileOption);

            if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(file))
            {
                result.ErrorMessage = "Either --source or --file must be specified";
            }
            else if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(file))
            {
                result.ErrorMessage = "Cannot specify both --source and --file";
            }
        });

        return new CommandLineBuilder(rootCommand).UseDefaults().Build();
    }

    private Option<string> GetSourceOption(Parser parser)
    {
        return GetOption<string>(parser, "--source");
    }

    private Option<string> GetFileOption(Parser parser)
    {
        return GetOption<string>(parser, "--file");
    }

    private Option<string> GetEndpointOption(Parser parser)
    {
        return GetOption<string>(parser, "--endpoint");
    }

    private Option<FileInfo> GetOutputOption(Parser parser)
    {
        return GetOption<FileInfo>(parser, "--output");
    }

    private Option<bool> GetIgnoreOption(Parser parser)
    {
        return GetOption<bool>(parser, "--ignore");
    }

    private Option<bool> GetVerboseOption(Parser parser)
    {
        return GetOption<bool>(parser, "--verbose");
    }

    private Option<int> GetTimeoutOption(Parser parser)
    {
        return GetOption<int>(parser, "--timeout");
    }

    private Option<T> GetOption<T>(Parser parser, string name)
    {
        var rootCommand = (RootCommand)parser.Configuration.RootCommand;
        return (Option<T>)rootCommand.Options.First(o => o.Aliases.Contains(name));
    }
}
