#nowarn "20"
namespace MangaSharp.CLI.Server

open System
open System.Reflection
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.FileProviders
open Giraffe
open Giraffe.EndpointRouting
open MangaSharp.Database
open MangaSharp.Database.MangaDomain
open MangaSharp.CLI.Server.Pages
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

    let putBookmarkHandler (mangaId: Guid) (request: PutBookmarkRequest): HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()
                let! manga = db.Manga.SingleAsync(fun m -> m.Id = mangaId)
                manga.BookmarkChapterId <- Some request.ChapterId
                manga.BookmarkPageId <- request.PageId
                let! _ = db.SaveChangesAsync()
                return! Successful.NO_CONTENT next ctx
            }

    let servePage (pageId: Guid): HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()
                let! page =
                    db.Pages
                        .AsNoTracking()
                        .FirstAsync(fun p -> p.Id = pageId)
                return! streamFile true page.File None None next ctx
            }

    let endpoints =
        [
            GET [
                route "/" Index.handler
                routef "/chapters/%O/{*rest}" Read.handler
                routef "/pages/%O" servePage
            ]
            subRoute "/api" [
                PUT [
                    routef "/manga/%O/bookmark" (putBookmarkHandler >> bindJson<PutBookmarkRequest>)
                ]
            ]
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

        hostBuilder.UseSerilog(fun _ config ->
            config.Enrich.FromLogContext()
            config.WriteTo.Console()
            // Disable default ASP.NET Core logging because we are using SerilogRequestLogging instead
            config.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning) |> ignore)

        hostBuilder.ConfigureWebHostDefaults(fun webHostBuilder ->

            webHostBuilder.UseUrls(getIndexUrl port)

            webHostBuilder.ConfigureServices(fun services ->
                services.AddMangaContext()
                services.AddJsonSerializer() |> ignore)

            webHostBuilder.Configure(fun app ->
                let provider = ManifestEmbeddedFileProvider(Assembly.GetAssembly(typeof<AssemblyMarker>), "wwwroot")
                app.UseStaticFiles(StaticFileOptions(FileProvider = provider, RequestPath = ""))
                app.UseStatusCodePages()
                app.UseDeveloperExceptionPage()
                app.UseSerilogRequestLogging(fun options -> options.IncludeQueryInRequestPath <- true)
                app.UseRouting()
                app.UseEndpoints(fun options -> options.MapGiraffeEndpoints(endpoints)) |> ignore) |> ignore)

        hostBuilder.Build()
