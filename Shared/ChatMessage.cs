using System;
using System.Text.Json.Serialization;

namespace BlazorApp.Shared
{
    public class ChatMessage
    {
        [JsonPropertyName("user")]
        public string User { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

}
