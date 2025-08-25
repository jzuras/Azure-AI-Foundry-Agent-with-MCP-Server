using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace TestMcpAgent;

internal class Program
{
    static async Task Main(string[] args)
    {
        await RunCompatibleAsyncAgentFlow();
    }

    /// <summary>
    /// Runs a complete agent interaction using an MCP tool, combining all asynchronous
    /// steps from the sample documentation into a single, robust method.
    /// </summary>
    public static async Task RunCompatibleAsyncAgentFlow()
    {
        // --- 1. Initialization and Environment Setup ---
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
            .Build();

        var projectEndpoint = configuration["ProjectEndpoint"];
        var modelDeploymentName = configuration["ModelDeploymentName"];
        var mcpServerUrl = configuration["McpServerUrl"];

        var mcpServerLabel = "ngrok";

        if (string.IsNullOrEmpty(projectEndpoint) || string.IsNullOrEmpty(modelDeploymentName) || string.IsNullOrEmpty(mcpServerUrl) || string.IsNullOrEmpty(mcpServerLabel))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: One or more required environment variables are not set.");
            Console.WriteLine("Please set: PROJECT_ENDPOINT, MODEL_DEPLOYMENT_NAME, MCP_SERVER_URL, MCP_SERVER_LABEL");
            Console.ResetColor();
            return;
        }

        // --- 2. Create the Agent Client ---
        // Uses DefaultAzureCredential, so ensure you are logged in via Azure CLI (`az login`).
        // Note: DefaultAzureCredential can throw a null exception when using the debugger, so I
        // switched to AzureCliCredential for this reaspm.
        PersistentAgentsClient agentClient = new(projectEndpoint, new AzureCliCredential());

        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;

        try
        {
            // --- 3. Create the MCP Tool Definition ---
            Console.WriteLine($"Defining MCP tool '{mcpServerLabel}' pointing to '{mcpServerUrl}'...");
            MCPToolDefinition mcpTool = new(mcpServerLabel, mcpServerUrl);

            // Optional: Configure allowed tools. This name must match a tool on the MCP server.
            // If this is used, other tools within the MCP server will not be available to the agent.
            //string toolName = "get_panel_serials";
            //mcpTool.AllowedTools.Add(toolName);

            // --- 4. Create the Agent (Async) ---
            Console.WriteLine("Creating agent in Azure...");
            agent = await agentClient.Administration.CreateAgentAsync(
               model: modelDeploymentName,
               name: "my-mcp-agent-async",
               instructions: "You are a helpful agent that can use MCP tools to assist users. Use the available MCP tools to answer questions and perform tasks.",
               tools: [mcpTool]
            );
            Console.WriteLine($"Agent created with ID: {agent.Id}");

            // --- 5. Create Thread, Message, and Run (Async) ---
            Console.WriteLine("Creating conversation thread...");
            thread = await agentClient.Threads.CreateThreadAsync();
            Console.WriteLine($"Thread created with ID: {thread.Id}");

            await agentClient.Messages.CreateMessageAsync(
                thread.Id,
                MessageRole.User,
                "List the MCP tools that I made available to you.");

            MCPToolResource mcpToolResource = new(mcpServerLabel);

            // Note that the token for an in-memory Auth Server will need to be manually updated each time the server is restarted.
            // These are also short-lived tokens, so they need to be updates periodically.
            // Production code would use a more robust authentication mechanism.
            // Also note - this header also has to be set for tool approvals, but this console app does not demonstrate that.
            mcpToolResource.UpdateHeader("Authorization", "Bearer [get from Curl Command]");
            
            ToolResources toolResources = mcpToolResource.ToToolResources();

            Console.WriteLine("Starting agent run...");
            ThreadRun run = await agentClient.Runs.CreateRunAsync(thread, agent, toolResources);

            run = await HandleRunExecutionAndToolApprovalsAsync(run, agentClient, thread);

            string followUpMessage = "Which tool is your favorite one?";
            Console.WriteLine($"\n--- Adding Follow-Up Message: '{followUpMessage}' ---\n");

            // Add another message to the thread.
            await agentClient.Messages.CreateMessageAsync(
                thread.Id,
                MessageRole.User,
                followUpMessage
            );

            run = await agentClient.Runs.CreateRunAsync(thread, agent, toolResources);

            run = await HandleRunExecutionAndToolApprovalsAsync(run, agentClient, thread);

            // --- 7. Print Run Steps and Messages (Async) ---
            Console.WriteLine("\n--- Run Activity Steps ---");
            // The GetRunSteps method itself is synchronous but returns a pageable result.
            // Here we convert it to a list using a collection expression.
            IReadOnlyList<RunStep> runSteps = [.. agentClient.Runs.GetRunSteps(run: run)];
            PrintActivitySteps(runSteps);

            Console.WriteLine("--------------------------------\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"An unhandled exception occurred: {ex}");
            Console.ResetColor();
        }
        finally
        {
            // --- 8. Clean Up Resources (Async) ---
            // This block ensures resources are deleted even if an error occurs.
            if (thread != null)
            {
                Console.WriteLine($"Deleting thread: {thread.Id}");
                await agentClient.Threads.DeleteThreadAsync(threadId: thread.Id);
            }
            if (agent != null)
            {
                Console.WriteLine($"Deleting agent: {agent.Id}");
                await agentClient.Administration.DeleteAgentAsync(agentId: agent.Id);
            }
            Console.WriteLine("Cleanup complete.");
        }
    }

