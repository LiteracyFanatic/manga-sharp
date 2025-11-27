#nowarn "20"
namespace MangaSharp.CLI.Server

open System
open System.Reflection
open System.Linq
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration.Json
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Giraffe
open Giraffe.EndpointRouting
open Serilog
open Serilog.Events
open Polly
open Polly.Extensions.Http
open System.Net.Http
open MangaSharp.Database
open MangaSharp.CLI
open MangaSharp.CLI.Util

module WebApp =

    let private apiRoutes =
        List.concat [
            MangaEndpoints.routes
            ChapterEndpoints.routes
            DownloadEndpoints.routes
        ]

    let private endpoints =
        List.concat [
            PageEndpoints.routes
            [ subRoute "/api" apiRoutes ]
            SpaEndpoints.routes
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
