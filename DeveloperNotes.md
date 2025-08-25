# Developer Notes - Azure AI Foundry Agent / MCP Server / MS Teams Bot

This document provides detailed setup instructions, development insights, and troubleshooting information for developers wanting to understand or replicate this project.

## Getting Started from Scratch

### Prerequisites
- **Windows 11** (required for Claude Code integration via WSL)
- **.NET 9.0 SDK**
- **Visual Studio 2022** or VS Code with C# extension
- **Azure CLI** - Install and run `az login`
- **Node.js and npm** (for Teams Toolkit if installing to Teams)
- **WSL2** with a Linux distribution (for Claude Code)
- **ngrok** - For exposing local MCP server to Azure

### Azure Setup
1. **Azure Account** - Ensure you have appropriate permissions for:
   - Creating Azure AI Foundry projects
   - Deploying models
   - Creating agents
   
2. **Azure CLI Authentication**
   ```bash
   az login
   ```
   Note: If you encounter permission issues with `DefaultAzureCredential()`, you may need to use `AzureKeyCredential` with an API key instead.

### MCP Server Setup
This project requires the OAuth-Protected MCP Server running locally:

1. **Clone the MCP Server Repository**
   ```bash
   git clone https://github.com/jzuras/OAuth-Protected-MCP-Server
   ```

2. **Follow the setup instructions** in that repository's README

3. **Start the MCP Server** - It should be running on localhost before proceeding

### ngrok Configuration
1. **Install ngrok** from https://ngrok.com/
2. **Expose your MCP server**:
   ```bash
   ngrok http [your-mcp-server-port]
   ```
3. **Copy the HTTPS URL** - You'll need this for configuration

### Claude Code Installation
1. **Install Claude Code** following instructions at https://claude.ai/code
2. **Ensure WSL integration** is working
3. **Test Claude Code** can access your local Enphase MCP Server

## Configuration and Setup

### appsettings.json Configuration
Create `appsettings.json` and `appsettings.development.json` files:

```json
{
  "ProjectEndpointForModel": "https://your-foundry-project.openai.azure.com/",
  "ProjectEndpointForAgent": "https://your-foundry-project.cognitiveservices.azure.com/",
  "ModelDeploymentName": "gpt-4o",
  "ApiKey": "your-azure-ai-foundry-api-key",
  "McpServerUrl": "https://your-ngrok-url.ngrok-free.app/mcp/"
}
```

### Authentication Token Generation
You'll need to generate a JWT token for MCP server authentication:

```bash
curl -k -X POST https://your-ngrok-url.ngrok-free.app/token \
  -d "grant_type=client_credentials&client_id=demo-client&client_secret=demo-secret&resource=https://your-ngrok-url.ngrok-free.app/mcp/"
```

### Bearer Token Placement
The generated JWT token must be placed in **3 locations** (search for 'bearer'):

1. **MainController.cs line 155** - `mcpToolResource.UpdateHeader()` call
2. **MainController.cs line 448** - Tool approval headers
3. **TestMcpAgent/Program.cs line 88** - `mcpToolResource.UpdateHeader()` call

**Important:** Tokens are short-lived and need periodic updates during development.

### Teams App Setup
This project includes Teams Toolkit configuration, but installation to Teams may fail with personal Microsoft 365 accounts. Instead:

1. **Use DevTools browser option** from the launch profile
2. **Or test via the console application** for MCP functionality

## Quickstarts and Sample Code Used

### Primary Quickstarts Followed

1. **Teams AI Library v2 (Preview)**
   - https://learn.microsoft.com/en-us/microsoftteams/platform/teams-ai-library/welcome
   - Source of the "Quote.Agent" project name
   - Provides the Teams bot foundation

2. **Azure AI Foundry Project Setup**
   - https://learn.microsoft.com/en-us/azure/ai-foundry/quickstarts/get-started-code?tabs=csharp&pivots=fdp-project
   - Used for creating Azure resources and model deployment
   - Contains base model connection examples

3. **MCP Integration with Agents**
   - https://learn.microsoft.com/en-us/azure/ai-foundry/agents/how-to/tools/model-context-protocol
   - Preview documentation for agent MCP integration
   - Code samples were fragmented and required assembly

### Code Assembly Process
The Microsoft Learn documentation breaks sample code into many fragments without providing complete examples. This project assembles those fragments into working implementations, which is why you'll see numbered comments in the code (e.g., `// --- 1. Initialization and Environment Setup ---`).

## Development Issues and Solutions

### Azure SDK Beta Version Challenges

**GitHub Issue Filed**: https://github.com/Azure/azure-sdk-for-net/issues/52213
- `MCPApprovalPerTool` class serialization issues
- Workaround provided in MainController.cs comments (lines 179-192)

