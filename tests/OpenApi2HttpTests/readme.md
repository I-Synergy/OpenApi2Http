# MSTest Unit Tests for OpenAPI2HTTP

## Project Structure

```
OpenApi2Http.Tests/
├── OpenApi2Http.Tests.csproj          # Test project file
├── TestData/                          # Test data files
│   ├── petstore.json                  # Valid OpenAPI spec
│   └── invalid.json                   # Invalid OpenAPI spec
├── UtilityTests.cs                    # Tests for utility methods
├── OpenApiParsingTests.cs             # Tests for OpenAPI parsing logic
├── HttpClientTests.cs                 # Tests for HTTP client functionality
├── IntegrationTests.cs                # End-to-end integration tests
├── CommandLineTests.cs                # Tests for command line parsing
└── TestConsole.cs                     # Helper class for testing console output
```

## Test Categories

### 1. **UtilityTests.cs**
Tests utility functions like:
- `IsUrl()` - URL validation
- `GetNameFromUrl()` - Extracting names from URLs
- `SanitizeFileName()` - File name sanitization
- `FormatAsComment()` - Comment formatting

### 2. **OpenApiParsingTests.cs**
Tests OpenAPI document processing:
- Valid/invalid spec parsing
- HTTP file generation
- Operation comment generation
- Content type detection
- Request counting
- Authentication header detection

### 3. **HttpClientTests.cs**
Tests HTTP functionality using WireMock:
- Downloading specs from URLs
- Timeout handling
- Error responses (404, 500)
- Redirects
- Different content types (JSON/YAML)

### 4. **IntegrationTests.cs**
End-to-end tests:
- Processing local files
- Processing remote URLs
- Custom endpoints
- Output file generation
- Error scenarios
- Validation with/without ignore flag

### 5. **CommandLineTests.cs**
Tests command line argument parsing:
- Valid/invalid argument combinations
- Default values
- Help and version output
- Option validation

## Running the Tests

### Prerequisites
1. Ensure the main project is in a sibling directory: `../OpenApi2Http/`
2. Install required test packages (included in .csproj)

### Commands

```bash
# Restore packages
dotnet restore

# Build the test project
dotnet build

# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "ClassName=UtilityTests"

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run tests in parallel
dotnet test --parallel
```

## Test Data

### petstore.json
A complete, valid OpenAPI 3.0 specification with:
- Multiple endpoints (`/pets`, `/pets/{petId}`)
- Different HTTP methods (GET, POST, PUT, DELETE)
- Authentication requirements
- Request/response schemas

### invalid.json
An OpenAPI spec with validation errors:
- Missing schema references
- Invalid structure
- Used to test error handling

## Key Testing Features

### 1. **Comprehensive Coverage**
- **Unit tests**: Individual method testing with mocked dependencies
- **Integration tests**: Full workflow testing
- **HTTP tests**: Network behavior with WireMock server
- **CLI tests**: Command line interface validation

### 2. **Realistic Test Data**
- Real OpenAPI specifications
- Various URL formats
- Edge cases and error conditions
- Authentication scenarios

### 3. **Advanced Testing Tools**
- **FluentAssertions**: Readable, expressive assertions
- **WireMock.Net**: HTTP server mocking
- **MSTest**: Microsoft's testing framework
- **Moq**: Object mocking (available but not used in current tests)

### 4. **Error Scenario Testing**
- Network timeouts
- Invalid URLs
- Missing files
- Validation errors
- Server errors (404, 500)

## Example Test Execution

```bash
# Navigate to test directory
cd OpenApi2Http.Tests

# Run all tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Expected output:
# Passed UtilityTests.IsUrl_ShouldReturnCorrectResult
# Passed OpenApiParsingTests.ValidOpenApiSpec_ShouldParseSuccessfully
# Passed HttpClientTests.DownloadOpenApiSpec_WithValidUrl_ShouldReturnContent
# Passed IntegrationTests.ProcessLocalFile_ShouldGenerateHttpFile
# Passed CommandLineTests.CommandLine_WithValidFileSource_ShouldParseSuccessfully
# ...
# Test Run Successful.
# Total tests: 45
# Passed: 45
# Failed: 0
# Skipped: 0
```

## Continuous Integration

The tests are designed to work in CI/CD environments:
- No external dependencies (except NuGet packages)
- Deterministic test data
- Proper cleanup of temporary files
- Cross-platform compatibility (.NET 8.0)

## Coverage Goals

The test suite aims for:
- **>90% line coverage** of the main application
- **100% coverage** of public methods
- **Edge case coverage** for error handling
- **Integration coverage** for full workflows

This comprehensive test suite ensures the OpenAPI2HTTP tool works reliably across different scenarios and environments.