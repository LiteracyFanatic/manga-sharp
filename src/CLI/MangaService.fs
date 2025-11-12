namespace MangaSharp.CLI

open System
open System.IO
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Logging
open MangaSharp.Database
open MangaSharp.Extractors.Util

type MangaService(db: MangaContext, logger: ILogger<MangaService>) =
    let getDirForMangaSafe (manga: Manga) =
        let dir = Path.Combine(mangaData, manga.Title)
        if dir = mangaData then
            logger.LogError("Manga had no title. Refusing to delete {MangaDataDirectory}.", mangaData)
            invalidOp "Invalid manga title"
        else
            dir

    member _.SetDirection(mangaId: Guid, direction: Direction) = task {
        let! manga = db.Manga.FirstAsync(fun m -> m.Id = mangaId)
        manga.Direction <- direction
        let! _ = db.SaveChangesAsync()
        return ()
    }

    member _.SetBookmark(mangaId: Guid, chapterId: Guid, pageId: Guid option) = task {
        let! manga = db.Manga.FirstAsync(fun m -> m.Id = mangaId)
        manga.BookmarkChapterId <- chapterId
        manga.BookmarkPageId <- Option.toNullable pageId
        let! _ = db.SaveChangesAsync()
        return ()
    }

    member _.Delete(mangaId: Guid) = task {
        let! manga = db.Manga.FirstOrDefaultAsync(fun m -> m.Id = mangaId)
        match Option.ofObj manga with
        | None -> return ()
        | Some manga ->
            db.Manga.Remove(manga) |> ignore
            try
                let! _ = db.SaveChangesAsync()
                logger.LogInformation("Removed {Title} from database.", manga.Title)
            with e ->
                logger.LogError(e, "Something went wrong while removing {Title} from database.", manga.Title)
            try
                let dir = getDirForMangaSafe manga
                if Directory.Exists(dir) then
                    Directory.Delete(dir, true)
                logger.LogInformation("Deleted {MangaDirectory}.", Path.Combine(mangaData, manga.Title))
            with e ->
                logger.LogError(e, "Couldn't delete {MangaDirectory}.", Path.Combine(mangaData, manga.Title))
            return ()
    }

    member _.ArchiveAll(mangaId: Guid) = task {
        let! manga =
            db.Manga
                .Include(fun m -> m.Chapters :> System.Collections.Generic.IEnumerable<_>)
                .ThenInclude(fun (c: Chapter) -> c.Pages)
                .AsSplitQuery()
                .FirstAsync(fun m -> m.Id = mangaId)

        if Seq.isEmpty manga.Chapters then
            logger.LogWarning("{Title} has no chapters.", manga.Title)
        else
            // Mark downloaded chapters archived and clear pages
            manga.Chapters
            |> Seq.iter (fun c ->
                if c.DownloadStatus = DownloadStatus.Downloaded then
                    c.DownloadStatus <- DownloadStatus.Archived
                c.Pages.Clear())

            let! _ = db.SaveChangesAsync()
            logger.LogInformation("Marked chapters for {Title} as archived in database.", manga.Title)

            // Remove chapter folders on disk
            let mangaDir = getDirForMangaSafe manga
            manga.Chapters
            |> Seq.iter (fun c ->
                match Option.ofObj c.Title with
                | Some chapterTitle ->
                    let dir = Path.Combine(mangaDir, chapterTitle)
                    try if Directory.Exists(dir) then Directory.Delete(dir, true)
                    with e -> logger.LogError(e, "Couldn't delete {MangaChapterDirectory}.", dir)
                | None -> ())
        return ()
    }

    member _.UnarchiveAll(mangaId: Guid) = task {
        let! manga = db.Manga.Include(fun m -> m.Chapters).FirstAsync(fun m -> m.Id = mangaId)
        if Seq.isEmpty manga.Chapters then
            logger.LogWarning("{Title} has no chapters.", manga.Title)
        else
            manga.Chapters
            |> Seq.iter (fun c -> if c.DownloadStatus = DownloadStatus.Archived then c.DownloadStatus <- DownloadStatus.NotDownloaded)
            let! _ = db.SaveChangesAsync()
            logger.LogInformation("Marked chapters for {Title} as not downloaded in database.", manga.Title)
        return ()
    }

    member _.ArchiveRange(mangaId: Guid, fromTitle: string option, toTitle: string option) = task {
        let! manga =
            db.Manga
                .Include(fun m -> m.Chapters :> System.Collections.Generic.IEnumerable<_>)
                .ThenInclude(fun (c: Chapter) -> c.Pages)
                .AsSplitQuery()
                .FirstAsync(fun m -> m.Id = mangaId)

        if Seq.isEmpty manga.Chapters then
            logger.LogWarning("{Title} has no chapters.", manga.Title)
        else
            let ordered = manga.Chapters |> Seq.sortBy (fun c -> c.Index) |> Seq.toList
            let tryFindByTitle title = ordered |> List.tryFind (fun c -> c.Title = title)
            let fromChapter = fromTitle |> Option.bind tryFindByTitle |> Option.defaultValue (ordered |> List.head)
            let toChapter = toTitle |> Option.bind tryFindByTitle |> Option.defaultValue (ordered |> List.last)
            let chapters = ordered |> List.filter (fun c -> c.Index >= fromChapter.Index && c.Index <= toChapter.Index)
            if List.isEmpty chapters then
                logger.LogWarning("No matching chapters found for {Title}.", manga.Title)
            else
                chapters |> List.iter (fun c -> if c.DownloadStatus = DownloadStatus.Downloaded then c.DownloadStatus <- DownloadStatus.Archived; c.Pages.Clear())
                let! _ = db.SaveChangesAsync()
                logger.LogInformation("Archived selected chapters for {Title}.", manga.Title)
                let mangaDir = getDirForMangaSafe manga
                chapters |> List.iter (fun c ->
                    match Option.ofObj c.Title with
                    | Some chapterTitle ->
                        let dir = Path.Combine(mangaDir, chapterTitle)
                        try if Directory.Exists(dir) then Directory.Delete(dir, true) with e -> logger.LogError(e, "Couldn't delete {MangaChapterDirectory}.", dir)
                    | None -> ())
        return ()
    }

    member _.UnarchiveRange(mangaId: Guid, fromTitle: string option, toTitle: string option) = task {
        let! manga = db.Manga.Include(fun m -> m.Chapters).FirstAsync(fun m -> m.Id = mangaId)
        if Seq.isEmpty manga.Chapters then
            logger.LogWarning("{Title} has no chapters.", manga.Title)
        else
            let ordered = manga.Chapters |> Seq.sortBy (fun c -> c.Index) |> Seq.toList
            let tryFindByTitle title = ordered |> List.tryFind (fun c -> c.Title = title)
            let fromChapter = fromTitle |> Option.bind tryFindByTitle |> Option.defaultValue (ordered |> List.head)
            let toChapter = toTitle |> Option.bind tryFindByTitle |> Option.defaultValue (ordered |> List.last)
            let chapters = ordered |> List.filter (fun c -> c.Index >= fromChapter.Index && c.Index <= toChapter.Index)
            if List.isEmpty chapters then
                logger.LogWarning("No matching chapters found for {Title}.", manga.Title)
            else
                chapters |> List.iter (fun c -> if c.DownloadStatus = DownloadStatus.Archived then c.DownloadStatus <- DownloadStatus.NotDownloaded)
                let! _ = db.SaveChangesAsync()
                logger.LogInformation("Unarchived selected chapters for {Title}.", manga.Title)
        return ()
    }
