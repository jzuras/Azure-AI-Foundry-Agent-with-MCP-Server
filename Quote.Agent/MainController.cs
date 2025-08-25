using System.Diagnostics;

using Azure;
using Azure.AI.Agents.Persistent;
using Azure.AI.OpenAI;
using Azure.Identity;

using Microsoft.Teams.Api.Activities;
using Microsoft.Teams.Api.Messages;
using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Activities;
using Microsoft.Teams.Apps.Annotations;

using OpenAI.Chat;

namespace Quote.Agent;

[TeamsController("main")]
public class MainController : IAsyncDisposable
{
    private ChatClient ChatClient { get; set; } = null!;
    private List<ChatMessage> MessagesForModel { get; set; } = null!;
    private bool AreAgentsInitialized { get; set; } = false;
    private PersistentAgentsClient AgentClient { get; set; } = null!;
    private PersistentAgent Agent { get; set; } = null!;
    private PersistentAgent AgentWithMcp { get; set; } = null!;
    private PersistentAgentThread Thread { get; set; } = null!;
    private PersistentAgentThread ThreadWithMcp { get; set; } = null!;
    private ToolResources ToolResources { get; set; } = null!; // Only used for MCP agent.

    public MainController()
    {
        this.InitializeBaseModel();

        // Note - the sample code from the Azure AI Foundry portal was synchronous for the base model chat, so it can be used in a constructor.
        // I chose to use the async version of the agent init sample, so the agents will be initialized in the OnMessage method instead.
    }

    private void InitializeBaseModel()
    {
        // Note - both models (including the goldfish model) can share the same ChatClient because
        // the goldfish model does not use chat history.


        // --- 1. Initialization and Environment Setup ---

        // Note - for production code, there are better ways to init these strings.
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
            .Build();

        var projectEndpoint = new Uri(configuration["ProjectEndpointForModel"] ?? "");
        var modelDeploymentName = configuration["ModelDeploymentName"];
        var apiKey = configuration["ApiKey"] ?? "";

        AzureOpenAIClient azureClient = new(
            projectEndpoint,
            new AzureKeyCredential(apiKey));

        this.ChatClient = azureClient.GetChatClient(modelDeploymentName);

        var requestOptions = new ChatCompletionOptions()
        {
            MaxOutputTokenCount = 4096,
            Temperature = 1.0f,
            TopP = 1.0f,
        };

        this.MessagesForModel = new List<ChatMessage>()
        {
            new SystemChatMessage("You are a helpful assistant model."),
        };
    }

    private async Task InitializeAgentsAsync()
    {
        // --- 1. Initialization and Environment Setup ---

        // Note - for production code, there are better ways to init these strings.
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
            .Build();

        var projectEndpoint = configuration["ProjectEndpointForAgent"];
        var modelDeploymentName = configuration["ModelDeploymentName"];

        var mcpServerUrl = configuration["McpServerUrl"];
        var mcpServerLabel = "EnphaseMcp";

        if (string.IsNullOrEmpty(projectEndpoint) || string.IsNullOrEmpty(modelDeploymentName) || string.IsNullOrEmpty(mcpServerUrl))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: One or more required environment variables are not set.");
            Console.WriteLine("Please set: PROJECT_ENDPOINT, MODEL_DEPLOYMENT_NAME, MCP_SERVER_URL");
            Console.ResetColor();

            return;
        }

        // --- 2. Create the Agent Client ---
        // Uses DefaultAzureCredential, so ensure you are logged in via Azure CLI(`az login`).
        // Note: DefaultAzureCredential can throw a null exception when using the debugger, so I
        // switched to AzureCliCredential for this reaspm.
        this.AgentClient = new(projectEndpoint, new AzureCliCredential());

