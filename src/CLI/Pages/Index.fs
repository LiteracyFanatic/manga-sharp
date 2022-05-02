[<RequireQualifiedAccess>]
module MangaSharp.CLI.Server.Pages.Index

open System.Linq
open Microsoft.EntityFrameworkCore
open Giraffe
open Giraffe.ViewEngine
open MangaSharp.CLI.Util
open MangaSharp.Database
open MangaSharp.Database.MangaDomain

let mangaTable (manga: Manga list) (tableTitle: string) =
    table [ _class "table is-bordered is-striped" ] [
        yield caption [ _class "is-size-3" ] [ str tableTitle ]
        yield thead [] [
            tr [] [
                th [] [ str "Title" ]
                th [] [ str "Direction" ]
                th [] [ str "Source" ]
                th [] [ str "Progress" ]
            ]
        ]
        for m in manga ->
            let chapters =
                m.Chapters
                |> Seq.filter (fun c -> c.DownloadStatus = Downloaded)
                |> Seq.sortBy (fun c -> c.Index)
            let chapterIndex =
                match m.BookmarkChapterId with
                | Some chapterId ->
                    chapters
                    |> Seq.findIndex (fun c -> c.Id = chapterId)
                | None -> 0
            tr [] [
                td [ _width "50%" ] [ a [ _href (getBookmarkUrl m) ] [ str m.Title ] ]
                td [ _width "10%" ] [ str (m.Direction.ToString()) ]
                td [ _width "30%" ] [ a [ _href m.Url ] [ str m.Url ] ]
                td [ _width "10%" ] [ str $"%i{1 + chapterIndex}/%i{Seq.length chapters}" ]
            ]
    ]

let view (allManga: Manga list) (recentManga: Manga list) =
    html [] [
        head [] [
            meta [ _name "viewport"; _content "width=device-width, initial-scale=1"]
            meta [ _charset "utf-8" ]
            title [] [ str "MangaSharp - Index" ]
            link [ _rel "stylesheet"; _href "/assets/bulma.min.css" ]
            link [ _rel "stylesheet"; _href "/index.css" ]
            link [ _rel "icon"; _href "/favicon.ico"; _type "image/x-icon" ]
        ]
        body [] [
            mangaTable recentManga "Recent"
            mangaTable allManga "All"
        ]
    ]

let handler: HttpHandler =
    fun next ctx ->
        task {
            let db = ctx.GetService<MangaContext>()
            let! all =
                db.Manga
                    .Include("_BookmarkChapter")
                    .Include("_BookmarkPage")
                    .Include(fun m -> m.Chapters)
                    .OrderBy(fun m -> m.Title)
                    .ToListAsync()
            let all = List.ofSeq all
            let recent =
                all
                |> List.sortByDescending (fun m -> m.Accessed)
                |> List.truncate 5
            return! htmlView (view all recent) next ctx
        }
