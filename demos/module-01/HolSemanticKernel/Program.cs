using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

var model = config["OpenAI:Model"];
var endpoint = config["OpenAI:EndPoint"];
var token = config["OpenAI:ApiKey"];

Console.WriteLine($"Model: {model}");
Console.WriteLine($"Endpoint: {endpoint}");

var client = new ChatCompletionsClient(
    new Uri(endpoint),
    new AzureKeyCredential(token),
    new AzureAIInferenceClientOptions());

var requestOptions = new ChatCompletionsOptions()
{
    Model = model,
    Messages =
    [
        new ChatRequestUserMessage("Tell me a joke about computers")
    ]
};

var resp = await client.CompleteAsync(requestOptions);
Console.WriteLine(resp.Value.Content);