        try
        {
            // --- 3. Create the MCP Tool Definition ---
            Console.WriteLine($"Defining MCP tool '{mcpServerLabel}' pointing to '{mcpServerUrl}'...");
            MCPToolDefinition mcpTool = new(mcpServerLabel, mcpServerUrl);

            // Optional: Configure allowed tools. This name must match a tool on the MCP server.
            // If this is used, other tools within the MCP server will not be available to the agent.
            //string toolName = "list_csv_files";
            //mcpTool.AllowedTools.Add(toolName);

            // --- 4. Create the Agent (Async) ---
            Console.WriteLine("Creating agents in Azure...");
            
            this.Agent = await this.AgentClient.Administration.CreateAgentAsync(
               model: modelDeploymentName,
               name: "My Agent",
               instructions: "You are a helpful agent that can assist users."
            );

            this.AgentWithMcp = await this.AgentClient.Administration.CreateAgentAsync(
               model: modelDeploymentName,
               name: "My Agent with MCP",
               instructions: "You are a helpful agent that can use MCP tools to assist users. Use the available MCP tools to answer questions and perform tasks.",
               tools: [mcpTool]
            );

            Console.WriteLine($"Agent created with ID: {this.Agent.Id}");
            Console.WriteLine($"MCP Agent created with ID: {this.AgentWithMcp.Id}");

            // --- 5. Create Thread, Message, and Run (Async) ---
            Console.WriteLine("Creating conversation threads...");
            this.Thread = await this.AgentClient.Threads.CreateThreadAsync();
            this.ThreadWithMcp = await this.AgentClient.Threads.CreateThreadAsync();
            Console.WriteLine($"Thread created with ID: {this.Thread.Id}");
            Console.WriteLine($"Thread for MCP Agent created with ID: {this.ThreadWithMcp.Id}");

            MCPToolResource mcpToolResource = new(mcpServerLabel);

            // Note that the token for an in-memory Auth Server will need to be manually updated each time the server is restarted.
            // These are also short-lived tokens, so they need to be updates periodically.
            // Production code would use a more robust authentication mechanism.
            // Also note - this header also has to be set for tool approvals in HandleMcpRequestAsync method.
            // A curl command similar to below can be used to get a new token:
            // curl -k -X POST [ngrok-url/token] -d "grant_type=client_credentials&client_id=demo-client&client_secret=demo-secret&resource=[ngrok-url/mcp/]"
            mcpToolResource.UpdateHeader("Authorization", "Bearer [get from Curl Command]");

            #region Usage notes for the RequireApproval Property

            // The RequireApproval property was not necessary for this demo code, but I tried it
            // out of curiosity and ran into some issues, so I am documenting it here for future reference.

            // The simplest usage, setting to "never", is shown below. This works fine.
            //mcpToolResource.RequireApproval = BinaryData.FromObjectAsJson("never");

            // However, if you want to approve specific tools only, you have to use a more complex structure,
            // and this is not simple to do correctly based on the documentation and examples provided.
            // The code below will fail at runtime in the beta version currently used by this project.
            // I created a GH Issue to get this clarified/fixed in future versions of the SDK:
            // https://github.com/Azure/azure-sdk-for-net/issues/52213
            //MCPApprovalPerTool mcpApprovalPerTool = new()
            //{
            //    Never = new MCPToolList(new List<string>() { "list_csv_files" })
            //};
            //mcpToolResource.RequireApproval = BinaryData.FromObjectAsJson(mcpApprovalPerTool);

            // "Always" is another Property of MCPApprovalPerTool class, also of type MCPToolList,
            // but it is the default value anyway.

            // Here is the fix for the runtime error when using the MCPApprovalPerTool class.
            // 1. Create the strongly-typed SDK model instance.
            //var approvalConfig = new MCPApprovalPerTool()
            //{
            //    Never = new MCPToolList(new List<string>() { "list_csv_files" })
            //};

            // 2. Use the model's 'Write' method with the correct options object.
            //    We instantiate ModelReaderWriterOptions with "W" for the wire format.
            //BinaryData correctlySerializedData = ((IPersistableModel<MCPApprovalPerTool>)approvalConfig)
            //    .Write(new ModelReaderWriterOptions("W"));

            // 3. Assign the perfectly serialized data to the property.
            //mcpToolResource.RequireApproval = correctlySerializedData;

            #endregion


            this.ToolResources = mcpToolResource.ToToolResources();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"An unhandled exception occurred: {ex}");
            Console.ResetColor();
        }
    }

    [Message]
    public async Task OnMessage([Context] MessageActivity activity, [Context] IContext.Client client)
    {
        // Sends a typing indicator, which renders as an animated ellipsis (…) in the chat.
        await client.Typing();

        // Note - for production code this would need to have a lock around it.
        if(this.AreAgentsInitialized == false)
        {
            System.Console.WriteLine("");
            System.Console.WriteLine("--------------------- initing agents -------------------------");
            System.Console.WriteLine("");

            await this.InitializeAgentsAsync();
            this.AreAgentsInitialized = true;
        }

        var userMessage = activity.Text;

        switch (true)
        {
            case var _ when userMessage.StartsWith("model", StringComparison.OrdinalIgnoreCase):
                await this.HandleModelRequestAsync(userMessage, client);
                break;

            case var _ when userMessage.StartsWith("goldfish", StringComparison.OrdinalIgnoreCase):
                await this.HandleGoldfishModelRequestAsync(userMessage, client);
                break;

            case var _ when userMessage.StartsWith("claude", StringComparison.OrdinalIgnoreCase):
                await this.HandleClaudeRequestAsync(userMessage, client);
                break;

            case var _ when userMessage.StartsWith("agent", StringComparison.OrdinalIgnoreCase):
                await this.HandleAgentRequestAsync(userMessage, client);
                break;

            case var _ when userMessage.StartsWith("mcp", StringComparison.OrdinalIgnoreCase):
                await this.HandleMcpRequestAsync(userMessage, client);
                break;

            default:
                await this.HelpResponseAsync(client);
                break;
        }
    }

    private async Task HelpResponseAsync(IContext.Client client)
    {
        var helpText = "**Choose your AI by starting your prompt with:**\n\n" +
                       "🧠 **model** - Chat against a base model (full conversation history -> memory)\n" +
                       "🐠 **goldfish** - Chat against a base model (only last question -> no memory)\n" +
                       "🤖 **claude** - Local Claude Code + local Enphase data MCP Server (stdio) (no chat memory)\n" +
                       "☁️ **agent** - Azure AI Foundry (no Enphase knowledge)\n" +
                       "🔧 **mcp** - Azure AI Foundry AI Agent + local Enphase data MCP Server (http via ngrok)\n\n" +
                       "**Example:** `claude When did I first generate 200W yesterday?`";

        await client.Send(helpText);
    }

    private async Task HandleModelRequestAsync(string userMessage, IContext.Client client)
    {
        // Append new user question.
        this.MessagesForModel.Add(new UserChatMessage(userMessage));

        var response = this.ChatClient.CompleteChat(this.MessagesForModel);
        System.Console.WriteLine(response.Value.Content[0].Text);

        var activity = new MessageActivity(response.Value.Content[0].Text).AddAIGenerated();

        await client.Send(activity);

        // Append the model response to the chat history.
        this.MessagesForModel.Add(new AssistantChatMessage(response.Value.Content[0].Text));
    }

    private async Task HandleGoldfishModelRequestAsync(string userMessage, IContext.Client client)
    {
        // Goldfish memory - will reset messages rather than appending to them,
        // so just use local variable here.

        List<ChatMessage>  messages = new List<ChatMessage>()
        {
            new SystemChatMessage("You are a Goldfish Model."),
        };

        // Append new user question.
        messages.Add(new UserChatMessage(userMessage));

        var response = this.ChatClient.CompleteChat(messages);
        System.Console.WriteLine(response.Value.Content[0].Text);

        var activity = new MessageActivity(response.Value.Content[0].Text).AddAIGenerated();

        await client.Send(activity);
    }

    private async Task HandleClaudeRequestAsync(string userMessage, IContext.Client client)
    {
        var command = $"wsl bash -i -c \"claude -p 'User asks: {userMessage}' --dangerously-skip-permissions\"";

        Console.WriteLine();
        Console.WriteLine($"Executing: {command}");
        Console.WriteLine();

        var processInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Normal
        };

        using var process = Process.Start(processInfo);
        if (process is null)
        {
            await client.Send("Failed to start the process.");
            return;
        }

        var claudeResponse = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var activity = new MessageActivity(claudeResponse).AddAIGenerated();

        await client.Send(activity);
    }

    private async Task HandleAgentRequestAsync(string userMessage, IContext.Client client)
    {
        // Note - the client being passed in is not the agent client, it is the Teams client.

        // Send the user's prompt to the agent.
        this.AgentClient.Messages.CreateMessage(
            this.Thread.Id,
            MessageRole.User,
            userMessage);

        // Have Agent begin processing user's question (no additional instructions associated with the ThreadRun).
        ThreadRun run = this.AgentClient.Runs.CreateRun(
            this.Thread.Id,
            this.Agent.Id,
            additionalInstructions: "");

        // Wait for completion. Not handling tool approvals here because this agent has no tools.
        do
        {
            await Task.Delay(1000);
            run = this.AgentClient.Runs.GetRun(this.Thread.Id, run.Id);
        }
        while (run.Status == RunStatus.Queued
            || run.Status == RunStatus.InProgress
            || run.Status == RunStatus.RequiresAction);

        // Get the response back to the user.

        // The last message from the run is the agent's response.
        PersistentThreadMessage message = await this.AgentClient.Messages.GetMessagesAsync(
            threadId: this.Thread.Id,
            order: ListSortOrder.Descending
        ).FirstAsync();

        string responseText = string.Join("", message.ContentItems
            .OfType<MessageTextContent>()
            .Select(textContent => textContent.Text));

        var activity = new MessageActivity(responseText).AddAIGenerated();

        await client.Send(activity);
    }

    private async Task HandleMcpRequestAsync(string userMessage, IContext.Client client)
    {
        // Note - the client being passed in is not the agent client, it is the Teams client.

        // Send the user's prompt to the agent.
        this.AgentClient.Messages.CreateMessage(
            this.ThreadWithMcp.Id,
            MessageRole.User,
            userMessage);

        // Have Agent begin processing user's question.
        ThreadRun run = this.AgentClient.Runs.CreateRun(
            this.ThreadWithMcp,
            this.AgentWithMcp,
            this.ToolResources);

        // --- 6. Handle Run Execution and Tool Approvals (Async Polling Loop) ---
        string errorString = await HandleRunExecutionAndToolApprovalsAsync(run, this.AgentClient, this.ThreadWithMcp);

        // Get the response back to the user.
        if (string.IsNullOrEmpty(errorString) is true)
        {
            // The last message from the run is the agent's response.
            PersistentThreadMessage message = await this.AgentClient.Messages.GetMessagesAsync(
                threadId: this.ThreadWithMcp.Id,
                order: ListSortOrder.Descending
            ).FirstAsync();

            string responseText = string.Join("", message.ContentItems
                .OfType<MessageTextContent>()
                .Select(textContent => textContent.Text));

            var activity = new MessageActivity(responseText).AddAIGenerated();

            await client.Send(activity);
        }
        else
        {
            await client.Send(errorString);
        }
    }

    private static async Task<string> HandleRunExecutionAndToolApprovalsAsync(ThreadRun run, PersistentAgentsClient agentClient, PersistentAgentThread thread)
    {
        // Important Note: if approval is NOT required for an MCP tool, the RequiresAction will not be triggered.
        // This means that the code which sets the Authorization header for the tool approval will not be executed here,
        // but it appears that the SDK will automatically pass the header from the MCPToolResource object when it calls the MCP tool.
        // This header was set when the MCPToolResource object was created during agent initialization.

        Console.WriteLine($"Run started with ID: {run.Id}. Polling for status...");
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress || run.Status == RunStatus.RequiresAction)
        {
            await Task.Delay(1000);
            run = await agentClient.Runs.GetRunAsync(thread.Id, run.Id);
            Console.WriteLine($" > Run status: {run.Status}");

            if (run.Status == RunStatus.RequiresAction && run.RequiredAction is SubmitToolApprovalAction toolApprovalAction)
            {
                var toolApprovals = new List<ToolApproval>();
                foreach (var toolCall in toolApprovalAction.SubmitToolApproval.ToolCalls)
                {
                    if (toolCall is RequiredMcpToolCall mcpToolCall)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Approving MCP tool call -> Name: {mcpToolCall.Name}, Arguments: {mcpToolCall.Arguments}");
                        Console.ResetColor();
                        toolApprovals.Add(new ToolApproval(mcpToolCall.Id, approve: true)
                        {
                            Headers = { ["Authorization"] = "Bearer [get from Curl Command]" }
                        });
                    }
                }
                if (toolApprovals.Count > 0)
                {
                    Console.WriteLine("Submitting tool approvals to the run...");
                    run = await agentClient.Runs.SubmitToolOutputsToRunAsync(thread.Id, run.Id, toolApprovals: toolApprovals);
                }
            }
        }

        Console.WriteLine($"Run finished with status: {run.Status}.");
        if (run.Status != RunStatus.Completed)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Run failed or was cancelled. Last Error: {run.LastError?.Message}");
            Console.ResetColor();

            // Return the error string.
            return $"Error: Run failed or was cancelled. Last Error: {run.LastError?.Message}";
        }

        // Return empty string to indicate no error.
        return string.Empty;
    }

    #region Dispose Pattern
    // Coding standard exception - only used here so not included at the top, and uses the "_" which I hate.
    private bool _isDisposed;

    public async ValueTask DisposeAsync()
    {
        if (this._isDisposed is false)
        {
            // --- 8. Clean Up Resources (Async) ---
            // This block ensures resources are deleted when the controller is disposed.
            if (this.Thread != null)
            {
                Console.WriteLine($"Deleting thread: {this.Thread.Id}");
                await this.AgentClient.Threads.DeleteThreadAsync(threadId: this.Thread.Id);
            }
            if (this.ThreadWithMcp != null)
            {
                Console.WriteLine($"Deleting thread: {this.ThreadWithMcp.Id}");
                await this.AgentClient.Threads.DeleteThreadAsync(threadId: this.ThreadWithMcp.Id);
            }
            if (this.Agent != null)
            {
                Console.WriteLine($"Deleting agent: {this.Agent.Id}");
                await this.AgentClient.Administration.DeleteAgentAsync(agentId: this.Agent.Id);
            }
            if (this.AgentWithMcp != null)
            {
                Console.WriteLine($"Deleting agent: {this.AgentWithMcp.Id}");
                await this.AgentClient.Administration.DeleteAgentAsync(agentId: this.AgentWithMcp.Id);
            }

            Console.WriteLine("Cleanup complete.");

            this._isDisposed = true;
        }
    }
    #endregion
}
