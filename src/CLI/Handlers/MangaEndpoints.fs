namespace MangaSharp.CLI.Server

open System
open System.Linq
open Microsoft.EntityFrameworkCore
open Giraffe
open Giraffe.EndpointRouting
open MangaSharp.Database
open MangaSharp.CLI
open MangaSharp.CLI.Util

module MangaEndpoints =

    [<CLIMutable>]
    type PutBookmarkRequest = {
        ChapterId: Guid
        PageId: Guid option
    }

    [<CLIMutable>]
    type PutDirectionRequest = {
        Direction: Direction
    }

    [<CLIMutable>]
    type PostMangaRequest = {
        Url: string
        Direction: Direction
    }

    [<CLIMutable>]
    type MangaGetResponse = {
        Id: Guid
        Title: string
        BookmarkUrl: string
        NumberOfChapters: {| NotDownloaded: int; Downloaded: int; Archived: int; Ignored: int; Total: int |}
        ChapterIndex: int
        Direction: Direction
        SourceUrl: string
        Updated: DateTime
    }

    let private putDirectionHandler (mangaId: Guid) (request: PutDirectionRequest) : HttpHandler =
        fun next ctx ->
            task {
                let service = ctx.GetService<MangaService>()
                do! service.SetDirection(mangaId, request.Direction)
                return! Successful.NO_CONTENT next ctx
            }

    let private deleteMangaHandler (mangaId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()
                let! exists = db.Manga.AnyAsync(fun m -> m.Id = mangaId)
                if not exists then
                    return! RequestErrors.NOT_FOUND "Manga not found" next ctx
                else
                    let service = ctx.GetService<MangaService>()
                    do! service.Delete(mangaId)
                    return! Successful.NO_CONTENT next ctx
            }

    let private archiveMangaHandler (mangaId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()
                let! exists = db.Manga.AnyAsync(fun m -> m.Id = mangaId)
                if not exists then
                    return! RequestErrors.NOT_FOUND "Manga not found" next ctx
                else
                    let service = ctx.GetService<MangaService>()
                    do! service.ArchiveAll(mangaId)
                    return! Successful.NO_CONTENT next ctx
            }

    let private unarchiveMangaHandler (mangaId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()
                let! exists = db.Manga.AnyAsync(fun m -> m.Id = mangaId)
                if not exists then
                    return! RequestErrors.NOT_FOUND "Manga not found" next ctx
                else
                    let service = ctx.GetService<MangaService>()
                    do! service.UnarchiveAll(mangaId)
                    return! Successful.NO_CONTENT next ctx
            }

    let private putBookmarkHandler (mangaId: Guid) (request: PutBookmarkRequest) : HttpHandler =
        fun next ctx ->
            task {
                let service = ctx.GetService<MangaService>()
                do! service.SetBookmark(mangaId, request.ChapterId, request.PageId)
                return! Successful.NO_CONTENT next ctx
            }

    let private postMangaHandler (request: PostMangaRequest) : HttpHandler =
        fun next ctx ->
            task {
                let manager = ctx.GetService<DownloadManager>()
                let! id = manager.QueueDownload(request.Url, request.Direction)
                return! Successful.OK id next ctx
            }

    let private getMangaHandler: HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()

                let! manga =
                    db.Manga
                        .Include(fun m -> m.BookmarkChapter)
                        .Include(fun m -> m.BookmarkPage)
                        .Include(fun m -> m.Chapters)
                        .OrderBy(fun m -> m.Title)
                        .ToListAsync()

                let response =
                    manga
                    |> Seq.map (fun m ->
                        let chapters =
                            m.Chapters
                            |> Seq.filter (fun c ->
                                c.DownloadStatus = DownloadStatus.Downloaded
                                || c.DownloadStatus = DownloadStatus.Archived)
                            |> Seq.sortBy (fun c -> c.Index)

                        let chapterIndex =
                            m.BookmarkChapterId
                            |> Option.ofNullable
                            |> Option.bind (fun chapterId -> chapters |> Seq.tryFindIndex (fun c -> c.Id = chapterId))
                            |> Option.defaultValue 0

                        let updated =
                            if m.Chapters.Count > 0 then
                                m.Chapters |> Seq.map (fun c -> c.Created) |> Seq.max
                            else
                                DateTime.MinValue

                        {
                            Id = m.Id
                            Title = m.Title
                            BookmarkUrl = getBookmarkUrl m
                            NumberOfChapters = {|
                                NotDownloaded =
                                    m.Chapters
                                    |> Seq.filter (fun c -> c.DownloadStatus = DownloadStatus.NotDownloaded)
                                    |> Seq.length
                                Downloaded =
                                    m.Chapters
                                    |> Seq.filter (fun c -> c.DownloadStatus = DownloadStatus.Downloaded)
                                    |> Seq.length
                                Archived =
                                    m.Chapters
                                    |> Seq.filter (fun c -> c.DownloadStatus = DownloadStatus.Archived)
                                    |> Seq.length
                                Ignored =
                                    m.Chapters
                                    |> Seq.filter (fun c -> c.DownloadStatus = DownloadStatus.Ignored)
                                    |> Seq.length
                                Total = m.Chapters.Count
                            |}
                            ChapterIndex = chapterIndex
                            Direction = m.Direction
                            SourceUrl = m.Url
                            Updated = updated
                        })

                return! json response next ctx
            }

    let private checkUpdateHandler (mangaId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()
                let! manga = db.Manga.FindAsync(mangaId)
                if isNull (box manga) then
                    return! RequestErrors.NOT_FOUND "Manga not found" next ctx
                else
                    let downloader = ctx.GetService<MangaDownloaderService>()
                    let! result = downloader.CheckForUpdates(mangaId, manga.Url)
                    match result with
                    | Ok count ->
                        if count > 0 then
                            let manager = ctx.GetService<DownloadManager>()
                            let! _ = manager.QueueUpdate(mangaId, manga.Title, manga.Url)
                            ()
                        return! json {| Count = count |} next ctx
                    | Error e ->
                        return! RequestErrors.BAD_REQUEST e next ctx
            }

    let private checkAllUpdatesHandler : HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()
                let! allManga =
                    db.Manga
                        .AsNoTracking()
                        .OrderBy(fun m -> m.Title)
                        .Select(fun m -> {| Id = m.Id; Title = m.Title; Url = m.Url |})
                        .ToListAsync()

                let! existingJobs =
                    db.DownloadJobs
                        .AsNoTracking()
                        .Where(fun j -> j.Type = JobType.UpdateManga)
                        .ToListAsync()

                let manager = ctx.GetService<DownloadManager>()

                for m in allManga do
                    let existingJob =
                        existingJobs
                        |> Seq.tryFind (fun j ->
                            j.MangaId.HasValue
                            && j.MangaId.Value = m.Id
                            && j.Status <> JobStatus.Completed
                            && j.Status <> JobStatus.Canceled
                            && j.Status <> JobStatus.Failed)

                    match existingJob with
                    | Some job ->
                        do! manager.UpdateStatus(job.Id, JobStatus.Pending, None)
                    | None ->
                        let! _ = manager.QueueUpdate(m.Id, m.Title, m.Url)
                        ()

                return! Successful.NO_CONTENT next ctx
            }

    let routes : Endpoint list =
        [
            GET [ route "/manga" getMangaHandler ]
            PUT [
                routef "/manga/%O/bookmark" (putBookmarkHandler >> bindJson<PutBookmarkRequest>)
                routef "/manga/%O/direction" (putDirectionHandler >> bindJson<PutDirectionRequest>)
            ]
            POST [
                route "/manga" (bindJson<PostMangaRequest> postMangaHandler)
                routef "/manga/%O/archive" (fun id -> archiveMangaHandler id)
                routef "/manga/%O/unarchive" (fun id -> unarchiveMangaHandler id)
                routef "/manga/%O/check-update" checkUpdateHandler
                route "/manga/check-updates" checkAllUpdatesHandler
            ]
            DELETE [
                routef "/manga/%O" (fun id -> deleteMangaHandler id)
            ]
        ]
