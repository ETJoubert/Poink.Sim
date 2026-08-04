using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenAPI.Net.Auth
{
    public class Token
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonConverter(typeof(TokenExpiryConverter))]
        [JsonPropertyName("expiresIn")]
        public DateTimeOffset ExpiresIn { get; set; }

        [JsonPropertyName("tokenType")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("errorCode")]
        public string ErrorCode { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string ErrorDescription { get; set; } = string.Empty;

        private class TokenExpiryConverter : JsonConverter<DateTimeOffset>
        {
            public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return DateTimeOffset.UtcNow.AddSeconds(reader.GetInt64());
            }

            public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
            {
                writer.WriteNumberValue(Convert.ToInt64((value - DateTimeOffset.UtcNow).TotalSeconds));
            }
        }
    }
}