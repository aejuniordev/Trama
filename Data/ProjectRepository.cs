using Microsoft.EntityFrameworkCore;

namespace Trama.Data;

/// <summary>
/// Acesso aos projetos. Usa uma fábrica de DbContext porque, no Blazor Server,
/// um componente vive muito tempo e pode disparar operações concorrentes.
/// </summary>
public sealed class ProjectRepository(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<ProjectSummary>> ListAsync(string? search = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Projects.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Name, term));
        }

        return await query
            .OrderByDescending(p => p.UpdatedAt)
            .Take(300)
            .Select(p => new ProjectSummary(p.Id, p.Name, p.Target, p.Units, p.SegmentCount, p.IsManual, p.UpdatedAt))
            .ToListAsync();
    }

    public async Task<Project?> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project> SaveAsync(Project project)
    {
        await using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        var existing = project.Id > 0 ? await db.Projects.FirstOrDefaultAsync(p => p.Id == project.Id) : null;
        if (existing is null)
        {
            project.Id = 0;
            project.CreatedAt = now;
            project.UpdatedAt = now;
            db.Projects.Add(project);
        }
        else
        {
            project.CreatedAt = existing.CreatedAt;
            project.UpdatedAt = now;
            db.Entry(existing).CurrentValues.SetValues(project);
        }

        await db.SaveChangesAsync();
        return project;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Projects.Where(p => p.Id == id).ExecuteDeleteAsync();
    }

    public async Task<Project?> DuplicateAsync(int id)
    {
        var source = await GetAsync(id);
        if (source is null) return null;

        source.Id = 0;
        source.Name = source.Name.Length > 110 ? source.Name[..110] + " (cópia)" : source.Name + " (cópia)";
        return await SaveAsync(source);
    }
}
