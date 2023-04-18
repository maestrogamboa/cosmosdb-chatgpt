﻿using Cosmos.Chat.GPT.Constants;
using Cosmos.Chat.GPT.Models;

namespace Cosmos.Chat.GPT.Services;

public class ChatService
{
    /// <summary>
    /// All data is cached in the _sessions List object.
    /// </summary>
    private static List<Session> _sessions = new();

    private readonly CosmosDbService _cosmosDbService;
    private readonly OpenAiService _openAiService;
    private readonly int _maxConversationLength;

    public ChatService(CosmosDbService cosmosDbService, OpenAiService openAiService)
    {
        _cosmosDbService = cosmosDbService;
        _openAiService = openAiService;

        _maxConversationLength = openAiService.MaxTokens / 2;
    }

    /// <summary>
    /// Returns list of chat session ids and names for left-hand nav to bind to (display Name and ChatSessionId as hidden)
    /// </summary>
    public async Task<List<Session>> GetAllChatSessionsAsync()
    {
        return _sessions = await _cosmosDbService.GetSessionsAsync();
    }

    /// <summary>
    /// Returns the chat messages to display on the main web page when the user selects a chat from the left-hand nav
    /// </summary>
    public async Task<List<Message>> GetChatSessionMessagesAsync(string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        List<Message> chatMessages = new();

        if (_sessions.Count == 0)
        {
            return Enumerable.Empty<Message>().ToList();
        }

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        if (_sessions[index].Messages.Count == 0)
        {
            // Messages are not cached, go read from database
            chatMessages = await _cosmosDbService.GetSessionMessagesAsync(sessionId);

            // Cache results
            _sessions[index].Messages = chatMessages;
        }
        else
        {
            // Load from cache
            chatMessages = _sessions[index].Messages;
        }

        return chatMessages;
    }

    /// <summary>
    /// User creates a new Chat Session.
    /// </summary>
    public async Task CreateNewChatSessionAsync()
    {
        Session session = new();

        _sessions.Add(session);

        await _cosmosDbService.InsertSessionAsync(session);

    }

    /// <summary>
    /// User Inputs a chat from "New Chat" to user defined.
    /// </summary>
    public async Task RenameChatSessionAsync(string? sessionId, string newChatSessionName)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        _sessions[index].Name = newChatSessionName;

        await _cosmosDbService.UpdateSessionAsync(_sessions[index]);
    }

    /// <summary>
    /// User deletes a chat session
    /// </summary>
    public async Task DeleteChatSessionAsync(string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        _sessions.RemoveAt(index);

        await _cosmosDbService.DeleteSessionAndMessagesAsync(sessionId);
    }

    /// <summary>
    /// User prompt to ask _openAiService a question
    /// </summary>
    public async Task<string> AskOpenAiAsync(string? sessionId, string prompt)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        Message promptMessage = await AddPromptMessageAsync(sessionId, prompt);

        string conversation = GetChatSessionConversation(sessionId, prompt);

        (string response, int promptTokens, int responseTokens) = await _openAiService.AskAsync(sessionId, conversation);

        await AddPromptResponseMessagesAsync(sessionId, promptTokens, responseTokens, promptMessage, response);

        return response;
    }

    /// <summary>
    /// Get current conversation with the user prompt added and truncated
    /// </summary>
    private string GetChatSessionConversation(string sessionId, string prompt)
    {
        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        string previousConversation = String.Join(Environment.NewLine, _sessions[index].Messages);
        string currentConversation = previousConversation + Environment.NewLine + prompt;

        return currentConversation.Length > _maxConversationLength ?
            currentConversation.Substring(currentConversation.Length - _maxConversationLength, _maxConversationLength) :
            currentConversation;
    }

    public async Task<string> SummarizeChatSessionNameAsync(string? sessionId, string prompt)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        string response = await _openAiService.SummarizeAsync(sessionId, prompt);

        await RenameChatSessionAsync(sessionId, response);

        return response;
    }

    /// <summary>
    /// Add human prompt to the chat session message list object and insert into the data service.
    /// </summary>
    private async Task<Message> AddPromptMessageAsync(string sessionId, string promptText)
    {
        Message promptMessage = new(sessionId, nameof(Participants.Human), default, promptText);

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        _sessions[index].AddMessage(promptMessage);

        return await _cosmosDbService.InsertMessageAsync(promptMessage);
    }

    /// <summary>
    /// Add human prompt and AI response to the chat session message list object and insert into the data service.
    /// </summary>
    private async Task AddPromptResponseMessagesAsync(string sessionId, int promptTokens, int responseTokens, Message promptMessage, string responseText)
    {
        Message updatedPromptMessage = promptMessage with { Tokens = promptTokens };
        Message responseMessage = new(sessionId, nameof(Participants.Bot), responseTokens, responseText);

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        _sessions[index].UpdateMessage(updatedPromptMessage);
        _sessions[index].AddMessage(responseMessage);

        await _cosmosDbService.UpsertMessagesBatchAsync(promptMessage, responseMessage);
    }
}