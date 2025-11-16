using Microsoft.AspNetCore.SignalR;
using Wolverine;

namespace GloboTicket.Frontend.Services.AI;

/// <summary>
/// SignalR Hub for chat communication.
/// </summary>
public class ChatHub(IMessageBus messageBus) : Hub
{
    /// <summary>
    /// Method invoked by clients to send a message to the chat assistant.
    /// </summary>
    /// <param name="message">The user message/prompt</param>
    public async Task SendMessage(string message)
    {
        // Dispatch message to a background handler via Wolverine message bus
        await messageBus.SendAsync(new UserRequest(Context.ConnectionId, message));

        // Response will be streamed back via SignalR from the background handler
        // This will keep the web app responsive and scalable
    }
}
