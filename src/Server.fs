module MangaSharp.Server

open System.IO
open System.Web
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Giraffe
open Giraffe.ViewEngine
open Serilog

let private chapterSelect (manga: StoredManga) (chapter: Chapter) =
    div [ _class "control" ] [
        div [ _class "select"] [
            select [ _id "chapter-select" ] [
                for c in manga.Chapters ->
                    option [
                        _value c.Title
                        if c = chapter then attr "selected" ""
                    ] [ encodedText ($"Chapter %s{c.Title}") ]
            ]
        ]
    ]

let private pageSelect (chapter: Chapter) =
    div [ _class "control" ] [
        div [ _class "select"] [
            select [ _id "page-select" ] [
                for p in chapter.Pages ->
                    option [ _value p.Name ] [
                        encodedText ($"Page %i{int p.Name}")
                    ]
            ]
        ]
    ]

let private homeButton =
    div [ _class "control" ] [
        a [ _class "button"; _href "/" ] [
            span [ _class "icon" ] [
                tag "svg" [ _style "width: 24px; height: 24px"; attr "viewBox" "0 0 24 24"] [
                    tag "path" [ attr "fill" "#000000"; attr "d" "M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z" ] []
                ]
            ]
        ]
    ]

let private mangaTable (mangaListing: MangaListing list) (tableTitle: string) =
    table [ _class "table is-bordered is-striped" ] [
        yield caption [ _class "is-size-3" ] [ encodedText tableTitle ]
        yield thead [] [
            tr [] [
                th [] [ encodedText "Title" ]
                th [] [ encodedText "Direction" ]
                th [] [ encodedText "Source" ]
                th [] [ encodedText "Progress" ]
            ]
        ]
        for m in mangaListing ->
            let link =
                match m.Bookmark with
                | Some b -> Bookmark.toUrl m.Title b
                | None -> MangaListing.firstPage m

            tr [] [
                td [ _width "50%" ] [ a [ _href link ] [ encodedText m.Title ] ]
                td [ _width "10%" ] [ encodedText (m.Source.Direction.ToString()) ]
                td [ _width "30%" ] [ a [ _href m.Source.Url ] [ encodedText m.Source.Url ] ]
                td [ _width "10%" ] [ encodedText $"%i{1 + m.ChapterIndex}/%i{m.NumberOfChapters}" ]
            ]
    ]

let private index (allManga: MangaListing list) (recentManga: MangaListing list) =
    html [] [
        head [] [
            meta [ _name "viewport"; _content "width=device-width, initial-scale=1"]
            meta [ _charset "utf-8" ]
            title [] [ encodedText "MangaSharp - Index" ]
            link [ _rel "stylesheet"; _href "/assets/bulma.min.css" ]
            link [ _rel "stylesheet"; _href "/index.css" ]
        ]
        body [] [
            mangaTable recentManga "Recent"
            mangaTable allManga "All"
        ]
    ]
    |> htmlView

