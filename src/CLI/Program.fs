#nowarn "20"
open Argu
open EntityFrameworkCore.FSharp.DbContextHelpers
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Configuration
open Polly
open System
open System.Linq
open System.Reflection
open Serilog

open MangaSharp.CLI
open MangaSharp.CLI.Arguments
open MangaSharp.CLI.Server
open MangaSharp.Database
open MangaSharp.Extractors
open MangaSharp.Extractors.MangaDex
open MangaSharp.Extractors.Util

let getCliApp () =
    // Don't use Host.CreateDefaultBuilder() because it registers a FileWatcher
    // in the current working directory which can massively affect startup time
    let builder = HostBuilder()

    // Host.CreateDefaultBuilder() normally does this for us
    builder.ConfigureHostConfiguration(fun config -> config.AddEnvironmentVariables("DOTNET_") |> ignore)

    builder.UseConsoleLifetime()

    builder.UseSerilog(fun ctx _ config ->
        config.Enrich.FromLogContext()
        config.WriteTo.Console()
        // Don't log EF Core queries or HTTP requests to the console except in
        // development
        if not (ctx.HostingEnvironment.IsDevelopment()) then
            config.MinimumLevel.Override("System.Net.Http.HttpClient", Events.LogEventLevel.Warning)
            config.MinimumLevel.Override("Microsoft", Events.LogEventLevel.Warning) |> ignore)

    builder.ConfigureServices(fun services ->
        services.AddMangaContext()
        services.AddTransient<MangaRepository>()
        services.AddJsonSerializer()

        services.AddHttpClient(fun hc ->
            hc.Timeout <- TimeSpan.FromSeconds(20.))
            .AddTransientHttpErrorPolicy(fun policyBuilder ->
                policyBuilder.WaitAndRetryAsync(3, fun n -> TimeSpan.FromSeconds(2 ** n)))
        services.AddSingleton<PageSaver>()
        services.AddTransient<MangaDexApi>()
        services.AddTransient<IMangaExtractor, MangaDexExtractor>()
        services.AddTransient<IMangaExtractor, ManganatoExtractor>()
        services.AddTransient<IMangaExtractor, Manwha18CCExtractor>()
        services.AddTransient<IMangaExtractor, Manwha18Extractor>()
        services.AddTransient<IMangaExtractor, ManyToonExtractor>()
        services.AddTransient<IMangaExtractor, WebToonExtractor>()
        services.AddTransient<Application>() |> ignore)

    let host = builder.Build()
    let scope = host.Services.CreateScope()
    scope.ServiceProvider.GetRequiredService<Application>()

let read (args: ParseResults<ReadArgs>) =
    let port = args.TryGetResult(Port)
    let openInBrowser = not (args.Contains(No_Open))
    let server = WebApp.create port
    let manga =
        using (server.Services.CreateScope()) (fun scope ->
            let db = scope.ServiceProvider.GetRequiredService<MangaContext>()
            match args.Contains(Last), args.TryGetResult(Title) with
            | true, Some _ ->
                args.Raise("Cannot specify --last and --title at the same time.")
            | true, None ->
                db.Manga
                    .AsNoTracking()
                    .Include("_BookmarkChapter")
                    .Include("_BookmarkPage")
                    .Include(fun m -> m.Chapters)
                    .OrderByDescending(fun m -> m.Accessed)
                    .TryFirst()
            | false, Some title ->
                db.Manga
                    .AsNoTracking()
                    .Include("_BookmarkChapter")
                    .Include("_BookmarkPage")
                    .Include(fun m -> m.Chapters)
                    .TryFirst(fun m -> m.Title = title)
            | false, None -> None)
    if openInBrowser then
        let url =
            match manga with
            | Some m -> WebApp.getMangaUrl port m
            | None -> WebApp.getIndexUrl port
        let lifetime = server.Services.GetRequiredService<IHostApplicationLifetime>()
        lifetime.ApplicationStarted.Register(fun () -> openInDefaultApp url |> ignore) |> ignore
    server.Run()

[<EntryPoint>]
let main argv =
    let parser =
        ArgumentParser.Create<Args>(
            programName="manga",
            helpTextMessage="Download, update, and read manga from a variety of sites.",
            errorHandler = ProcessExiter())
    let results = parser.ParseCommandLine(argv)
    match results.GetSubCommand() with
    | Download downloadArgs -> (getCliApp ()).Download(downloadArgs)
    | Update updateArgs -> (getCliApp ()).Update(updateArgs)
    | Read readArgs -> read readArgs
    | Ls lsArgs -> (getCliApp ()).Ls(lsArgs)
    | Version ->
        let version =
            Assembly.GetEntryAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .First(fun a -> a.Key = "GitTag")
                .Value
        printfn "%s" version

    0
