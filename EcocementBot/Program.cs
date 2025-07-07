using EcocementBot;
using EcocementBot.Data;
using EcocementBot.Services;
using EcocementBot.States;
using EcocementBot.States.Screens.Admin;
using EcocementBot.States.Screens.Admin.Clients;
using EcocementBot.States.Screens.Admin.Marks;
using EcocementBot.States.Screens.Auth;
using EcocementBot.States.Screens.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder();

var botToken = builder.Configuration["BotToken"] ?? throw new InvalidOperationException("BotToken not found.");
var dbFile = builder.Configuration["DbFile"] ?? throw new InvalidOperationException("DbFile not found.");

builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite($"Data Source={dbFile}"), ServiceLifetime.Singleton);
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<ClientService>();
builder.Services.AddSingleton<MarkService>();

builder.Services.AddSingleton(new TelegramBotClient(botToken));
builder.Services.AddSingleton<Navigator>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<OrderSender>();
builder.Services.AddSingleton<PersistanceService>();

builder.Services.AddTransient<AdminService>();
builder.Services.AddTransient<AdminScreen>();
builder.Services.AddTransient<ClientsScreen>();
builder.Services.AddTransient<CreateClientScreen>();
builder.Services.AddTransient<EditClientScreen>();
builder.Services.AddTransient<DeleteClientScreen>();
builder.Services.AddTransient<MarksScreen>();
builder.Services.AddTransient<CreateMarkScreen>();
builder.Services.AddTransient<RemoveMarkScreen>();
builder.Services.AddTransient<AuthorizationScreen>();
builder.Services.AddTransient<OrderScreen>();

builder.Services.AddHostedService<StartupService>();

IHost host = builder.Build();
host.Run();
