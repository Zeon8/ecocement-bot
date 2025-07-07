using EcocementBot.States;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcocementBot.Services;

public class BotState
{
    public Dictionary<long, List<IScreen>> Screens { get; set; } = [];

    public Dictionary<long, string> Sessions { get; set; } = [];
}

public class StatePersistanceService
{
    private readonly Navigator _navigator;
    private readonly SessionService _sessionService;
    
    private const string StateFileName = "state.json";
    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        //ReferenceHandler = ReferenceHandler.Preserve,
        WriteIndented = true,
    };


    public StatePersistanceService(Navigator navigator, 
        DIJsonTypeInfoResolver typeInfoResolver, 
        SessionService sessionService)
    {
        _navigator = navigator;
        s_serializerOptions.TypeInfoResolver = typeInfoResolver;
        _sessionService = sessionService;
    }

    public async Task Save()
    {
        var state = new BotState()
        {
            Screens = _navigator.Screens.ToDictionary(i => i.Key, i => i.Value.ToList()),
            Sessions = _sessionService.Sessions,
        };

        using var file = File.Open(StateFileName, FileMode.Create, FileAccess.Write, FileShare.None);
        file.Position = 0;
        await JsonSerializer.SerializeAsync(file, state, s_serializerOptions);
        await file.FlushAsync();
    }

    public async Task Load()
    {
        if (!File.Exists(StateFileName))
            return;

        var json = File.OpenRead(StateFileName);
        var state = await JsonSerializer.DeserializeAsync<BotState>(json, s_serializerOptions);

        _navigator.Screens = state!.Screens.ToDictionary(i => i.Key, i => 
        { 
            i.Value.Reverse();
            return new Stack<IScreen>(i.Value); 
        });
        _sessionService.Sessions = state.Sessions;
    }
}
