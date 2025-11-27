#nowarn "20"
namespace MangaSharp.CLI

open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Diagnostics
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System
open System.IO
open System.Linq
open System.Net.Http
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.Encodings.Web
open Giraffe
open Polly
open Polly.Extensions.Http

open MangaSharp.Database
open MangaSharp.Extractors
open MangaSharp.Extractors.MangaDex
open MangaSharp.Extractors.Util

[<AutoOpen>]
module Extensions =
    open MangaSharp
    open System.Reflection

    type IServiceCollection with

        member this.AddMangaContext() =
            let serviceProvider = this.BuildServiceProvider()
            let env = serviceProvider.GetRequiredService<IHostEnvironment>()
            let dbFile = Path.Combine(mangaData, "manga.db")

            this.AddDbContext<MangaContext>(fun options ->
                options.UseSqlite($"Data Source=%s{dbFile};foreign keys=true")
                options.EnableSensitiveDataLogging()

                options.ConfigureWarnings(fun w ->
                    if not (env.IsDevelopment()) then
                        w.Ignore(CoreEventId.SensitiveDataLoggingEnabledWarning) |> ignore

                    w.Throw(RelationalEventId.MultipleCollectionIncludeWarning) |> ignore)
                |> ignore)

        member this.AddJsonSerializer() =
            let serializationOptions =
                JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

            serializationOptions.Converters.Add(JsonStringEnumConverter())

            JsonFSharpOptions
                .Default()
                .WithUnionNamedFields()
                .WithUnionUnwrapFieldlessTags()
                .WithUnionUnwrapRecordCases()
                .AddToJsonSerializerOptions(serializationOptions)

            this.AddSingleton<Json.ISerializer>(Json.Serializer(serializationOptions))

        member this.AddCoreMangaServices() =
            let versionInfo = {
                Version =
                    Assembly
                        .GetEntryAssembly()
                        .GetCustomAttributes<AssemblyMetadataAttribute>()
                        .First(fun a -> a.Key = "GitTag")
                        .Value
            }

            this.AddSingleton<VersionInfo>(versionInfo)
            this.AddMangaContext()
            this.AddTransient<MangaService>()
            this.AddTransient<MangaRepository>()
            this.AddTransient<DownloadJobRepository>()
            this.AddJsonSerializer() |> ignore

            this.AddHttpClient(fun hc -> hc.Timeout <- TimeSpan.FromSeconds(20.))
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

            this.AddSingleton<PageSaver>()
            this.AddTransient<MangaDexApi>()
            this.AddTransient<IMangaExtractor, MangaDexExtractor>()
            this.AddTransient<IMangaExtractor, ManganatoExtractor>()
            this.AddTransient<IMangaExtractor, Manwha18CCExtractor>()
            this.AddTransient<IMangaExtractor, Manwha18Extractor>()
            this.AddTransient<IMangaExtractor, ManyToonExtractor>()
            this.AddTransient<IMangaExtractor, WebToonExtractor>()
            this.AddTransient<MangaDownloaderService>()
            this.AddScoped<DownloadManager>()
            this.AddHostedService<DownloadWorker>()
