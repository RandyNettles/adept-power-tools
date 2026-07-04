using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdeptTools.Workflow.Models;

public sealed class WorkflowUserTypeJsonConverter : JsonConverter<WorkflowUserType>
{
    public override WorkflowUserType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = (reader.GetString() ?? string.Empty).Trim();
            if (raw.Length == 1)
                return FromCode(raw[0]);

            if (Enum.TryParse<WorkflowUserType>(raw, ignoreCase: true, out var parsed))
                return parsed;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numeric))
            return (WorkflowUserType)numeric;

        throw new JsonException($"Unsupported WorkflowUserType value token: {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, WorkflowUserType value, JsonSerializerOptions options)
    {
        // Keep single-letter wire format for known values, but do not emit
        // invalid control characters for default/unknown enum values.
        if (Enum.IsDefined(typeof(WorkflowUserType), value))
        {
            writer.WriteStringValue(((char)value).ToString());
            return;
        }

        writer.WriteNumberValue((int)value);
    }

    private static WorkflowUserType FromCode(char code)
    {
        return char.ToUpperInvariant(code) switch
        {
            'U' => WorkflowUserType.User,
            'G' => WorkflowUserType.Group,
            'K' => WorkflowUserType.Key,
            'E' => WorkflowUserType.Email,
            'A' => WorkflowUserType.Approvers,
            _ => throw new JsonException($"Unknown WorkflowUserType code '{code}'.")
        };
    }
}

public sealed class WorkflowNotificationActionJsonConverter : JsonConverter<WorkflowNotificationAction>
{
    public override WorkflowNotificationAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = (reader.GetString() ?? string.Empty).Trim();
            if (raw.Length == 0)
                return WorkflowNotificationAction.Undefined;

            if (raw.Length == 1)
                return FromCode(raw[0]);

            if (Enum.TryParse<WorkflowNotificationAction>(raw, ignoreCase: true, out var parsed))
                return parsed;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numeric))
            return (WorkflowNotificationAction)numeric;

        throw new JsonException($"Unsupported WorkflowNotificationAction value token: {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, WorkflowNotificationAction value, JsonSerializerOptions options)
    {
        // Keep single-letter wire format for known values, but avoid emitting
        // problematic characters for unknown enum values.
        if (Enum.IsDefined(typeof(WorkflowNotificationAction), value))
        {
            writer.WriteStringValue(((char)value).ToString());
            return;
        }

        writer.WriteNumberValue((int)value);
    }

    private static WorkflowNotificationAction FromCode(char code)
    {
        return char.ToUpperInvariant(code) switch
        {
            'A' => WorkflowNotificationAction.Approve,
            'T' => WorkflowNotificationAction.Timeout,
            _ => WorkflowNotificationAction.Undefined
        };
    }
}
