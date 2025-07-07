using EcocementBot.States;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcocementBot.Services;

public class BotState
{
    public IReadOnlyDictionary<long, Stack<IScreen>> Screens { get; set; }
}

public class PersistanceService
{
    private readonly Navigator _navigator;

    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve
    };

    public PersistanceService(Navigator navigator)
    {
        _navigator = navigator;
    }

    public async Task Save()
    {
        var state = new BotState()
        {
            Screens = _navigator.Screens,
        };

        using var file = File.Open("state.json", FileMode.Create, FileAccess.Write, FileShare.None);
        file.Position = 0;
        await JsonSerializer.SerializeAsync(file, state, s_serializerOptions);
        await file.FlushAsync();
    }
}
