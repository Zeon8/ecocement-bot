using EcocementBot.States.Screens;
using EcocementBot.States.Screens.Admin;
using EcocementBot.States.Screens.Admin.Clients;
using EcocementBot.States.Screens.Admin.Marks;
using System.Text.Json.Serialization;
using Telegram.Bot.Types;

namespace EcocementBot.States;

[JsonDerivedType(typeof(OrderScreen), typeDiscriminator: nameof(OrderScreen))]
[JsonDerivedType(typeof(AdminScreen), typeDiscriminator: nameof(AdminScreen))]
[JsonDerivedType(typeof(ClientsScreen), typeDiscriminator: nameof(ClientsScreen))]
[JsonDerivedType(typeof(CreateClientScreen), typeDiscriminator: nameof(CreateClientScreen))]
[JsonDerivedType(typeof(DeleteClientScreen), typeDiscriminator: nameof(DeleteClientScreen))]
[JsonDerivedType(typeof(EditClientScreen), typeDiscriminator: nameof(EditClientScreen))]
[JsonDerivedType(typeof(CreateMarkScreen), typeDiscriminator: nameof(CreateMarkScreen))]
[JsonDerivedType(typeof(RemoveMarkScreen), typeDiscriminator: nameof(RemoveMarkScreen))]
[JsonDerivedType(typeof(MarksScreen), typeDiscriminator: nameof(MarksScreen))]
[JsonDerivedType(typeof(AuthorizationScreen), typeDiscriminator: nameof(AuthorizationScreen))]
public interface IScreen
{
    Task EnterAsync(User user, Chat chat);

    Task HandleInput(Message message);
}
