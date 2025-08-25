# Azure AI Foundry Agent / MCP Server / MS Teams Bot Project (Beta/Preview SDKs)

A learning project demonstrating integration between Azure AI Foundry models/agents, MCP (Model Context Protocol) servers, and Microsoft Teams bots using preview and beta versions of various SDKs.

## Overview

This project showcases five different AI interaction approaches:
- **Base Model** - Azure AI Foundry model with full conversation history
- **Goldfish Model** - Azure AI Foundry model with no memory 
- **Claude Code** - Local Claude Code with local Enphase MCP Server (stdio transport)
- **Azure Agent** - Azure AI Foundry Agent without MCP tools
- **MCP Agent** - Azure AI Foundry Agent with Enphase MCP Server access (HTTP transport via ngrok)

The Teams Bot serves as a unified interface to interact with all these AI variants through simple command prefixes.

## Project Structure

### Quote.Agent (Main Teams Bot)
- **Program.cs** - ASP.NET Core startup configuration with Teams middleware
- **MainController.cs** - Teams bot controller handling message routing and AI integrations
- Supports all five AI interaction modes with proper resource cleanup using the Dispose pattern

### TestMcpAgent (Standalone Console App)
- **Program.cs** - Demonstrates Azure AI Foundry Agent with MCP Server integration
- Two-part conversation example for testing agent functionality
- Comprehensive error handling and resource cleanup

## Key Features Demonstrated

### Azure AI Foundry Integration
- Base model chat with conversation history
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

- .NET 9.0
- Azure CLI (`az login` required)
- Azure AI Foundry project with deployed model
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

**Example:** `mcp When did I first generate 200W yesterday?`

### Console App
Run `TestMcpAgent.exe` for a standalone demonstration of:
1. Agent creation with MCP tool integration
2. Two-part conversation flow
3. Tool approval handling
4. Resource cleanup

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
- Resource initialization is synchronous for base models but asynchronous for agents


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