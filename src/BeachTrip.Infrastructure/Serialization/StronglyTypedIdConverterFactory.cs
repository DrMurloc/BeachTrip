using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BeachTrip.Infrastructure.Serialization;

// Serializes any single-Guid record-struct identifier as a bare Guid string instead
// of the default `{ "Value": "..." }` shape. Matches the Domain.Identifiers convention
// (record struct Foo(Guid Value)).
public sealed class StronglyTypedIdConverterFactory : JsonConverterFactory
{
    private static readonly ConcurrentDictionary<Type, JsonConverter?> _cache = new();

    public override bool CanConvert(Type typeToConvert) =>
        _cache.GetOrAdd(typeToConvert, BuildConverterIfApplicable) is not null;

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        _cache.GetOrAdd(typeToConvert, BuildConverterIfApplicable);

    private static JsonConverter? BuildConverterIfApplicable(Type type)
    {
        if (!type.IsValueType) return null;
        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault();
        if (ctor is null) return null;
        var parameters = ctor.GetParameters();
        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(Guid)) return null;
        if (!string.Equals(parameters[0].Name, "value", StringComparison.OrdinalIgnoreCase)) return null;

        var converterType = typeof(GuidStructIdConverter<>).MakeGenericType(type);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}

internal sealed class GuidStructIdConverter<T> : JsonConverter<T> where T : struct
{
    private static readonly Func<Guid, T> _construct = BuildConstructor();
    private static readonly Func<T, Guid> _read = BuildReader();

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        _construct(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(_read(value));

    private static Func<Guid, T> BuildConstructor()
    {
        var ctor = typeof(T).GetConstructor(new[] { typeof(Guid) })
            ?? throw new InvalidOperationException($"{typeof(T).Name} needs a (Guid) constructor.");
        var p = System.Linq.Expressions.Expression.Parameter(typeof(Guid));
        var newExpr = System.Linq.Expressions.Expression.New(ctor, p);
        return System.Linq.Expressions.Expression.Lambda<Func<Guid, T>>(newExpr, p).Compile();
    }

    private static Func<T, Guid> BuildReader()
    {
        var prop = typeof(T).GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{typeof(T).Name} needs a public Value property.");
        var p = System.Linq.Expressions.Expression.Parameter(typeof(T));
        var read = System.Linq.Expressions.Expression.Property(p, prop);
        return System.Linq.Expressions.Expression.Lambda<Func<T, Guid>>(read, p).Compile();
    }
}
