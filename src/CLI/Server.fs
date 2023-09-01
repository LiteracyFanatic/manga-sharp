#nowarn "20"
namespace MangaSharp.CLI.Server

open System
open System.Reflection
open System.Collections.Generic
open System.Linq
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.FileProviders
open Giraffe
open Giraffe.EndpointRouting
open MangaSharp.Database
open MangaSharp.Database.MangaDomain
open MangaSharp.CLI
open MangaSharp.CLI.Util
open Serilog
open Serilog.Events

module WebApp =

    [<CLIMutable>]
    type PutBookmarkRequest = {
        ChapterId: Guid
        PageId: Guid option
    }

    let putBookmarkHandler (mangaId: Guid) (request: PutBookmarkRequest) : HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()
                let! manga = db.Manga.SingleAsync(fun m -> m.Id = mangaId)
                manga.BookmarkChapterId <- Some request.ChapterId
                manga.BookmarkPageId <- request.PageId
                let! _ = db.SaveChangesAsync()
                return! Successful.NO_CONTENT next ctx
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
        NumberOfChapters: int
        ChapterIndex: int
        Direction: Direction
        SourceUrl: string
    }

    let getMangaHandler: HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()

                let! manga =
                    db.Manga
                        .Include("_BookmarkChapter")
                        .Include("_BookmarkPage")
                        .Include(fun m -> m.Chapters)
                        .OrderBy(fun m -> m.Title)
                        .ToListAsync()

                let response =
                    manga
                    |> Seq.map (fun m ->
                        let chapters =
                            m.Chapters
                            |> Seq.filter (fun c -> c.DownloadStatus = Downloaded || c.DownloadStatus = Archived)
                            |> Seq.sortBy (fun c -> c.Index)

                        let chapterIndex =
                            match m.BookmarkChapterId with
                            | Some chapterId -> chapters |> Seq.findIndex (fun c -> c.Id = chapterId)
                            | None -> 0

                        {
                            Id = m.Id
                            Title = m.Title
                            BookmarkUrl = getBookmarkUrl m
                            NumberOfChapters = chapters.Count()
                            ChapterIndex = chapterIndex
                            Direction = m.Direction
                            SourceUrl = m.Url
                        })

                return! json response next ctx
            }

    [<CLIMutable>]
    type ChapterGetResponse = {
        MangaId: Guid
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
                                .Where(fun chapter -> chapter.Title.IsSome)
                                .OrderBy(fun chapter -> chapter.Index)
                            :> IEnumerable<_>)
                        .ThenInclude(fun (chapter: Chapter) -> chapter.Pages.OrderBy(fun page -> page.Name))
                        .AsSplitQuery()
                        .FirstAsync(fun manga -> manga.Chapters.Select(fun c -> c.Id).Contains(chapterId))

                manga.Accessed <- Some DateTime.UtcNow
                let! _ = db.SaveChangesAsync()
                let chapter = manga.Chapters |> Seq.find (fun c -> c.Id = chapterId)

                let getQueryParams (page: Page) =
                    match manga.Direction with
                    | Horizontal -> $"?page=%s{page.Name}"
                    | Vertical -> ""

                let response = {
                    MangaId = chapter.MangaId
                    Direction = chapter.Manga.Direction
                    ChapterId = chapter.Id
                    ChapterTitle = chapter.Title
                    PreviousChapterUrl =
                        tryPreviousChapter manga chapter
                        |> Option.map (fun c ->
                            let queryParams =
                                c.Pages
                                |> Seq.sortBy (fun p -> p.Name)
                                |> Seq.tryLast
                                |> Option.map getQueryParams
                                |> Option.defaultValue ""

                            $"/chapters/%A{c.Id}/%s{slugify manga.Title}/%s{c.Title.Value}%s{queryParams}")
                    NextChapterUrl =
                        tryNextChapter manga chapter
                        |> Option.map (fun c ->
                            let queryParams =
                                c.Pages
                                |> Seq.sortBy (fun p -> p.Name)
                                |> Seq.tryHead
                                |> Option.map getQueryParams
                                |> Option.defaultValue ""

                            $"/chapters/%A{c.Id}/%s{slugify manga.Title}/%s{c.Title.Value}%s{queryParams}")
                    OtherChapters =
                        manga.Chapters
                        |> Seq.map (fun chapter -> {|
                            Id = chapter.Id
                            Title = chapter.Title.Value
                            Url = $"/chapters/%A{chapter.Id}/%s{slugify manga.Title}/%s{chapter.Title.Value}"
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

    let endpoints = [
        GET [
            routef "/pages/%O" (fun id ->
                privateResponseCaching (int (TimeSpan.FromDays(365).TotalSeconds)) None
                >=> servePage id)
        ]
        subRoute "/api" [
            GET [ route "/manga" getMangaHandler; routef "/chapters/%O" getChapterHandler ]
            PUT [
                routef "/manga/%O/bookmark" (putBookmarkHandler >> bindJson<PutBookmarkRequest>)
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

    type AssemblyMarker() =
        class
        end

    let create (port: int option) =
        let hostBuilder = Host.CreateDefaultBuilder()

        hostBuilder.UseSerilog(fun _ config ->
            config.Enrich.FromLogContext()
            config.WriteTo.Console()
            // Disable default ASP.NET Core logging because we are using SerilogRequestLogging instead
            config.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            |> ignore)

        hostBuilder.ConfigureWebHostDefaults(fun webHostBuilder ->

            webHostBuilder.UseUrls(getIndexUrl port)

            let fileProvider =
                ManifestEmbeddedFileProvider(Assembly.GetAssembly(typeof<AssemblyMarker>), "wwwroot")

            webHostBuilder.ConfigureServices(fun services ->
                services.AddMangaContext()
                services.AddJsonSerializer()
                services.AddSingleton<IFileProvider>(fileProvider) |> ignore)

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
