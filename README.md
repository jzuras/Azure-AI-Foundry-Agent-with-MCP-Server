# Azure AI Foundry Agent / MCP Server / MS Teams Bot Project (Beta/Preview SDKs)

A learning project demonstrating integration between Azure AI Foundry models/agents, MCP (Model Context Protocol) servers, and Microsoft Teams bots using preview and beta versions of various SDKs.

Please see my LinkedIn posts for more info:  
https://www.linkedin.com/feed/update/urn:li:activity:7365798255726579714/  
https://www.linkedin.com/feed/update/urn:li:activity:7378160986584670208/  

## Overview

This project showcases seven different AI interaction approaches:
- **Base Model** - Azure AI Foundry model with full conversation history
- **Goldfish Model** - Azure AI Foundry model with no memory
- **Claude Code** - Local Claude Code with local Enphase MCP Server (stdio transport)
- **Azure Agent** - Azure AI Foundry Agent without MCP tools
- **MCP Agent** - Azure AI Foundry Agent with Enphase MCP Server access (HTTP transport via ngrok)
- **Local Foundry** - Local Azure AI Foundry model with full conversation history
- **Local Foundry Goldfish** - Local Azure AI Foundry model with no memory

The Teams Bot serves as a unified interface to interact with all these AI variants through simple command prefixes.

## Project Structure

### Quote.Agent (Main Teams Bot)
- **Program.cs** - ASP.NET Core startup configuration with Teams middleware
- **MainController.cs** - Teams bot controller handling message routing and AI integrations
- Supports all seven AI interaction modes with proper resource cleanup using the Dispose pattern

### TestMcpAgent (Standalone Console App)
- **Program.cs** - Demonstrates Azure AI Foundry Agent with MCP Server integration
- Two-part conversation example for testing agent functionality
- Comprehensive error handling and resource cleanup

### TestLocalFoundryModel (Standalone Console App)
- **Program.cs** - Demonstrates Azure AI Foundry Local model integration
- Shows local model startup, inference, and management
- Uses phi-4-mini model for hardware compatibility

## Key Features Demonstrated

### Azure AI Foundry Integration
- Base model chat with conversation history
- Local model integration using Azure AI Foundry Local
- Agent creation and management
- Thread and message handling
- Asynchronous operations with proper polling

### MCP Server Integration
- HTTP transport protocol with authorization
- Stateless server configuration
- Tool definition and approval workflows
- Dynamic header management for authentication
- Optional tool filtering (allow specific tools only)

### Teams Bot Functionality
- Message routing based on command prefixes
- Multiple AI backend support
- Typing indicators and proper message formatting
- Integration with local Claude Code via WSL

### Authentication & Authorization
- Azure CLI credential integration
- JWT token management for MCP server access
- Authorization header handling for tool approvals
- Support for both automatic and manual tool approval flows

## Prerequisites

- .NET 9.0 (Quote.Agent, TestMcpAgent)
- .NET 10.0 (TestLocalFoundryModel)
- Azure CLI (`az login` required)
- Azure AI Foundry project with deployed model
- Azure AI Foundry Local (for local model support)
- Enphase MCP Server running locally
- ngrok for exposing local MCP server to Azure (for HTTP transport)
- Microsoft Teams for bot testing

## Configuration

The project uses `appsettings.json` and `appsettings.development.json` for configuration:

```json
{
  "ProjectEndpointForModel": "your-azure-ai-foundry-model-endpoint",
  "ProjectEndpointForAgent": "your-azure-ai-foundry-agent-endpoint", 
  "ModelDeploymentName": "your-model-deployment-name",
  "ApiKey": "your-api-key",
  "McpServerUrl": "your-ngrok-url-for-mcp-server"
}
```

## Usage

### Teams Bot Commands
Start your message with one of these prefixes:

- `model` - Chat with base model (full conversation history)
- `goldfish` - Chat with model (no memory)
- `claude` - Use local Claude Code with Enphase MCP
- `agent` - Use Azure AI Foundry Agent
- `mcp` - Use Azure Agent with MCP Server access
- `localfoundry` - Use local Azure AI Foundry model (full conversation history)
- `localfoundry-goldfish` - Use local Azure AI Foundry model (no memory)

**Examples:**
- `mcp When did I first generate 200W yesterday?`
- `localfoundry What can you tell me about renewable energy?`

### Console App
Run `TestMcpAgent.exe` for a standalone demonstration of:
1. Agent creation with MCP tool integration
2. Two-part conversation flow
3. Tool approval handling
4. Resource cleanup

### LocalFoundry Setup

If you wish to experiment with LocalFoundry:

1. **Install LocalFoundry**: Follow Microsoft's [Getting Started with LocalFoundry](https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-local/get-started) guide

2. **Pre-download the model** (recommended before running ArchGuard):
   ```
   foundry model run qwen2.5-0.5b
   ```
   This downloads the model locally and can take significant time on first run. Running this command first prevents timeouts during ArchGuard startup.

3. **Test your setup**: Use the LocalFoundry command-line chatbot or [AI Studio for VS Code](https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-local/concepts/foundry-local-architecture) to directly chat with the model to test performance.

## Technical Insights

### MCP Server Configuration
- HTTP transport requires stateless configuration
- Authorization tokens are short-lived and need periodic updates
- The same authorization header must be set both during initialization and tool approvals
- Tool approval is optional but enabled by default

### SDK Terminology
- "Tool" can refer to either the MCP Server itself or individual functions within the server
- AllowedTools property restricts access to specific server functions
- RequireApproval setting controls whether manual approval is needed for tool calls

### Resource Management
- Proper cleanup of Azure resources (threads, agents) using dispose pattern
- Automatic resource deletion on application shutdown
- Exception handling ensures cleanup even on failures

## Dependencies

### Preview/Beta Packages
- Azure.AI.Agents.Persistent (1.2.0-beta.2)
- Azure.AI.Inference (1.0.0-beta.5)
- Azure.AI.Projects (1.0.0-beta.10)
- Microsoft.AI.Foundry.Local (0.3.0)
- Microsoft.Teams.* packages (2.0.0-preview.*)

**Note:** These are preview/beta packages subject to breaking changes in future versions.

## Lessons Learned

1. **MCP Server Setup** - HTTP transport with authorization requires stateless configuration
2. **Authentication** - Authorization headers must be consistent between initialization and tool approvals
3. **Tool Management** - SDK provides granular control over which MCP server functions are accessible
4. **Resource Cleanup** - Critical for Azure resources to prevent orphaned objects
5. **Debugging** - DefaultAzureCredential can cause issues in debugger; AzureCliCredential is more reliable

## Known Issues

- JWT tokens for MCP server are short-lived and need manual updates
- RequireApproval configuration for specific tools has serialization issues in current beta (GitHub issue #52213)
- Resource initialization is synchronous for base models but asynchronous for agents and local foundry models


## Copyright and License

### Code

Copyright (�) 2025  Jzuras

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.


## Trademarks

Enphase(R) and Envoy(R) are trademarks of Enphase Energy(R).

All trademarks are the property of their respective owners.

Any trademarks used in this project are used in a purely descriptive manner and to state compatibility.