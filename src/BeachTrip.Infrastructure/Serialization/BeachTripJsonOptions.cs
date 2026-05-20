using System.Text.Json;
using System.Text.Json.Serialization;

namespace BeachTrip.Infrastructure.Serialization;

public static class BeachTripJsonOptions
{
    public static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new StronglyTypedIdConverterFactory());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
