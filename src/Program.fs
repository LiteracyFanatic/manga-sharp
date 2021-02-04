open Microsoft.Extensions.Hosting
open MangaSharp
open Argu

type DownloadArgs =
    | [<Mandatory; MainCommand>] Url of string
    | Direction of Direction
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Url _ -> "the url of the index page for the manga to download."
            | Direction _ -> "the orientation of the manga."

type UpdateArgs =
    | [<MainCommand>] Title of string
    | All
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Title _ -> "the manga to update."
            | All -> "update all manga."

type ReadArgs =
    | Title of string
    | Last
    | Port of int
    | No_Open
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Title _ -> "the manga to read."
            | Last -> "use the most recently read manga."
            | Port _ -> "the port to run the server on."
            | No_Open _ -> "don't automatically open the default browser."

type Args =
    | [<CliPrefix(CliPrefix.None)>] Download of ParseResults<DownloadArgs>
    | [<CliPrefix(CliPrefix.None)>] Update of ParseResults<UpdateArgs>
    | [<CliPrefix(CliPrefix.None)>] Read of ParseResults<ReadArgs>
    | [<CliPrefix(CliPrefix.None); SubCommand>] Ls
    | Version
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Download _ -> "download a new manga."
            | Update _ -> "update an existing manga."
            | Read _ -> "open manga to read in an external application."
            | Ls -> "list the downloaded manga."
            | Version -> "display the version info."

let ensureProviders (urls: string list) =
    let foundProviders, errors =
        List.foldBack (fun url (foundProviders, errors) ->
            match Provider.tryFromTable url with
            | Some m -> m :: foundProviders, errors
            | None -> foundProviders, url :: errors
        ) urls ([], [])
    match errors with
    | [] -> Ok foundProviders
    | _ -> Error errors

let makeDirectionUrlPairs (args: DownloadArgs list) =
    let rec loop direction acc args =
        match args with
        | [] -> acc
        | Direction d :: t -> loop d acc t
        | Url u :: t -> loop direction ((direction, u) :: acc) t

    loop Horizontal [] args
    |> List.rev

let download (args: ParseResults<DownloadArgs>) =
    if not (args.Contains(Url)) then
        printfn "Argument URL is required."
        exit 1

    let directions, urls =
        args.GetAllResults()
        |> makeDirectionUrlPairs
        |> List.unzip
    
    match ensureProviders urls with
    | Ok providers ->
        List.zip3 urls directions providers
        |> List.iter (fun (u, d, p) -> Manga.download { Url = u; Direction = d; Provider = p } |> ignore) 
    | Error u ->
        printfn "Could not find a provider for the following URLs:"
        List.iter (printfn "    %s") u
        exit 1

let ensureTitles (allManga: MangaListing list) (titles: string list) =
    let mangaDict = allManga |> List.map (fun m -> m.Title, m) |> Map.ofList
    let foundManga, errors =
        List.foldBack (fun title (foundManga, errors) ->
            match mangaDict.TryFind title with
            | Some m -> m :: foundManga, errors
            | None -> foundManga, title :: errors
        ) titles ([], [])
    match errors with
    | [] -> Ok foundManga
    | _ -> Error errors

let update (args: ParseResults<UpdateArgs>) =
    let allManga = MangaListing.getAll ()
    let manga =
        match args.Contains(All), args.GetResults(UpdateArgs.Title) with
        | true, [] -> allManga
        | true, _ ->
            failwith "Cannot specify --all and manga titles at the same time."
            exit 1
        | false, titles ->
            match ensureTitles allManga titles with
            | Ok m -> m
            | Error t ->
                printfn "The following titles could not be found:"
                List.iter (printfn "    %s") t
                exit 1
    let updated =
        manga
        |> List.map (fun m ->
            printfn $"Checking %s{m.Title} for updates..."
            Manga.download m.Source
        )
        |> List.contains true
    if not updated then
        printfn "No updates were found."

let read (args: ParseResults<ReadArgs>) =
    let port = args.TryGetResult(Port)
    let openInBrowser = not (args.Contains(No_Open))
    let manga =
        match args.Contains(Last), args.TryGetResult(Title) with
        | true, Some _ ->
            printfn "Cannot specify --last and a manga title at the same time."
            exit 1
        | true, None -> List.tryHead (MangaListing.getRecent())
        | false, Some title -> MangaListing.tryFromTitle title
        | false, None -> None
    let server = Server.create port
    if openInBrowser then
        let lifetime = server.Services.GetService(typeof<IHostApplicationLifetime>) :?> IHostApplicationLifetime
        lifetime.ApplicationStarted.Register(fun () ->
            match manga with
            | Some m -> Server.getMangaUrl port m
            | None -> Server.getIndexUrl port
            |> Util.openInDefaultApp |> ignore
        ) |> ignore
    server.Run()

let ls () =
    MangaListing.getAll ()
    |> List.iter (fun m ->
        let bookmarkText = 
            m.Bookmark
            |> Option.map (fun b ->
                let chapterText = Bookmark.getChapter b
                let pageText =
                    Bookmark.tryGetPage b
                    |> Option.map (sprintf " Page %s")
                    |> Option.defaultValue ""
                $"Chapter %s{chapterText}%s{pageText}" 
            )
            |> Option.defaultValue "None"
        printfn $"%s{m.Title},%A{m.Source.Direction},%i{m.NumberOfChapters},%s{bookmarkText}"
    )

[<EntryPoint>]
let main argv =
    let parser = 
        ArgumentParser.Create<Args>(
            programName="manga",
            helpTextMessage="Download, update, and read manga from a variety of sites."
        )
    try
        let results = parser.ParseCommandLine(argv)
        match results.GetSubCommand() with
        | Download downloadArgs -> download downloadArgs
        | Update updateArgs -> update updateArgs
        | Read readArgs -> read readArgs
        | Ls -> ls ()
        | Version -> ()
    with
    | :? ArguParseException as e -> printf $"%s{e.Message}"

    0 // return an integer exit code