let private mangaPage (port: int) (manga: StoredManga) (chapter: Chapter) =
    let getHash (page: Page) =
        match manga.Source.Direction with
        | Horizontal -> $"#%s{page.Name}"
        | Vertical -> ""
    let previousLink =
        Manga.tryPreviousChapter manga chapter
        |> Option.map (fun c -> $"/manga/%s{HttpUtility.UrlEncode manga.Title}/%s{c.Title}%s{getHash (List.last c.Pages)}")
    let nextLink =
        Manga.tryNextChapter manga chapter
        |> Option.map (fun c -> $"/manga/%s{HttpUtility.UrlEncode manga.Title}/%s{c.Title}%s{getHash (List.head c.Pages)}")

    html [] [
        head [] [
            meta [ _name "viewport"; _content "width=device-width, initial-scale=1"]
            meta [ _charset "utf-8" ]
            title [] [ encodedText $"MangaSharp - %s{manga.Title} - %s{chapter.Title}" ]
            link [ _rel "stylesheet"; _href "/assets/bulma.min.css" ]
            match manga.Source.Direction with
            | Horizontal ->
                link [ _rel "stylesheet"; _href "/horizontal.css" ]
            | Vertical ->
                link [ _rel "stylesheet"; _href "/vertical.css" ]
            script [ _src "/manga.js" ] []
        ]
        body [
            if previousLink.IsSome then attr "data-previous-page" previousLink.Value
            if nextLink.IsSome then attr "data-next-page" nextLink.Value
            attr "data-manga" (HttpUtility.UrlEncode manga.Title)
            attr "data-direction" (manga.Source.Direction.ToString())
            attr "data-chapter" chapter.Title
            attr "data-port" (string port)
        ] [
            div [ _id "select-container"; _class "field is-grouped" ] [
                homeButton
                chapterSelect manga chapter
                if manga.Source.Direction = Horizontal then pageSelect chapter
            ]
            div [ _id "image-container" ] [
                for p in chapter.Pages ->
                    img [
                        attr "data-page" p.Name;
                        _src $"/manga/%s{HttpUtility.UrlEncode manga.Title}/%s{chapter.Title}/%s{p.File}"
                    ]
            ]
        ]
    ]
    |> htmlView

let private setLastManga =
    handleContext (fun ctx ->
        task {
            use reader = new StreamReader(ctx.Request.Body)
            let! body = reader.ReadToEndAsync()
            Manga.setLast (HttpUtility.UrlDecode body)
            return Some ctx
        })
    >=> Successful.NO_CONTENT

let private setBookmark (mangaTitle: string) =
    handleContext (fun ctx ->
        task {
            use reader = new StreamReader(ctx.Request.Body)
            let! body = reader.ReadToEndAsync()
            let manga = Manga.fromTitle mangaTitle
            let bookmarkPath = Path.Combine(mangaData, manga.Title, "bookmark")
            do! File.WriteAllTextAsync(bookmarkPath, $"%s{body}\n")
            return Some ctx
        })
    >=> Successful.NO_CONTENT

let private webApp (port: int) =
    choose [
        GET >=> choose [
            route "/" >=> warbler (fun _ ->
                let mangaListing = MangaListing.getAll ()
                index mangaListing (MangaListing.getRecent ())
            )
            subRoutef "/manga/%s/%s" (fun (m, c) ->
                let mangaTitle = HttpUtility.UrlDecode m
                choose [
                    route "" >=> warbler (fun _ ->
                        let manga = Manga.fromTitle mangaTitle
                        let chapter = manga.Chapters |> List.find (fun ch -> ch.Title = c)
                        mangaPage port manga chapter
                    )
                    routef "/%s" (fun p ->
                        let path = Path.Combine(mangaData, mangaTitle, c, p)
                        streamFile true path None None
                    )
                ]
            )
        ]
        PUT >=> choose [
            route "/manga/last-manga" >=> setLastManga
            routef "/manga/%s/bookmark" (HttpUtility.UrlDecode >> setBookmark)
        ]
        RequestErrors.NOT_FOUND "Page not found."
    ]

let defaultPort = 8080

let getMangaUrl (port: int option) (manga: MangaListing) =
    let p = Option.defaultValue defaultPort port
    let urlPath =
        match manga.Bookmark with
        | Some b -> Bookmark.toUrl manga.Title b
        | None -> MangaListing.firstPage manga
    $"http://localhost:%i{p}%s{urlPath}/"

let getIndexUrl (port: int option) =
    let p = Option.defaultValue defaultPort port
    $"http://localhost:%i{p}/"

let create (port: int option) =
    let p = Option.defaultValue defaultPort port
    let builder = WebApplication.CreateBuilder()
    builder.WebHost.UseUrls($"http://localhost:%i{p}") |> ignore
    builder.Host.UseSerilog(LoggerConfiguration().WriteTo.Console().CreateLogger()) |> ignore
    builder.Services.AddGiraffe() |> ignore
    let app = builder.Build()
    app.UseStaticFiles() |> ignore
    app.UseGiraffe(webApp p)
    app