    #region Helper Methods from Sample

    private static async Task<ThreadRun> HandleRunExecutionAndToolApprovalsAsync(ThreadRun run, PersistentAgentsClient agentClient, PersistentAgentThread thread)
    {
        // --- 6. Handle Run Execution and Tool Approvals (Async Polling Loop) ---
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
                            Headers = { ["SuperSecret"] = "123456" }
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
        }

        // Print last message from the run
        PersistentThreadMessage message = await agentClient.Messages.GetMessagesAsync(
            threadId: thread.Id,
            order: ListSortOrder.Descending
        ).FirstAsync();
        Console.WriteLine("\n--- Last Response ---");
        PrintSingleMessage(message);
        Console.WriteLine("");

        return run;
    }

    private static void PrintActivitySteps(IReadOnlyList<RunStep> runSteps)
    {
        foreach (RunStep step in runSteps)
        {
            if (step.StepDetails is RunStepActivityDetails activityDetails)
            {
                foreach (RunStepDetailsActivity activity in activityDetails.Activities)
                {
                    foreach (KeyValuePair<string, ActivityFunctionDefinition> activityFunction in activity.Tools)
                    {
                        Console.WriteLine($"The function {activityFunction.Key} with description \"{activityFunction.Value.Description}\" will be called.");
                        if (activityFunction.Value.Parameters.Properties.Count > 0)
                        {
                            Console.WriteLine("Function parameters:");
                            foreach (KeyValuePair<string, FunctionArgument> arg in activityFunction.Value.Parameters.Properties)
                            {
                                Console.WriteLine($"\t{arg.Key}");
                                Console.WriteLine($"\t\tType: {arg.Value.Type}");
                                if (!string.IsNullOrEmpty(arg.Value.Description))
                                    Console.WriteLine($"\t\tDescription: {arg.Value.Description}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("This function has no parameters");
                        }
                    }
                }
            }
        }
    }

    private static void PrintSingleMessage(PersistentThreadMessage message)
    {
            Console.Write($"{message.CreatedAt:yyyy-MM-dd HH:mm:ss} - {message.Role,10}: ");
            foreach (MessageContent contentItem in message.ContentItems)
            {
                if (contentItem is MessageTextContent textItem)
                {
                    Console.Write(textItem.Text);
                }
                else if (contentItem is MessageImageFileContent imageFileItem)
                {
                    Console.Write($"<image from ID: {imageFileItem.FileId}>");
                }
            }
            Console.WriteLine();
    }

    #endregion
}
