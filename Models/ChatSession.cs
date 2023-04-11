using Newtonsoft.Json;

namespace Cosmos.Chat.GPT.Models;

public class ChatSession
{
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; }

    public string Type { get; set; }

    public string ChatSessionId { get; set; } //partition key

    public string ChatSessionName { get; set; }

    [JsonIgnore]
    public List<ChatMessage> Messages { get; set; }

    public ChatSession()
    {
        this.Id = Guid.NewGuid().ToString();
        this.Type = "ChatSession";
        this.ChatSessionId = this.Id;
        this.ChatSessionName = "New Chat";
        this.Messages = new List<ChatMessage>();
    }

    public void AddMessage(ChatMessage message)
    {

        Messages.Add(message);
    }
}