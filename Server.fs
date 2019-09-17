module MangaSharp.Server

open MangaSharp
open System
open System.IO
open System.Net
open System.Runtime.InteropServices
open System.Diagnostics
open Suave
open Suave.Filters
open Suave.Successful
open Suave.Operators
open Suave.FunctionalViewEngine

let chapterSelect (manga: StoredManga) (chapter: Chapter) =
    div [ attr "class" "control" ] [
        div [ attr "class" "select"] [
            select [ attr "id" "chapter-select" ] [
                for c in manga.Chapters ->
                    option [
                        yield attr "value" c.Title
                        if c = chapter then yield attr "selected" ""
                    ] [ encodedText (sprintf "Chapter %s" c.Title) ]
            ]
        ]
    ]

let pageSelect (chapter: Chapter) =
    div [ attr "class" "control" ] [
        div [ attr "class" "select"] [
            select [ attr "id" "page-select" ] [
                for p in chapter.Pages ->
                    option [ attr "value" p.Name ] [
                        encodedText (sprintf "Page %i" (int p.Name))
                    ]
            ]
        ]
    ]

let homeButton =
    div [ attr "class" "control" ] [
        a [ attr "class" "button"; attr "href" "/" ] [
            span [ attr "class" "icon" ] [
                tag "svg" [ attr "style" "width: 24px; height: 24px"; attr "viewBox" "0 0 24 24"] [
                    tag "path" [ attr "fill" "#000000"; attr "d" "M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z" ] []
                ]
            ]
        ]
    ]

let index (storedManga: StoredManga list) =
    html [] [
        head [] [
            meta [ attr "name" "viewport"; attr "content" "width=device-width, initial-scale=1"]
            meta [ attr "charset" "utf-8" ]
            title [] [ encodedText "MangaSharp - Index" ]
            link [ attr "rel" "stylesheet"; attr "href" "/bulma.min.css" ]
            link [ attr "rel" "stylesheet"; attr "href" "/index.css" ]
            link [ attr "rel" "shortcut icon"; attr  "href" "#" ]
        ]
        body [] [
            table [ attr "class" "table is-bordered is-striped" ] [
                yield thead [] [
                    tr [] [
                        th [] [ encodedText "Title" ]
                        th [] [ encodedText "Direction" ]
                        th [] [ encodedText "Source" ]
                        th [] [ encodedText "Progress" ]
                    ]
                ]
                for m in storedManga ->
                    let link =
                        match m.Bookmark with
                        | Some b -> Bookmark.toUrl m.Title b
                        | None -> Manga.firstPage m
                    let selectedChapter =
                        match m.Bookmark with
                        | Some b -> Bookmark.getChapter b
                        | None -> m.Chapters.[0]

                    tr [] [
                        yield td [] [ a [ attr "href" link ] [ encodedText m.Title ] ]
                        yield td [] [ encodedText (m.Source.Direction.ToString()) ]
                        yield td [] [ a [ attr "href" m.Source.Url ] [ encodedText m.Source.Url ] ]
                        let n = 1 + List.findIndex ((=) selectedChapter) m.Chapters
                        yield td [] [ encodedText (sprintf "%i/%i" n m.Chapters.Length) ]
                    ]
            ]
        ]
    ]
    |> renderHtmlDocument
    |> OK

