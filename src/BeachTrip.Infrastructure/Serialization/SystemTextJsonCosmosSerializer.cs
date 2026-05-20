using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace BeachTrip.Infrastructure.Serialization;

// Cosmos SDK v3 defaults to Newtonsoft.Json. We want System.Text.Json end-to-end so the
// StronglyTypedIdConverterFactory and other STJ converters control how docs land in Cosmos.
public sealed class SystemTextJsonCosmosSerializer : CosmosSerializer
{
    private readonly JsonSerializerOptions _options;

    public SystemTextJsonCosmosSerializer(JsonSerializerOptions options) => _options = options;

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (typeof(T) == typeof(Stream)) return (T)(object)stream;
            return JsonSerializer.Deserialize<T>(stream, _options)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var ms = new MemoryStream();
        JsonSerializer.Serialize(ms, input, _options);
        ms.Position = 0;
        return ms;
    }
}
