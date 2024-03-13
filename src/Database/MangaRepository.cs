using Microsoft.EntityFrameworkCore;

namespace MangaSharp.Database;

public class MangaRepository(MangaContext db)
{
    public async Task<Manga> GetByIdAsync(Guid mangaId)
    {
        return await db
            .Manga
            .Include(manga => manga.Chapters)
            .ThenInclude(chapter => chapter.Pages)
            .AsSplitQuery()
            .FirstAsync(m => m.Id == mangaId);
    }

    public async Task<Manga> GetOrCreateAsync(string title, Direction direction, string url)
    {
        var existingManga = await db
            .Manga
            .Include(manga => manga.Chapters)
            .ThenInclude(chapter => chapter.Pages)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Title == title);

        var manga = existingManga ?? db.Manga.Add(new Manga
            {
                Title = title,
                Direction = direction,
                Url = url
            })
            .Entity;

        return manga;
    }
}