let mangaPage (port: int) (manga: StoredManga) (chapter: Chapter) =
    let getHash (page: Page) =
        match manga.Source.Direction with
        | Horizontal -> sprintf "#%s" page.Name
        | Vertical -> ""
    let previousLink =
        Manga.tryPreviousChapter manga chapter
        |> Option.map (fun c -> sprintf "/manga/%s/%s%s" manga.Title c.Title (getHash (List.last c.Pages)))
    let nextLink =
        Manga.tryNextChapter manga chapter
        |> Option.map (fun c -> sprintf "/manga/%s/%s%s" manga.Title c.Title (getHash c.Pages.Head))

    html [] [
        head [] [
            yield meta [ attr "name" "viewport"; attr "content" "width=device-width, initial-scale=1"]
            yield meta [ attr "charset" "utf-8" ]
            yield title [] [ encodedText (sprintf "MangaSharp - %s - %s" manga.Title chapter.Title) ]
            yield link [ attr "rel" "stylesheet"; attr "href" "/bulma.min.css" ]
            yield link [ attr "rel" "shortcut icon"; attr  "href" "#" ]
            match manga.Source.Direction with
            | Horizontal ->
                yield link [ attr "rel" "stylesheet"; attr "href" "/horizontal.css" ]
            | Vertical ->
                yield link [ attr "rel" "stylesheet"; attr "href" "/vertical.css" ]
            yield script [ attr "src" "/manga.js" ] []
        ]
        body [
            if previousLink.IsSome then yield attr "data-previous-page" previousLink.Value
            if nextLink.IsSome then yield attr "data-next-page" nextLink.Value
            yield attr "data-manga" manga.Title
            yield attr "data-direction" (manga.Source.Direction.ToString())
            yield attr "data-chapter" chapter.Title
            yield attr "data-port" (string port)
        ] [
            yield div [ attr "id" "select-container"; attr "class" "field is-grouped" ] [
                yield homeButton
                yield chapterSelect manga chapter
                if manga.Source.Direction = Horizontal then yield pageSelect chapter
            ]
            yield div [] [
                for p in chapter.Pages ->
                    img [
                        attr "data-page" p.Name;
                        attr "src" (sprintf "/manga/%s/%s/%s" manga.Title chapter.Title p.File)
                    ]
            ]
        ]
    ]
    |> renderHtmlDocument
    |> OK

let urlDecode (ctx: HttpContext) =
    asyncOption {
        let decodedUrl =
            ctx.request.url.ToString()
            |> WebUtility.UrlDecode
            |> Uri
        return { ctx with request = { ctx.request with url = decodedUrl } }
    }

let app (port: int) =
    urlDecode >=> choose [
        GET >=> choose [
            path "/" >=> warbler (fun _ -> index (Manga.getStoredManga ()))
            Files.browseHome
            pathScan "/manga/%s" (Files.browseFile mangaData)
            pathScan "/manga/%s/%s" (fun (m, c) ->
                let manga = List.find (fun sm -> sm.Title = m) (Manga.getStoredManga ())
                let chapter: Chapter = List.find (fun ch -> ch.Title = c) manga.Chapters
                mangaPage port manga chapter
            )
        ]
        PUT >=> choose [
            path "/manga/last-manga" >=> request (fun r ->
                let body = Text.Encoding.UTF8.GetString(r.rawForm)
                let lastMangaPath = Path.Combine(mangaData, "last-manga")
                File.WriteAllText(lastMangaPath, sprintf "%s\n" body)
                Response.response HTTP_204 [||]
            )
            pathScan "/manga/%s/bookmark" (fun m ->
                request (fun r ->
                    let body = Text.Encoding.UTF8.GetString(r.rawForm)
                    let manga = List.find (fun sm -> sm.Title = m) (Manga.getStoredManga ())
                    let bookmarkPath = Path.Combine(mangaData, manga.Title, "bookmark")
                    File.WriteAllText(bookmarkPath, sprintf "%s\n" body)
                    Response.response HTTP_204 [||]
            ))
        ]
        RequestErrors.NOT_FOUND "Page not found."
    ]

let openInDefaultApp (url: string) =
    let cmd, args =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            "cmd", sprintf "/c start \"%s\"" url
        else if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
            "xdg-open", sprintf "\"%s\"" url
        else if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
            "open", sprintf "\"%s\"" url
        else
            failwith "Unrecognized platform."
    let startInfo =
        ProcessStartInfo(
            FileName=cmd,
            Arguments=args,
            UseShellExecute=false,
            RedirectStandardOutput=true,
            RedirectStandardError=true
        )
    Process.Start(startInfo) |> ignore

let read (port: int Option) (openInBrowser: bool) (manga: StoredManga option) =
    let binding =
        match port with
        | Some p -> HttpBinding.createSimple HTTP "127.0.0.1" p
        | None -> HttpBinding.defaults
    let config =
        { defaultConfig with
            homeFolder = Some (Path.GetFullPath("./dist"))
            bindings = [binding] }
    let chosenPort = int binding.socketBinding.port
    let _, server = startWebServerAsync config (app chosenPort)
    let task = Async.StartAsTask(server)
    if openInBrowser then
        let urlPath =
            match manga with
            | Some m ->
                match m.Bookmark with
                | Some b -> Bookmark.toUrl m.Title b
                | None -> Manga.firstPage m
            | None -> "/"
        let url = sprintf "http://localhost:%i%s" chosenPort urlPath
        openInDefaultApp url
    task.Wait()
