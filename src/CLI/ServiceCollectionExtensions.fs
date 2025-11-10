#nowarn "20"
namespace MangaSharp.CLI

open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Diagnostics
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.Encodings.Web
open Giraffe

open MangaSharp.Database
open MangaSharp.Extractors.Util

[<AutoOpen>]
module Extensions =

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
