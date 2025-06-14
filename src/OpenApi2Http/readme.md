# OpenAPI to HTTP Converter

A .NET global tool that converts OpenAPI specifications (Swagger) to `.http` files for API testing in Visual Studio Code, JetBrains IDEs, and other HTTP clients.

## Installation

Install as a global .NET tool:

```bash
dotnet tool install --global OpenApi2Http
```

Or install from a local package:

```bash
dotnet pack
dotnet tool install --global --add-source ./bin/Debug OpenApi2Http
```

## Usage

```bash
openapi2http -s <source> [options]
```

### Options

- `-s, --source <source>` **(required)** - OpenAPI source (file path or HTTP/HTTPS URL)
- `-f, --file <file>` - Input OpenAPI file path (alternative to --source, for backward compatibility)
- `-e, --endpoint <url>` - Base URL for the API endpoint (overrides servers in spec)
- `-o, --output <file>` - Output .http file (defaults to generated name based on source)
- `-i, --ignore` - Ignore validation errors and generate the .http file anyway
- `-v, --verbose` - Show verbose output
- `-t, --timeout <seconds>` - HTTP timeout in seconds (default: 30)
- `--help` - Show help information

### Examples

```bash
# From local file
openapi2http -s petstore.yaml

# From HTTP URL
openapi2http -s https://petstore.swagger.io/v2/swagger.json

# From HTTPS with custom endpoint
openapi2http -s https://api.github.com/openapi.json -e https://api.github.com

# With custom output file and verbose logging
openapi2http -s https://example.com/api/openapi.yaml -o my-api.http --verbose

# Ignore validation errors with custom timeout
openapi2http -s https://slow-server.com/spec.json --ignore --timeout 60

# Backward compatibility - using -f flag
openapi2http -f local-spec.yaml
```

## Supported Sources

- **Local files**: YAML or JSON OpenAPI specifications
- **HTTP/HTTPS URLs**: Direct links to OpenAPI specifications
- **Popular sources**:
  - GitHub raw files: `https://raw.githubusercontent.com/user/repo/main/openapi.yaml`
  - Swagger Hub: `https://api.swaggerhub.com/apis/user/api/version/swagger.json`
  - Public APIs: Many APIs provide direct links to their OpenAPI specs

## Generated Output

The tool generates `.http` files with:

- API description and version information as comments
- Environment variable for the base endpoint
- Individual HTTP requests for each operation
- Proper HTTP method and path
- Content-Type headers for JSON operations
- Placeholder request bodies for POST/PUT/PATCH operations

### Example Output

```http
#
# Pet Store API
# Version: 1.0.0
# Source: https://petstore.swagger.io/v2/swagger.json
# Generated: 2025-06-14 10:30:15
#

# API Endpoint
@endpoint = https://petstore.swagger.io/v2

### Get all pets
GET {{endpoint}}/pets
# Authorization: Bearer {{token}}
# X-API-Key: {{apiKey}}

### Create a new pet
POST {{endpoint}}/pets
Content-Type: application/json
# Authorization: Bearer {{token}}
# X-API-Key: {{apiKey}}

{
  // Add your request body here
}

### Get pet by ID
GET {{endpoint}}/pets/{id}
```

## Supported Formats

- **Input**: 
  - OpenAPI 3.0+ specifications in YAML or JSON format
  - Local files or HTTP/HTTPS URLs
  - Automatic content-type detection
- **Output**: HTTP files compatible with:
  - Visual Studio Code REST Client extension
  - JetBrains HTTP Client (IntelliJ IDEA, WebStorm, etc.)
  - Other tools supporting the `.http` format

## Features

- **Multiple Sources**: Support for local files and remote URLs
- **Smart Naming**: Automatically generates meaningful output file names from URLs
- **Timeout Control**: Configurable HTTP timeout for downloading specs
- **Authentication Hints**: Adds commented authentication headers when security is required
- **Validation**: Comprehensive OpenAPI specification validation
- **Verbose Logging**: Detailed output for debugging and monitoring
- **Error Handling**: Graceful handling of network issues and invalid specs

## Requirements

- .NET 9.0 or later

## Uninstall

```bash
dotnet tool uninstall --global OpenApi2Http
```

## Development

To build and test locally:

```bash
# Build the project
dotnet build

# Run locally
dotnet run -- -f examples/petstore.yaml

# Create package
dotnet pack

# Install local package
dotnet tool install --global --add-source ./bin/Debug OpenApi2Http
```

## License

MIT License - see LICENSE file for details.