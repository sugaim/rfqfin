using System.Text.Json;
using System.Text.Json.Serialization;
using Rfq.Domain;

namespace Rfq.Infrastructure;

internal static class PersistenceJsonSerializer
{
    internal static JsonSerializerOptions Options { get; } = CreateOptions();

    internal static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    internal static T Deserialize<T>(string json, string description)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                ?? throw new DomainInvariantException($"Persisted {description} is null.");
        }
        catch (JsonException exception)
        {
            throw new DomainInvariantException($"Persisted {description} is invalid: {exception.Message}");
        }
    }

    internal static JsonDocument Parse(string json, string description)
    {
        try
        {
            var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw new DomainInvariantException($"Persisted {description} must be a JSON object.");
            }
            return document;
        }
        catch (JsonException exception)
        {
            throw new DomainInvariantException($"Persisted {description} is invalid: {exception.Message}");
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            NumberHandling = JsonNumberHandling.Strict,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

}
