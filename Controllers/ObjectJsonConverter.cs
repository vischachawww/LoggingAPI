using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class ObjectJsonConverter : JsonConverter<object>
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(object);

    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return ToObject(doc.RootElement);
    }

    private static object ToObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object>();
                foreach (var p in element.EnumerateObject())
                    dict[p.Name] = ToObject(p.Value);
                return dict;

            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var i in element.EnumerateArray())
                    list.Add(ToObject(i));
                return list;

            case JsonValueKind.String:
                if (element.TryGetDateTimeOffset(out var dto)) return dto;
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l))   return l;
                if (element.TryGetDouble(out var d))  return d;
                return element.GetDecimal();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null!;
        }
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object), options);
}
