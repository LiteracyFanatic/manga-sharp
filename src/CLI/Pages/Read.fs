[<RequireQualifiedAccess>]
module MangaSharp.CLI.Server.Pages.Read

open System
open System.Collections.Generic
open System.Linq
open Microsoft.EntityFrameworkCore
open Giraffe
open Giraffe.ViewEngine
open MangaSharp.CLI.Util
open MangaSharp.Database
open MangaSharp.Database.MangaDomain

let chapterSelect (manga: Manga) (chapter: Chapter) =
    div [ _class "control" ] [
        div [ _class "select"] [
            select [ _id "chapter-select" ] [
                for c in manga.Chapters.Where(fun c -> c.DownloadStatus = Downloaded).OrderBy(fun c -> c.Index) ->
                    option [
                        _value $"/chapters/%A{c.Id}/%s{slugify manga.Title}/%s{c.Title.Value}"
                        if c.Id = chapter.Id then attr "selected" ""
                    ] [ str ($"Chapter %s{c.Title.Value}") ]
            ]
        ]
    ]

let pageSelect (chapter: Chapter) =
    div [ _class "control" ] [
        div [ _class "select"] [
            select [ _id "page-select" ] [
                for p in chapter.Pages.OrderBy(fun p -> p.Name) ->
                    option [ _value (string p.Id) ] [
                        str ($"Page %i{int p.Name}")
                    ]
            ]
        ]
    ]

let homeButton =
    div [ _class "control" ] [
        a [ _class "button"; _href "/" ] [
            span [ _class "icon" ] [
                tag "svg" [ _style "width: 24px; height: 24px"; attr "viewBox" "0 0 24 24"] [
                    tag "path" [ attr "fill" "#000000"; attr "d" "M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z" ] []
                ]
            ]
        ]
    ]

let view (manga: Manga) (chapter: Chapter) =
    let getHash (page: Page) =
        match manga.Direction with
        | Horizontal -> $"#%s{page.Name}"
        | Vertical -> ""
    let previousLink =
        tryPreviousChapter manga chapter
        |> Option.map (fun c -> $"/chapters/%A{c.Id}/%s{slugify manga.Title}/%s{c.Title.Value}%s{getHash (c.Pages.OrderBy(fun p -> p.Name).Last())}")
    let nextLink =
        tryNextChapter manga chapter
        |> Option.map (fun c -> $"/chapters/%A{c.Id}/%s{slugify manga.Title}/%s{c.Title.Value}%s{getHash (c.Pages.OrderBy(fun p -> p.Name).First())}")

    html [] [
        head [] [
            meta [ _name "viewport"; _content "width=device-width, initial-scale=1"]
            meta [ _charset "utf-8" ]
            title [] [ str $"MangaSharp - %s{manga.Title} - %s{chapter.Title.Value}" ]
            link [ _rel "stylesheet"; _href "/assets/bulma.min.css" ]
            match manga.Direction with
            | Horizontal ->
                link [ _rel "stylesheet"; _href "/horizontal.css" ]
            | Vertical ->
                link [ _rel "stylesheet"; _href "/vertical.css" ]
            link [ _rel "icon"; _href "/favicon.ico"; _type "image/x-icon" ]
            script [ _src "/manga.js" ] []
        ]
        body [
            if previousLink.IsSome then attr "data-previous-page" previousLink.Value
            if nextLink.IsSome then attr "data-next-page" nextLink.Value
            attr "data-manga-id" (string manga.Id)
            attr "data-direction" (string manga.Direction)
            attr "data-chapter-id" (string chapter.Id)
        ] [
            div [ _id "select-container"; _class "field is-grouped" ] [
                homeButton
                chapterSelect manga chapter
                if manga.Direction = Horizontal then pageSelect chapter
            ]
            div [ _id "image-container" ] [
                for p in chapter.Pages.OrderBy(fun p -> p.Name) ->
                    img [
                        attr "data-page" p.Name
                        attr "data-page-id"(string p.Id)
                        _src $"/pages/%A{p.Id}"
                    ]
            ]
        ]
    ]

let handler (chapterId: Guid): HttpHandler =
    fun next ctx ->
        task {
            let db = ctx.GetService<MangaContext>()
            let! manga =
                db.Manga
                    .Include(fun manga -> manga.Chapters :> IEnumerable<_>)
                    .ThenInclude(fun (chapter: Chapter) -> chapter.Pages)
                    .AsSplitQuery()
                    .FirstAsync(fun manga -> manga.Chapters.Select(fun c -> c.Id).Contains(chapterId))
            manga.Accessed <- Some DateTime.UtcNow
            let! _ = db.SaveChangesAsync()
            let chapter = manga.Chapters |> Seq.find (fun c -> c.Id = chapterId)
            return! htmlView (view manga chapter) next ctx
        }
