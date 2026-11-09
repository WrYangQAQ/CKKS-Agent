using System.Text.Json.Serialization;

namespace CKKS_Agent_System.Enum
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LoginType
    {
        Username,
        PhoneNumber,
        Email
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum VerifyType
    {
        PhoneNumber,
        Email
    }
}
