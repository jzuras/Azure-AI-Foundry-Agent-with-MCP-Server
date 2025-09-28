using Microsoft.AI.Foundry.Local;
using OpenAI;
using System.ClientModel;

namespace TestLocalFoundryModel;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var alias = "phi-4-mini";

        var manager = await FoundryLocalManager.StartModelAsync(aliasOrModelId: alias);

        var model = await manager.GetModelInfoAsync(aliasOrModelId: alias);
        ApiKeyCredential key = new ApiKeyCredential(manager.ApiKey);
        OpenAIClient client = new OpenAIClient(key, new OpenAIClientOptions
        {
            Endpoint = manager.Endpoint
        });

        var chatClient = client.GetChatClient(model?.ModelId);

        var completionUpdates = chatClient.CompleteChat("Why is the sky blue'");

        Console.Write($"[ASSISTANT]: ");
        
        Console.WriteLine($"FinishReason: {completionUpdates.Value.FinishReason}");
        
        Console.Write(completionUpdates.Value.Content[0].Text);

        Console.WriteLine();
        Console.WriteLine($"[End of response]");
    }
}
