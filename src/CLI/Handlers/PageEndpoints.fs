namespace MangaSharp.CLI.Server

open System
open Microsoft.EntityFrameworkCore
open Giraffe
open Giraffe.EndpointRouting
open MangaSharp.Database

module PageEndpoints =

    let private servePage (pageId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()
                let! page = db.Pages.AsNoTracking().FirstAsync(fun p -> p.Id = pageId)
                return! streamFile true page.File None None next ctx
            }

    let routes : Endpoint list =
        [
            GET [
                routef "/pages/%O" (fun id ->
                    privateResponseCaching (int (TimeSpan.FromDays(365.).TotalSeconds)) None
                    >=> servePage id)
            ]
        ]
