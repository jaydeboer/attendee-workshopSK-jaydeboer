using Microsoft.AspNetCore.SignalR;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GloboTicket.Frontend.Services.AI;

public record UserRequest(string SessionId, string Message);

/// <summary>
/// Wolverine handler that processes chat requests in the background and streams
/// responses back to clients via SignalR.
/// </summary>
public static class ChatRequestHandler
{
    public static async Task Handle(
        UserRequest request,
        IHubContext<ChatHub> hubContext,
        IKernelBuilder builder,
        CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Client(request.SessionId)
            .SendAsync("NewResponse", cancellationToken);

        var agent = ChatAssistant.CreateChatAssistantAgent(builder.Build());
        var thread = AgentThreadRepository.GetOrCreateThread(request.SessionId);

        var stream = agent.InvokeStreamingAsync(
            [new ChatMessageContent(AuthorRole.User, request.Message)],
            thread, cancellationToken: cancellationToken);

        await foreach (var chunk in stream.WithCancellation(cancellationToken))
        {
            if (chunk.Message.Content != null)
            {
                await hubContext.Clients
                    .Client(request.SessionId)
                    .SendAsync("ReceiveMessagePart", chunk.Message.Content, cancellationToken);
            }
        }

        await hubContext.Clients
            .Client(request.SessionId)
            .SendAsync("ResponseDone", cancellationToken);
    }
}
