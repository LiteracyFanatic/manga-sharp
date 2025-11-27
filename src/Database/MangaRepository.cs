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

public class DownloadJobRepository(MangaContext db)
{
    public async Task<DownloadJob> EnqueueAddMangaAsync(string url)
    {
        var minOrder = await db.DownloadJobs.MinAsync(j => (double?)j.OrderIndex) ?? 0;
        var job = new DownloadJob
        {
            Id = Guid.NewGuid(),
            Type = JobType.AddManga,
            Status = JobStatus.Pending,
            Url = url,
            CreatedAt = DateTime.UtcNow,
            OrderIndex = minOrder - 1
        };

        db.DownloadJobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    public async Task<DownloadJob> EnqueueUpdateMangaAsync(Guid mangaId, string title, string url)
    {
        var maxOrder = await db.DownloadJobs.MaxAsync(j => (double?)j.OrderIndex) ?? 0;
        var job = new DownloadJob
        {
            Id = Guid.NewGuid(),
            Type = JobType.UpdateManga,
            Status = JobStatus.Pending,
            Url = url,
            MangaId = mangaId,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            OrderIndex = maxOrder + 1
        };

        db.DownloadJobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    public async Task<List<DownloadJob>> GetJobsAsync()
    {
        return await db.DownloadJobs.OrderBy(j => j.OrderIndex).ToListAsync();
    }

    public async Task<List<DownloadJob>> GetPendingJobsAsync(int limit)
    {
        return await db.DownloadJobs
            .Where(j => j.Status == JobStatus.Pending)
            .OrderBy(j => j.OrderIndex)
            .Take(limit)
            .TagWithCallSite()
            .ToListAsync();
    }

    public async Task MoveToTopAsync(Guid id)
    {
        var job = await db.DownloadJobs.FindAsync(id);
        if (job == null) return;
        var minOrder = await db.DownloadJobs.MinAsync(j => (double?)j.OrderIndex) ?? 0;
        job.OrderIndex = minOrder - 1;
        await db.SaveChangesAsync();
    }

    public async Task MoveToBottomAsync(Guid id)
    {
        var job = await db.DownloadJobs.FindAsync(id);
        if (job == null) return;
        var maxOrder = await db.DownloadJobs.MaxAsync(j => (double?)j.OrderIndex) ?? 0;
        job.OrderIndex = maxOrder + 1;
        await db.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(Guid id, JobStatus status, string? error = null)
    {
        var job = await db.DownloadJobs.FirstOrDefaultAsync(j => j.Id == id);
        if (job == null)
        {
            return;
        }

        job.Status = status;
        if (status == JobStatus.Downloading && job.StartedAt == null)
        {
            job.StartedAt = DateTime.UtcNow;
        }

        if (status is JobStatus.Completed or JobStatus.Canceled or JobStatus.Failed)
        {
            job.CompletedAt = DateTime.UtcNow;
        }

        job.Error = error;

        await db.SaveChangesAsync();
    }

    public async Task UpdateProgressAsync(Guid id, string? chapterTitle, int? chapterIndex, int? totalChapters, int? pageIndex, int? totalPages)
    {
        var job = await db.DownloadJobs.FirstOrDefaultAsync(j => j.Id == id);
        if (job == null)
        {
            return;
        }

        job.ProgressChapterTitle = chapterTitle;
        job.ProgressChapterIndex = chapterIndex;
        job.ProgressTotalChapters = totalChapters;
        job.ProgressPageIndex = pageIndex;
        job.ProgressTotalPages = totalPages;
        await db.SaveChangesAsync();
    }

    public async Task ClearCompletedAsync()
    {
        var completed = await db.DownloadJobs
            .Where(j => j.Status == JobStatus.Completed || j.Status == JobStatus.Canceled || j.Status == JobStatus.Failed)
            .ToListAsync();

        if (completed.Count == 0)
        {
            return;
        }

        db.DownloadJobs.RemoveRange(completed);
        await db.SaveChangesAsync();
    }

    public async Task ResetStuckJobsAsync()
    {
        var stuckJobs = await db.DownloadJobs
            .Where(j => j.Status == JobStatus.Downloading)
            .ToListAsync();

        foreach (var job in stuckJobs)
        {
            job.Status = JobStatus.Pending;
        }

        if (stuckJobs.Count > 0)
        {
            await db.SaveChangesAsync();
        }
    }
}
