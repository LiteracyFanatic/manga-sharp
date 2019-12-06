module MangaSharp.Server

open MangaSharp
open MangaSharp.Util
open System.IO
open System.Runtime.InteropServices
open System.Diagnostics
open System.Web
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.FileProviders
open Microsoft.Extensions.FileProviders.Internal
open Microsoft.Extensions.FileProviders.Physical
open Microsoft.Extensions.Primitives
open Giraffe
open Giraffe.GiraffeViewEngine
open FSharp.Control.Tasks.V2.ContextInsensitive
open Serilog

let private chapterSelect (manga: StoredManga) (chapter: Chapter) =
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

let private pageSelect (chapter: Chapter) =
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

let private homeButton =
    div [ attr "class" "control" ] [
        a [ attr "class" "button"; attr "href" "/" ] [
            span [ attr "class" "icon" ] [
                tag "svg" [ attr "style" "width: 24px; height: 24px"; attr "viewBox" "0 0 24 24"] [
                    tag "path" [ attr "fill" "#000000"; attr "d" "M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z" ] []
                ]
            ]
        ]
    ]

let private index (storedManga: StoredManga list) =
    html [] [
        head [] [
            meta [ attr "name" "viewport"; attr "content" "width=device-width, initial-scale=1"]
            meta [ attr "charset" "utf-8" ]
            title [] [ encodedText "MangaSharp - Index" ]
            link [ attr "rel" "stylesheet"; attr "href" "/assets/bulma.min.css" ]
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
                        | None -> NonEmptyList.head m.Chapters

                    tr [] [
                        yield td [] [ a [ attr "href" link ] [ encodedText m.Title ] ]
                        yield td [] [ encodedText (m.Source.Direction.ToString()) ]
                        yield td [] [ a [ attr "href" m.Source.Url ] [ encodedText m.Source.Url ] ]
                        let n = 1 + NonEmptyList.findIndex ((=) selectedChapter) m.Chapters
                        yield td [] [ encodedText (sprintf "%i/%i" n (NonEmptyList.length m.Chapters)) ]
                    ]
            ]
        ]
    ]
    |> htmlView

let private mangaPage (port: int) (manga: StoredManga) (chapter: Chapter) =
    let getHash (page: Page) =
        match manga.Source.Direction with
        | Horizontal -> sprintf "#%s" page.Name
        | Vertical -> ""
    let previousLink =
        Manga.tryPreviousChapter manga chapter
        |> Option.map (fun c -> sprintf "/manga/%s/%s%s" (HttpUtility.UrlEncode manga.Title) c.Title (getHash (NonEmptyList.last c.Pages)))
    let nextLink =
        Manga.tryNextChapter manga chapter
        |> Option.map (fun c -> sprintf "/manga/%s/%s%s" (HttpUtility.UrlEncode manga.Title) c.Title (getHash (NonEmptyList.head c.Pages)))

    html [] [
        head [] [
            yield meta [ attr "name" "viewport"; attr "content" "width=device-width, initial-scale=1"]
            yield meta [ attr "charset" "utf-8" ]
            yield title [] [ encodedText (sprintf "MangaSharp - %s - %s" manga.Title chapter.Title) ]
            yield link [ attr "rel" "stylesheet"; attr "href" "/assets/bulma.min.css" ]
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
            yield attr "data-manga" (HttpUtility.UrlEncode manga.Title)
            yield attr "data-direction" (manga.Source.Direction.ToString())
            yield attr "data-chapter" chapter.Title
            yield attr "data-port" (string port)
        ] [
            yield div [ attr "id" "select-container"; attr "class" "field is-grouped" ] [
                yield homeButton
                yield chapterSelect manga chapter
                if manga.Source.Direction = Horizontal then yield pageSelect chapter
            ]
            yield div [ attr "id" "image-container" ] [
                for p in chapter.Pages ->
                    img [
                        attr "data-page" p.Name;
                        attr "src" (sprintf "/manga/%s/%s/%s" (HttpUtility.UrlEncode manga.Title) chapter.Title p.File)
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
            let lastMangaPath = Path.Combine(mangaData, "last-manga")
            do! File.WriteAllTextAsync(lastMangaPath, sprintf "%s\n" body)
            return Some ctx
        })
    >=> Successful.NO_CONTENT

let private setBookmark (mangaTitle: string) =
    handleContext (fun ctx ->
        task {
            use reader = new StreamReader(ctx.Request.Body)
            let! body = reader.ReadToEndAsync()
            let manga = List.find (fun sm -> sm.Title = mangaTitle) (Manga.getStoredManga ())
            let bookmarkPath = Path.Combine(mangaData, manga.Title, "bookmark")
            do! File.WriteAllTextAsync(bookmarkPath, sprintf "%s\n" body)
            return Some ctx
        })
    >=> Successful.NO_CONTENT

let private webApp (port: int) =
    choose [
        GET >=> choose [
            route "/" >=> warbler (fun _ -> index (Manga.getStoredManga ()))
            subRoutef "/manga/%s/%s" (fun (m, c) ->
                let mangaTitle = HttpUtility.UrlDecode m
                let manga = List.find (fun sm -> sm.Title = mangaTitle) (Manga.getStoredManga ())
                let chapter: Chapter = NonEmptyList.find (fun ch -> ch.Title = c) manga.Chapters
                mangaPage port manga chapter
            )
        ]
        PUT >=> choose [
            route "/manga/last-manga" >=> setLastManga
            routef "/manga/%s/bookmark" (HttpUtility.UrlDecode >> setBookmark)
        ]
        RequestErrors.NOT_FOUND "Page not found."
    ]

let private openInDefaultApp (url: string) =
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

type UrlDecodingFileProvider(root: string)  =
    interface IFileProvider with
        member __.GetDirectoryContents(subpath: string) : IDirectoryContents =
            failwith "not implemented"

        member __.GetFileInfo(subpath: string) : IFileInfo =
            let fi = FileInfo(Path.Combine(root, (HttpUtility.UrlDecode subpath).Remove(0, 1)))
            PhysicalFileInfo(fi) :> IFileInfo

        member __.Watch(filter: string) : IChangeToken =
            NullChangeToken.Singleton :> IChangeToken

let private configureApp (port: int) (env: WebHostBuilderContext) (app : IApplicationBuilder) =
    app
        .UseStaticFiles()
        .UseStaticFiles(
            StaticFileOptions(
                FileProvider=UrlDecodingFileProvider(mangaData),
                RequestPath=PathString("/manga")
            )
        )
        .UseGiraffe(webApp port)

let private configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore

let read (port: int Option) (openInBrowser: bool) (manga: StoredManga option) =
    let p = Option.defaultValue 8080 port
    let server =
        Host
            .CreateDefaultBuilder()
            .UseSerilog(LoggerConfiguration().WriteTo.Console().CreateLogger())
            .ConfigureWebHostDefaults(fun (webHost: IWebHostBuilder) ->
                webHost
                    .UseUrls(sprintf "http://localhost:%i" p)
                    .ConfigureServices(configureServices)
                    .Configure(configureApp p)
                    |> ignore
            )
            .Build()
            .RunAsync()
    if openInBrowser then
        let urlPath =
            match manga with
            | Some m ->
                match m.Bookmark with
                | Some b -> Bookmark.toUrl m.Title b
                | None -> Manga.firstPage m
            | None -> "/"
        let url = sprintf "http://localhost:%i%s" p urlPath
        openInDefaultApp url
    server.Wait()
