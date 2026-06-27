using System.Text.Json;
using System.Text.Json.Serialization;
using JalcruzFirstClass.Api.Domain;

namespace JalcruzFirstClass.Api.Services;

/// <summary>
/// Serializa/deserializa un enum usando su mapa string (ej. Present &lt;-&gt; "asistio"),
/// preservando el MISMO contrato JSON que el backend Laravel original.
/// </summary>
public class MapEnumJsonConverter<TEnum>(IReadOnlyDictionary<TEnum, string> map) : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private readonly Dictionary<string, TEnum> _inverse =
        map.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (s is not null && _inverse.TryGetValue(s, out var value)) return value;
        // Acepta también el nombre del enum ("Present") como respaldo.
        if (Enum.TryParse<TEnum>(s, ignoreCase: true, out var parsed)) return parsed;
        throw new JsonException($"Valor inválido '{s}' para {typeof(TEnum).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        => writer.WriteStringValue(map[value]);
}

public static class EnumJsonConverters
{
    /// <summary>Registra los convertidores de los 5 enums del dominio.</summary>
    public static void AddDomainEnumConverters(this IList<JsonConverter> converters)
    {
        converters.Add(new MapEnumJsonConverter<Reliability>(EnumMaps.Reliability));
        converters.Add(new MapEnumJsonConverter<AttendanceStatus>(EnumMaps.AttendanceStatus));
        converters.Add(new MapEnumJsonConverter<EntityType>(EnumMaps.EntityType));
        converters.Add(new MapEnumJsonConverter<ProspectStatus>(EnumMaps.ProspectStatus));
        converters.Add(new MapEnumJsonConverter<TrialClassStatus>(EnumMaps.TrialClassStatus));
    }
}
