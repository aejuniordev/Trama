using Trama.Components;
using Trama.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Banco SQLite (arquivo trama.db) com os projetos salvos.
builder.AddTramaData();

var app = builder.Build();

app.EnsureTramaDatabase();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
