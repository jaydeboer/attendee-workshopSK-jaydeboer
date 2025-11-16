using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;

namespace modulerag;

internal class ChatWithAgent
{
    public async Task LetAgentFindRide(IConfiguration config)
    {
        var question =
        """
        I stay at the WestIn Seattle and the venue is the Seattle Kraken Stadium.
        The concert starts at 7:30 pm and is November 20th this year. 
        """;

        Console.WriteLine("******** Create the agent ***********");
        var transportationAgent = CreateTransportationAgent(config);
        transportationAgent.Kernel.ImportPluginFromType<RideInformationSystemService>();

        // get the thread so we can continue the conversation
        var thread = new ChatHistoryAgentThread();
        var agentResult = transportationAgent.InvokeAsync(question, thread);

        Console.WriteLine("******** RESPONSE 1 ***********");
        await PrintResult(agentResult);

        while (!await IsGoalReached(agentResult))
        {
            var input = Console.ReadLine();

            agentResult = transportationAgent.InvokeAsync(input, thread);

            Console.WriteLine("******** RESPONSE ***********");
            await PrintResult(agentResult);
        }
        Console.WriteLine("******** Terminating, goal reached ***********");
    }

    private async Task<bool> IsGoalReached(IAsyncEnumerable<AgentResponseItem<ChatMessageContent>> agentResponse)
    {
        await foreach(var item in agentResponse)
        {
            if(item.Message.Content.Contains("[** GOAL REACHED **]"))
            {
                return true;
            }
        }
        return false;
    }

    private ChatCompletionAgent CreateTransportationAgent(IConfiguration config)
    {
        var kernel = CreateKernel(config);

        var instructions = """
            You are an expert in finding transportation options from a given hotel location to the concert location.
            You will try to get the best options available for an affordable price. Make sure the customer will be there at least 30 minutes before the concert starts at the venue.
            You always suggest 3 options with different price ranges.
            You will ask for approval before you make the booking. 
            You are not allowed to make a booking without user confirmation!

            After you successfully booked the ride you will respond with [** GOAL REACHED **] in your message.            
            """; 

        ChatCompletionAgent transportationAgent =
            new()
            {
                Name = "TransportationAgent",
                Instructions = instructions,
                Description = "An agent that finds transportation options from hotel to concert location",
                Kernel = kernel,
                LoggerFactory = LoggerFactory.Create(builder =>
                    {
                        // Add Console logging provider
                        builder.AddConsole().SetMinimumLevel(LogLevel.Trace);
                    }),
                Arguments = new KernelArguments(new OpenAIPromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                }),
            };
        return transportationAgent;
    }


    private static Kernel CreateKernel(IConfiguration config)
    {
        var model = config["OpenAI:Model"];
        var endpoint = config["OpenAI:EndPoint"];
        var token = config["OpenAI:ApiKey"];

        var kernelBuilder = Kernel
            .CreateBuilder()
            .AddOpenAIChatCompletion(model, new Uri(endpoint), token);

        var kernel = kernelBuilder.Build();
        return kernel;
    }

    private static async Task PrintResult(IAsyncEnumerable<AgentResponseItem<ChatMessageContent>> agentResponse)
    {
        await
        foreach (var item in agentResponse)
        {
            Console.WriteLine($"Thread: {item.Thread.Id}");
            Console.WriteLine($"Thread data: {item.Thread}");
            Console.WriteLine($"Author: {item.Message.AuthorName}");
            Console.WriteLine($"Message:{item.Message}");
        }
    }
}