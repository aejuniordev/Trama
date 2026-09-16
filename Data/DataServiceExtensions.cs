using Microsoft.EntityFrameworkCore;

namespace Trama.Data;

public static class DataServiceExtensions
{
    public static WebApplicationBuilder AddTramaData(this WebApplicationBuilder builder)
    {
        var connection = builder.Configuration.GetConnectionString("Trama") ?? "Data Source=trama.db";
        builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddScoped<ProjectRepository>();
        return builder;
    }

    /// <summary>Cria o arquivo e a tabela na primeira execução.</summary>
    public static WebApplication EnsureTramaDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
        return app;
    }
}
