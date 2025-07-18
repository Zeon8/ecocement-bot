using EcocementBot;
using EcocementBot.Data;
using EcocementBot.Services;
using EcocementBot.States;
using EcocementBot.States.Screens;
using EcocementBot.States.Screens.Admin;
using EcocementBot.States.Screens.Admin.Clients;
using EcocementBot.States.Screens.Admin.Marks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Telegram.Bot;

Log.Logger = new LoggerConfiguration()
            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 1)
            .CreateLogger();

var builder = Host.CreateApplicationBuilder();

builder.Services.AddLogging(c => c.AddSerilog());

var botToken = builder.Configuration["BotToken"] ?? throw new InvalidOperationException("BotToken not found.");
var dbFile = builder.Configuration["DbFile"] ?? throw new InvalidOperationException("DbFile not found.");

builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite($"Data Source={dbFile}"));
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<MarkService>();

builder.Services.AddSingleton(new TelegramBotClient(botToken));
builder.Services.AddSingleton<Navigator>();
builder.Services.AddSingleton<OrderSender>();

builder.Services.AddSingleton<StatePersistanceService>();
builder.Services.AddSingleton<DIJsonTypeInfoResolver>();

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

using (var scope = builder.Services.BuildServiceProvider().CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
}

host.Run();
