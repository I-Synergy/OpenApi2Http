using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using System.CommandLine;
using System.Text;

namespace OpenApi2Http;

public class Program
{
    private static readonly HttpClient httpClient = new();

    static async Task<int> Main(string[] args)
    {
        var sourceOption = new Option<string>(
            aliases: new[] { "-s", "--source" },
            description: "OpenAPI source (file path or HTTP/HTTPS URL)")
        {
            IsRequired = true
        };

        // Keep -f for backward compatibility
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

        // Add validation to ensure either source or file is provided
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

        rootCommand.SetHandler(async (string? source, string? file, string? endpoint, FileInfo? output, bool ignore, bool verbose, int timeout) =>
        {
            try
            {
                // Use source if provided, otherwise use file (for backward compatibility)
                var actualSource = source ?? file!;
                await ProcessOpenApiSource(actualSource, endpoint, output, ignore, verbose, timeout);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (verbose)
                {
                    Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                Environment.Exit(1);
            }
        }, sourceOption, fileOption, endpointOption, outputOption, ignoreOption, verboseOption, timeoutOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task ProcessOpenApiSource(string source, string? endpointOverride, FileInfo? outputFile, bool ignoreErrors, bool verbose, int timeoutSeconds, HttpClient? customClient = null)
    {
        var client = customClient ?? httpClient;
        // Do NOT set timeout on the client here; assume it's set by the caller if needed

        string fileContent;
        string sourceName;

        if (IsUrl(source))
        {
            if (verbose)
            {
                Console.WriteLine($"Downloading OpenAPI spec from: {source}");
            }

            try
            {
                fileContent = await DownloadOpenApiSpec(source, verbose, client);
                sourceName = GetNameFromUrl(source);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download OpenAPI spec: {ex.Message}", ex);
            }
        }
        else
        {
            if (verbose)
            {
                Console.WriteLine($"Reading OpenAPI file: {source}");
            }

            if (!File.Exists(source))
            {
                throw new SystemException($"File does not exist: {source}");
            }

            fileContent = await File.ReadAllTextAsync(source);
            sourceName = Path.GetFileNameWithoutExtension(source);
        }

        // Parse & validate the OpenAPI file
        var reader = new OpenApiStringReader();
        var document = reader.Read(fileContent, out var diagnostic);

        if (diagnostic.Errors.Count > 0)
        {
            if (verbose || !ignoreErrors)
            {
                Console.WriteLine($"Found {diagnostic.Errors.Count} validation error(s):");
                foreach (var error in diagnostic.Errors)
                {
                    Console.WriteLine($"  - {error.Message}");
                }
            }

            if (!ignoreErrors)
            {
                throw new SystemException("OpenAPI spec contains validation errors. Use --ignore to proceed anyway.");
            }
        }

        if (diagnostic.Warnings.Count > 0 && verbose)
        {
            Console.WriteLine($"Found {diagnostic.Warnings.Count} warning(s):");
            foreach (var warning in diagnostic.Warnings)
            {
                Console.WriteLine($"  - {warning.Message}");
            }
        }

        // Determine endpoint
        string endpoint = "";
        if (document.Servers?.Count > 0)
        {
            endpoint = document.Servers[0].Url;
            if (verbose)
            {
                Console.WriteLine($"Using server from spec: {endpoint}");
            }
        }

        if (!string.IsNullOrEmpty(endpointOverride))
        {
            endpoint = endpointOverride;
            if (verbose)
            {
                Console.WriteLine($"Using endpoint override: {endpoint}");
            }
        }

        if (string.IsNullOrEmpty(endpoint))
        {
            throw new SystemException("No servers found in OpenAPI spec. Please provide an endpoint with --endpoint");
        }

        // Determine output file
        if (outputFile == null)
        {
            var currentDir = Directory.GetCurrentDirectory();
            var fileName = !string.IsNullOrEmpty(sourceName) ? sourceName : "openapi";
            outputFile = new FileInfo(Path.Combine(currentDir, $"{fileName}.http"));
        }

        if (verbose)
        {
            Console.WriteLine($"Output file: {outputFile.FullName}");
        }

        // Generate .http file content
        var httpContent = GenerateHttpFile(document, endpoint, source, verbose);

        // Write output file
        await File.WriteAllTextAsync(outputFile.FullName, httpContent);

        Console.WriteLine($"Successfully generated {outputFile.Name}");
        if (verbose)
        {
            Console.WriteLine($"Generated {CountRequests(document)} HTTP requests");
        }
    }

    static bool IsUrl(string source)
    {
        return Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    static async Task<string> DownloadOpenApiSpec(string url, bool verbose, HttpClient? customClient = null)
    {
        var client = customClient ?? httpClient;
        try
        {
            using var response = await client.GetAsync(url);

            if (verbose)
            {
                Console.WriteLine($"HTTP {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"Content-Type: {response.Content.Headers.ContentType}");
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            if (verbose)
            {
                Console.WriteLine($"Downloaded {content.Length} characters");
            }

            return content;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"HTTP request failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new InvalidOperationException("Request timed out", ex);
        }
        catch (TaskCanceledException ex)
        {
            // For .NET HTTP timeouts that do not wrap TimeoutException
            throw new InvalidOperationException("Request timed out", ex);
        }
    }

    static string GetNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var segments = uri.Segments;

            // Try to get a meaningful name from the URL
            if (segments.Length > 1)
            {
                var lastSegment = segments[^1].TrimEnd('/');
                // Only use last segment if it looks like a file (contains a dot)
                if (!string.IsNullOrEmpty(lastSegment) && lastSegment.Contains('.'))
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(lastSegment);
                    if (!string.IsNullOrEmpty(nameWithoutExt))
                    {
                        return SanitizeFileName(nameWithoutExt);
                    }
                }
            }

            // Fall back to host name
            var host = uri.Host.Replace("www.", "");
            return SanitizeFileName(host);
        }
        catch
        {
            return "openapi";
        }
    }

    static string SanitizeFileName(string fileName)
    {
        // Remove invalid file name characters
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        // Replace all whitespace with underscores
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, "\\s+", "_");
        if (string.IsNullOrWhiteSpace(sanitized))
            return "openapi";
        return sanitized;
    }

    static string GenerateHttpFile(OpenApiDocument document, string endpoint, string source, bool verbose)
    {
        var httpFile = new StringBuilder();

        // Header comments
        string title = document.Info.Title;
        string description = document.Info.Description;
        string serverDesc = document.Servers?.FirstOrDefault()?.Description ?? "API Endpoint";

        // Format multi-line descriptions as comments
        title = FormatAsComment(title);
        if (!string.IsNullOrEmpty(description) && description != title)
            description = FormatAsComment(description);
        else
            description = null;
        serverDesc = FormatAsComment(serverDesc);

        httpFile.AppendLine("#");
        httpFile.AppendLine($"# {title}");
        if (!string.IsNullOrEmpty(description) && description != title)
        {
            httpFile.AppendLine($"# {description}");
        }
        if (!string.IsNullOrEmpty(document.Info.Version))
        {
            httpFile.AppendLine($"# Version: {document.Info.Version}");
        }
        if (IsUrl(source))
        {
            httpFile.AppendLine($"# Source: {source}");
        }
        httpFile.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        httpFile.AppendLine("#");
        httpFile.AppendLine();

        httpFile.AppendLine($"# {serverDesc}");
        httpFile.AppendLine($"@endpoint = {endpoint}");
        httpFile.AppendLine();

        // Generate requests for each path/operation
        foreach (var pathItem in document.Paths.OrderBy(p => p.Key))
        {
            string pathKey = pathItem.Key;
            var pathValue = pathItem.Value;

            // Get all operations for this path
            var operations = new List<(string method, OpenApiOperation operation)>();

            foreach (var op in pathValue.Operations)
            {
                operations.Add((op.Key.ToString().ToLowerInvariant(), op.Value));
            }

            // Sort operations by common HTTP method order
            var methodOrder = new[] { "get", "post", "put", "patch", "delete", "head", "options", "trace" };
            operations = operations.OrderBy(op => Array.IndexOf(methodOrder, op.method)).ToList();

            foreach (var (method, operation) in operations)
            {
                string methodUpper = method.ToUpperInvariant();

                // Determine comment for the request
                string comment = GetOperationComment(operation, methodUpper, pathKey);

                httpFile.AppendLine($"### {comment}");
                httpFile.AppendLine($"{methodUpper} {{{{endpoint}}}}{pathKey}");

                // Add common headers if the operation accepts JSON
                if (HasJsonContent(operation))
                {
                    httpFile.AppendLine("Content-Type: application/json");
                }

                // Add authentication headers if required
                var authHeaders = GetAuthenticationHeaders(operation, document);
                foreach (var header in authHeaders)
                {
                    httpFile.AppendLine(header);
                }

                httpFile.AppendLine();

                // Add example body for POST/PUT/PATCH operations
                if (ShouldIncludeExampleBody(method, operation))
                {
                    var exampleBody = GenerateExampleBody(operation);
                    if (!string.IsNullOrEmpty(exampleBody))
                    {
                        httpFile.AppendLine(exampleBody);
                        httpFile.AppendLine();
                    }
                }

                httpFile.AppendLine();
            }
        }

        return httpFile.ToString();
    }

    static string GetOperationComment(OpenApiOperation operation, string method, string path)
    {
        if (!string.IsNullOrEmpty(operation.Summary))
            return operation.Summary;

        if (!string.IsNullOrEmpty(operation.Description))
        {
            // Use first line of description if it's multi-line
            var firstLine = operation.Description.Split('\n')[0].Trim();
            return firstLine;
        }

        if (!string.IsNullOrEmpty(operation.OperationId))
            return operation.OperationId;

        return $"{method} {path}";
    }

    static bool HasJsonContent(OpenApiOperation operation)
    {
        return operation.RequestBody?.Content?.ContainsKey("application/json") == true;
    }

    static bool ShouldIncludeExampleBody(string method, OpenApiOperation operation)
    {
        var methodsWithBody = new[] { "post", "put", "patch" };
        return methodsWithBody.Contains(method) && HasJsonContent(operation);
    }

    static string GenerateExampleBody(OpenApiOperation operation)
    {
        // This is a simplified example body generator
        // In a real implementation, you might want to generate more sophisticated examples
        // based on the schema
        return "{\n  // Add your request body here\n}";
    }

    static List<string> GetAuthenticationHeaders(OpenApiOperation operation, OpenApiDocument document)
    {
        var headers = new List<string>();

        // Check if operation requires authentication
        if (operation.Security?.Count > 0 || document.SecurityRequirements?.Count > 0)
        {
            // Add common authentication headers as comments
            headers.Add("# Authorization: Bearer {{token}}");
            headers.Add("# X-API-Key: {{apiKey}}");
        }

        return headers;
    }

    static string FormatAsComment(string text)
    {
        return text.Replace("\n", "\n# ");
    }

    static int CountRequests(OpenApiDocument document)
    {
        return document.Paths.Sum(p => p.Value.Operations.Count);
    }
}