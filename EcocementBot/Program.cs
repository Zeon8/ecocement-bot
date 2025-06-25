using EcocementBot;
using EcocementBot.Data;
using EcocementBot.Services;
using EcocementBot.States;
using EcocementBot.States.Screens.Admin;
using EcocementBot.States.Screens.Admin.Clients;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

var botToken = builder.Configuration["BotToken"] ?? throw new InvalidOperationException("BotToken not found.");
var dbFile = builder.Configuration["DbFile"] ?? throw new InvalidOperationException("DbFile not found.");

builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite($"Data Source={dbFile}"), ServiceLifetime.Singleton);
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<ClientService>();

builder.Services.AddSingleton(new TelegramBotClient(botToken));
builder.Services.AddSingleton<Navigator>();
builder.Services.AddSingleton<ApplicationStartup>();

builder.Services.AddTransient<AdminService>();
builder.Services.AddTransient<AdminScreen>();
builder.Services.AddTransient<ClientsScreen>();
builder.Services.AddTransient<CreateClientScreen>();
builder.Services.AddTransient<EditClientScreen>();
builder.Services.AddTransient<DeleteClientScreen>();

var app = builder.Build();

await app.Services.GetRequiredService<ApplicationStartup>().Start();

app.Run();
