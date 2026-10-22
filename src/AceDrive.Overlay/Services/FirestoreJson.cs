using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Firestore REST value mapping. Firestore wraps every field in a typed
/// object (<c>{"stringValue":…}</c>, <c>{"integerValue":"12"}</c>, …); this
/// file is the one place that translates between those wrappers and the CLR
/// primitives the services use.
/// </summary>
public static class FirestoreJson
{
    /// <summary>Decodes one typed value; unknown shapes answer null.</summary>
    public static object? ReadValue(JsonElement element)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "stringValue":
                    return property.Value.GetString();
                case "integerValue":
                    return long.TryParse(
                        property.Value.GetString(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out long integer)
                        ? integer
                        : null;
                case "doubleValue":
                    return property.Value.ValueKind == JsonValueKind.Number
                        ? property.Value.GetDouble()
                        : null;
                case "booleanValue":
                    return property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? property.Value.GetBoolean()
                        : null;
                case "timestampValue":
                    return DateTime.TryParse(
                        property.Value.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out DateTime stamp)
                        ? stamp
                        : null;
                case "nullValue":
                    return null;
                case "arrayValue":
                    return ReadArray(property.Value);
                case "mapValue":
                    return property.Value.TryGetProperty("fields", out JsonElement map)
                        ? ReadFields(map)
                        : new Dictionary<string, object?>(StringComparer.Ordinal);
            }
        }
        return null;
    }

    /// <summary>Decodes a <c>fields</c> object into a field dictionary.</summary>
    public static Dictionary<string, object?> ReadFields(JsonElement fields)
    {
        Dictionary<string, object?> result = new(StringComparer.Ordinal);
        foreach (JsonProperty field in fields.EnumerateObject())
        {
            result[field.Name] = ReadValue(field.Value);
        }
        return result;
    }

    /// <summary>
    /// Decodes one REST document object (<c>{name, fields}</c>). Null when
    /// the object has no usable name.
    /// </summary>
    public static FirestoreDocument? ReadDocument(JsonElement element)
    {
        if (!element.TryGetProperty("name", out JsonElement name)
            || name.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        string? fullName = name.GetString();
        if (string.IsNullOrEmpty(fullName))
        {
            return null;
        }

        IReadOnlyDictionary<string, object?> fields =
            element.TryGetProperty("fields", out JsonElement rawFields)
                ? ReadFields(rawFields)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
        return new FirestoreDocument(DocId(fullName), fields);
    }

    /// <summary>The last path segment of a Firestore document name.</summary>
    public static string DocId(string fullName)
    {
        int slash = fullName.LastIndexOf('/');
        return slash >= 0 ? fullName[(slash + 1)..] : fullName;
    }

    /// <summary>Encodes a field dictionary into Firestore's typed wrapper.</summary>
    public static JsonObject WriteFields(IReadOnlyDictionary<string, object?> fields)
    {
        JsonObject result = [];
        foreach (KeyValuePair<string, object?> field in fields)
        {
            result[field.Key] = WriteValue(field.Value);
        }
        return result;
    }

    private static JsonObject WriteValue(object? value)
    {
        JsonObject wrapper = [];
        switch (value)
        {
            case null:
                wrapper["nullValue"] = null;
                break;
            case string text:
                wrapper["stringValue"] = text;
                break;
            case bool flag:
                wrapper["booleanValue"] = flag;
                break;
            case int number:
                wrapper["integerValue"] = number.ToString(CultureInfo.InvariantCulture);
                break;
            case long number:
                wrapper["integerValue"] = number.ToString(CultureInfo.InvariantCulture);
                break;
            case double number when double.IsFinite(number):
                wrapper["doubleValue"] = number;
                break;
            case double:
                wrapper["nullValue"] = null;
                break;
            case DateTime stamp:
                wrapper["timestampValue"] = stamp.ToUniversalTime().ToString(
                    "o", CultureInfo.InvariantCulture);
                break;
            case IReadOnlyDictionary<string, object?> map:
                wrapper["mapValue"] = new JsonObject { ["fields"] = WriteFields(map) };
                break;
            case IReadOnlyList<object?> list:
                JsonArray values = [];
                foreach (object? item in list)
                {
                    values.Add(WriteValue(item));
                }
                wrapper["arrayValue"] = new JsonObject { ["values"] = values };
                break;
            default:
                wrapper["stringValue"] = value.ToString() ?? string.Empty;
                break;
        }
        return wrapper;
    }

    private static List<object?> ReadArray(JsonElement array)
    {
        List<object?> items = [];
        if (!array.TryGetProperty("values", out JsonElement values)
            || values.ValueKind != JsonValueKind.Array)
        {
            return items;
        }
        foreach (JsonElement item in values.EnumerateArray())
        {
            items.Add(ReadValue(item));
        }
        return items;
    }
}
