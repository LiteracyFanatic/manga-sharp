#nowarn "20"
namespace MangaSharp.CLI.Server

open System
open System.Reflection
open System.Collections.Generic
open System.Linq
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Configuration.Json
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.FileProviders
open Giraffe
open Giraffe.EndpointRouting
open MangaSharp.Database
open MangaSharp.CLI
open MangaSharp.CLI.Util
open Serilog
open Serilog.Events

open Microsoft.Extensions.Logging
open Polly
open Polly.Extensions.Http
open System.Net.Http

module WebApp =

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

    let putDirectionHandler (mangaId: Guid) (request: PutDirectionRequest) : HttpHandler =
        fun next ctx ->
            task {
                let service = ctx.GetService<MangaService>()
                do! service.SetDirection(mangaId, request.Direction)
                return! Successful.NO_CONTENT next ctx
            }

    let deleteMangaHandler (mangaId: Guid) : HttpHandler =
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

    let archiveMangaHandler (mangaId: Guid) : HttpHandler =
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

    let unarchiveMangaHandler (mangaId: Guid) : HttpHandler =
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

    let putBookmarkHandler (mangaId: Guid) (request: PutBookmarkRequest) : HttpHandler =
        fun next ctx ->
            task {
                let service = ctx.GetService<MangaService>()
                do! service.SetBookmark(mangaId, request.ChapterId, request.PageId)
                return! Successful.NO_CONTENT next ctx
            }

    let postMangaHandler (request: PostMangaRequest) : HttpHandler =
        fun next ctx ->
            task {
                let manager = ctx.GetService<DownloadManager>()
                let! id = manager.QueueDownload(request.Url, request.Direction)
                return! Successful.OK id next ctx
            }

    let getDownloadsHandler : HttpHandler =
        fun next ctx ->
            task {
                let manager = ctx.GetService<DownloadManager>()
                let! jobs = manager.GetJobs()
                return! Successful.OK jobs next ctx
            }

    let servePage (pageId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()
                let! page = db.Pages.AsNoTracking().FirstAsync(fun p -> p.Id = pageId)
                return! streamFile true page.File None None next ctx
            }

    [<CLIMutable>]
    type MangaGetResponse = {
        Id: Guid
        Title: string
        BookmarkUrl: string
        NumberOfChapters: {|
            NotDownloaded: int
            Downloaded: int
            Archived: int
            Ignored: int
            Total: int
        |}
        ChapterIndex: int
        Direction: Direction
        SourceUrl: string
        Updated: DateTime
    }

    let getMangaHandler: HttpHandler =
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

    [<CLIMutable>]
    type ChapterGetResponse = {
        MangaId: Guid
        MangaTitle: string
        Direction: Direction
        ChapterId: Guid
        ChapterTitle: string option
        PreviousChapterUrl: string option
        NextChapterUrl: string option
        OtherChapters:
            {|
                Id: Guid
                Title: string
                Url: string
                DownloadStatus: DownloadStatus
            |}[]
        DownloadStatus: DownloadStatus
        Pages:
            {|
                Id: Guid
                Name: string
                Width: int
                Height: int
            |}[]
    }

    let getChapterHandler (chapterId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()

                let! manga =
                    db.Manga
                        .Include(fun manga ->
                            manga.Chapters
                                .Where(fun chapter -> chapter.Title <> null)
                                .OrderBy(fun chapter -> chapter.Index)
                            :> IEnumerable<_>)
                        .ThenInclude(fun (chapter: Chapter) -> chapter.Pages.OrderBy(fun page -> page.Name))
                        .AsSplitQuery()
                        .FirstAsync(fun manga -> manga.Chapters.Select(fun c -> c.Id).Contains(chapterId))

                manga.Accessed <- DateTime.UtcNow
                let! _ = db.SaveChangesAsync()
                let chapter = manga.Chapters |> Seq.find (fun c -> c.Id = chapterId)

                let getQueryParams (page: Page) =
                    match manga.Direction with
                    | Direction.Horizontal -> $"?page=%s{page.Name}"
                    | Direction.Vertical -> ""
                    | _ -> ArgumentOutOfRangeException() |> raise

                let response = {
                    MangaId = chapter.MangaId
                    MangaTitle = manga.Title
                    Direction = chapter.Manga.Direction
                    ChapterId = chapter.Id
                    ChapterTitle = Option.ofObj chapter.Title
                    PreviousChapterUrl =
                        tryPreviousChapter manga chapter
                        |> Option.map (fun c ->
                            let queryParams =
                                c.Pages
                                |> Seq.sortBy (fun p -> p.Name)
                                |> Seq.tryLast
                                |> Option.map getQueryParams
                                |> Option.defaultValue ""

                            $"/chapters/%A{c.Id}/%s{slugify manga.Title}/%s{c.Title}%s{queryParams}")
                    NextChapterUrl =
                        tryNextChapter manga chapter
                        |> Option.map (fun c ->
                            let queryParams =
                                c.Pages
                                |> Seq.sortBy (fun p -> p.Name)
                                |> Seq.tryHead
                                |> Option.map getQueryParams
                                |> Option.defaultValue ""

                            $"/chapters/%A{c.Id}/%s{slugify manga.Title}/%s{c.Title}%s{queryParams}")
                    OtherChapters =
                        manga.Chapters
                        |> Seq.map (fun chapter -> {|
                            Id = chapter.Id
                            Title = chapter.Title
                            Url = $"/chapters/%A{chapter.Id}/%s{slugify manga.Title}/%s{chapter.Title}"
                            DownloadStatus = chapter.DownloadStatus
                        |})
                        |> Seq.toArray
                    DownloadStatus = chapter.DownloadStatus
                    Pages =
                        chapter.Pages
                        |> Seq.distinctBy (fun page -> page.Name)
                        |> Seq.sortBy (fun page -> page.Name)
                        |> Seq.map (fun page -> {|
                            Id = page.Id
                            Name = page.Name
                            Width = page.Width
                            Height = page.Height
                        |})
                        |> Seq.toArray
                }

                return! json response next ctx
            }

    let serveSpa: HttpHandler =
        fun next ctx ->
            task {
                let provider = ctx.GetService<IFileProvider>()
                use stream = provider.GetFileInfo("index.html").CreateReadStream()
                return! streamData true stream None None next ctx
            }

    let checkUpdateHandler (mangaId: Guid) : HttpHandler =
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

    let checkAllUpdatesHandler : HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()
                let! allManga =
                    db.Manga
                        .AsNoTracking()
                        .OrderBy(fun m -> m.Title)
                        .Select(fun m -> {| Id = m.Id; Title = m.Title; Url = m.Url |})
                        .ToListAsync()

                // Load existing update jobs once so we can reuse them
                let! existingJobs =
                    db.DownloadJobs
                        .AsNoTracking()
                        .Where(fun j -> j.Type = JobType.UpdateManga)
                        .ToListAsync()

                let manager = ctx.GetService<DownloadManager>()

                // For each manga, either reuse an existing job or enqueue a new one
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
                        // Refresh existing job state instead of creating a duplicate
                        do! manager.UpdateStatus(job.Id, JobStatus.Pending, None)
                    | None ->
                        let! _ = manager.QueueUpdate(m.Id, m.Title, m.Url)
                        ()

                return! Successful.NO_CONTENT next ctx
            }

    let clearCompletedDownloadsHandler : HttpHandler =
        fun next ctx ->
            task {
                let manager = ctx.GetService<DownloadManager>()
                do! manager.ClearCompleted()
                return! Successful.NO_CONTENT next ctx
            }

    let moveJobTopHandler (jobId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let manager = ctx.GetService<DownloadManager>()
                do! manager.MoveToTop(jobId)
                return! Successful.NO_CONTENT next ctx
            }

    let moveJobBottomHandler (jobId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let manager = ctx.GetService<DownloadManager>()
                do! manager.MoveToBottom(jobId)
                return! Successful.NO_CONTENT next ctx
            }

    let cancelJobHandler (jobId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let manager = ctx.GetService<DownloadManager>()
                do! manager.CancelJob(jobId)
                return! Successful.NO_CONTENT next ctx
            }

    let endpoints = [
        GET [
            routef "/pages/%O" (fun id ->
                privateResponseCaching (int (TimeSpan.FromDays(365).TotalSeconds)) None
                >=> servePage id)
        ]
        subRoute "/api" [
            GET [ 
                route "/manga" getMangaHandler
                routef "/chapters/%O" getChapterHandler 
                route "/downloads" getDownloadsHandler
            ]
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
                routef "/jobs/%O/move-top" moveJobTopHandler
                routef "/jobs/%O/move-bottom" moveJobBottomHandler
                routef "/jobs/%O/cancel" cancelJobHandler
            ]
            DELETE [
                routef "/manga/%O" (fun id -> deleteMangaHandler id)
                route "/downloads" clearCompletedDownloadsHandler
            ]
        ]
        GET [ route "{*rest}" serveSpa ]
    ]

    let defaultPort = 8080

    let getIndexUrl (port: int option) =
        let p = Option.defaultValue defaultPort port
        $"http://localhost:%i{p}"

    let getMangaUrl (port: int option) (manga: Manga) =
        $"%s{getIndexUrl port}/%s{getBookmarkUrl manga}/"

    type AssemblyMarker() = class end

    let create (port: int option) =
        let hostBuilder = Host.CreateDefaultBuilder()

        hostBuilder.ConfigureAppConfiguration(fun configBuilder ->
            configBuilder.Sources.OfType<JsonConfigurationSource>()
            |> Seq.iter (fun source -> source.ReloadOnChange <- false))

        hostBuilder.UseSerilog(fun _ config ->
            config.Enrich.FromLogContext()

            config.WriteTo.Console(
                outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}"
            )
            // Disable default ASP.NET Core logging because we are using SerilogRequestLogging instead
            config.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            |> ignore)

        hostBuilder.ConfigureWebHostDefaults(fun webHostBuilder ->

            webHostBuilder.UseUrls(getIndexUrl port)

            let fileProvider =
                ManifestEmbeddedFileProvider(Assembly.GetAssembly(typeof<AssemblyMarker>), "wwwroot")

            webHostBuilder.ConfigureServices(fun services ->
                services.AddGiraffe() |> ignore
                services.AddCoreMangaServices() |> ignore
                services.AddSingleton<IFileProvider>(fileProvider) |> ignore
                services
                    .AddHttpClient(fun hc -> hc.Timeout <- TimeSpan.FromSeconds(20.))
                    .AddPolicyHandler(fun services _ ->
                        let logger = services.GetRequiredService<ILogger<HttpClient>>()
                        HttpPolicyExtensions
                            .HandleTransientHttpError()
                            .WaitAndRetryAsync(
                                3,
                                (fun n -> TimeSpan.FromSeconds(2. ** n)),
                                (fun _ delay -> logger.LogError("Retrying request after {Delay}", delay))
                            )
                        :> IAsyncPolicy<HttpResponseMessage>) |> ignore
                )

            webHostBuilder.Configure(fun app ->
                app.UseStaticFiles(StaticFileOptions(FileProvider = fileProvider, RequestPath = ""))
                app.UseStatusCodePages()
                app.UseDeveloperExceptionPage()
                app.UseSerilogRequestLogging(fun options -> options.IncludeQueryInRequestPath <- true)
                app.UseRouting()

                app.UseEndpoints(fun options -> options.MapGiraffeEndpoints(endpoints))
                |> ignore)
            |> ignore)

        hostBuilder.Build()
