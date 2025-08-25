using Microsoft.Teams.Plugins.AspNetCore.DevTools.Extensions;
using Microsoft.Teams.Plugins.AspNetCore.Extensions;

using Quote.Agent;

var builder = WebApplication.CreateBuilder(args);
builder.AddTeams();
builder.AddTeamsDevTools();
builder.Services.AddTransient<MainController>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseTeams();
app.Run();