### Authentication Issues

**Problem**: `DefaultAzureCredential()` connection failures
**Solution**: Use `AzureKeyCredential` with API key from Azure Portal
**Root Cause**: Likely Azure account permission configuration

**Problem**: `DefaultAzureCredential` null exceptions in debugger
**Solution**: Switch to `AzureCliCredential` for development

### Teams App Installation Problems
- Installation fails with personal Microsoft 365/Teams accounts
- Teams Toolkit "provision" may also fail
- **Workaround**: Use DevTools browser launch option for testing

### Rate Limiting with GPT-4o
- Even simple MCP operations can hit rate limits
- Consider alternative models or token optimization strategies
- Rate limit settings may need adjustment in Azure Portal

### MCP Server Configuration Requirements

**Critical Discovery**: MCP Server must be configured as **Stateless**
```csharp
// In MCP Server setup - REQUIRED for Azure AI Foundry integration
builder.Services.AddMcpServer()
            .WithTools<DataFileTool>()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            });
 ```

Without this configuration, the agent's MCP calls only work on the first attempt.

### Localhost vs ngrok for MCP Access

**Theory**: Azure AI Foundry SDK should be able to call localhost MCP servers directly
**Reality**: This doesn't work with current preview SDK
**Solution**: Use ngrok to provide public URL that forwards to localhost

The SDK includes `SubmitToolOutputsToRunAsync` method suggesting local tool execution, but this functionality appears incomplete in the current beta version.

## Advanced Configuration Notes

### MCP Tool Approval Patterns

**Simple Approval Settings:**
```csharp
// Never require approval for any tools
mcpToolResource.RequireApproval = BinaryData.FromObjectAsJson("never");

// Always require approval (default)
mcpToolResource.RequireApproval = BinaryData.FromObjectAsJson("always");
```

**Per-Tool Approval (Beta - Has Issues):**
The `MCPApprovalPerTool` class exists but has serialization problems. See MainController.cs lines 179-192 for the workaround.

### AllowedTools Behavior
```csharp
// If ANY tools are specified, ALL others are disallowed
mcpTool.AllowedTools.Add("list_csv_files");
// Now ONLY list_csv_files is available, all other MCP tools are blocked
```

### Authorization Header Handling
- **Initialization**: Set in `MCPToolResource.UpdateHeader()`
- **Tool Approval**: Set in `ToolApproval.Headers` when `RequiresAction` is triggered
- **Auto-handling**: When approval is not required, the SDK automatically uses the initialization header

## Testing and Usage Examples

### Teams Bot Command Examples
Start messages with these prefixes to test different AI backends (demo questions):

```
model i am jim who are you
model who am i
goldfish i am jim who are you
goldfish who am i
agent List the MCP tools that I made available to you
mcp List the MCP tools that I made available to you
mcp Using the 'system' file type, what are the dates available for my solar system data
```

### Console App Testing
1. Run `TestMcpAgent.exe`
2. Observe two-part conversation flow:
   - "List the MCP tools that I made available to you"
   - "Which tool is your favorite one?"
3. Check console output for tool approval workflow
4. Verify resource cleanup messages

### Debugging Tips
1. **Token Expiration**: Watch for 401/403 errors indicating token refresh needed
2. **ngrok Issues**: Ensure ngrok URL matches configuration in all locations
3. **Resource Cleanup**: Monitor Azure portal for orphaned agents/threads
4. **Rate Limits**: Implement retry logic or use simpler prompts during development

## Known Limitations and Workarounds

### Preview SDK Limitations
- `MCPApprovalPerTool` serialization requires manual intervention
- `DefaultAzureCredential` debugging issues
- Incomplete localhost MCP server support
- Resource initialization patterns differ between models and agents

### Token Management
- JWT tokens are short-lived (typically 1-2 hours)
- Manual token updates required during development
- Production implementations need automated token refresh

### Resource Management Considerations
- Azure resources (agents, threads) persist beyond application lifetime
- Implement proper disposal patterns to avoid orphaned resources
- Use try-finally blocks for cleanup in console applications
- Teams bot uses IAsyncDisposable for automatic cleanup

### Windows-Specific Requirements
- Claude Code integration requires WSL2
- Shell command execution uses Windows cmd.exe with WSL bash
- Cross-platform compatibility not tested

## Future Development Considerations

1. **Production Authentication**: Implement automated token refresh mechanism
2. **Error Handling**: Add comprehensive retry logic for rate limits
3. **Cross-Platform**: Test and adapt for non-Windows environments
4. **Resource Optimization**: Implement agent/thread pooling for high-volume scenarios
5. **Configuration Management**: Move hardcoded tokens to secure configuration
6. **Teams Installation**: Investigate enterprise Teams deployment options