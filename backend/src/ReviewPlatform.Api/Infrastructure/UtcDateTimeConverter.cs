using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReviewPlatform.Api.Infrastructure;

/// <summary>
/// Приводит входящие даты к UTC. Стандартный конвертер дату со смещением («+03:00», и даже «+00:00») отдаёт
/// как локальное время сервера, а Npgsql пишет в timestamptz только UTC — без приведения запрос падал с 500.
/// Дата без смещения считается UTC: все даты API — в UTC.
/// </summary>
internal sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
