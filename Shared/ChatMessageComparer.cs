using BlazorApp.Shared;
using System.Collections.Generic;
using System;

public class ChatMessageComparer : IEqualityComparer<ChatMessage>
{
    public bool Equals(ChatMessage x, ChatMessage y)
    {
        return x?.User == y?.User && x?.Message == y?.Message && x?.Timestamp == y?.Timestamp;
    }

    public int GetHashCode(ChatMessage obj)
    {
        return (obj.User?.GetHashCode() ?? 0)
             ^ (obj.Message?.GetHashCode() ?? 0)
             ^ obj.Timestamp.GetHashCode();
    }
}
