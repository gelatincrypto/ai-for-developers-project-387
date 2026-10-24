using System.Text.Json;
using System.Text.Json.Serialization;
using Calendar.Api.Generated.Models;

namespace Calendar.Api.Controllers;

internal sealed class HealthStatusJsonConverter : JsonConverter<HealthResponse.StatusEnum>
{
    public override HealthResponse.StatusEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() == "ok" ? HealthResponse.StatusEnum.OkEnum : throw new JsonException();

    public override void Write(Utf8JsonWriter writer, HealthResponse.StatusEnum value, JsonSerializerOptions options)
    {
        if (value != HealthResponse.StatusEnum.OkEnum)
        {
            throw new JsonException();
        }

        writer.WriteStringValue("ok");
    }
}
