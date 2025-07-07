using System.Text.Json.Serialization.Metadata;
using System.Text.Json;
using EcocementBot.States;

namespace EcocementBot;

public class DIJsonTypeInfoResolver : DefaultJsonTypeInfoResolver
{
    private readonly IServiceProvider _services;

    public DIJsonTypeInfoResolver(IServiceProvider services)
    {
        _services = services;
    }

    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var typeInfo = base.GetTypeInfo(type, options);

        if (typeInfo.Type.IsAssignableTo(typeof(IScreen)))
            typeInfo.CreateObject = () => _services.GetRequiredService(typeInfo.Type);

        return typeInfo;
    }
}
