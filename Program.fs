open MangaSharp
open Argu
open MangaSharp.Util

type DownloadArgs =
    | [<Mandatory; MainCommand; ExactlyOnce; Last>] Url of string
    | [<Mandatory>] Direction of Direction
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Url _ -> "the url of the index page for the manga to download."
            | Direction _ -> "the orientation of the manga."

type UpdateArgs =
    | Title of string
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
        | Download downloadArgs ->
            let indexUrl = downloadArgs.GetResult(Url)
            let direction = downloadArgs.GetResult(Direction)
            match Provider.tryFromTable indexUrl with
            | Some provider ->
                let manga = {
                    Url = indexUrl
                    Direction = direction
                    Provider = provider
                }
                Manga.download manga |> ignore
            | None ->
                ()
        | Update updateArgs ->
            printfn "Checking for updates..."
            let updated =
                if updateArgs.Contains(All) then
                    Manga.getStoredManga ()
                    |> List.map (fun m -> Manga.download m.Source)
                    |> List.contains true
                else
                    let title = updateArgs.GetResult(UpdateArgs.Title)
                    let { Source = source } =
                        List.find (fun m -> m.Title = title) (Manga.getStoredManga ())
                    Manga.download source
            if not updated then
                printfn "No updates were found."
        | Read readArgs ->
            let port = readArgs.TryGetResult(Port)
            let openInBrowser = not (readArgs.Contains(No_Open))
            if readArgs.Contains(Last) then
                if readArgs.Contains(Title) then
                    printfn "Cannot specify --last and a manga title at the same time."
                else
                    match Manga.getRecent () with
                    | h :: t -> Server.read port openInBrowser (Some h)
                    | [] -> ()
            else
                match readArgs.TryGetResult(Title) with
                | Some t ->
                    match Manga.tryFromTitle t with
                    | Some m -> Server.read port openInBrowser (Some m)
                    | None -> ()
                | None -> Server.read port openInBrowser None
        | Ls ->
            Manga.getStoredManga ()
            |> List.iter (fun m ->
                let bookmarkText = 
                    m.Bookmark
                    |> Option.map (fun b ->
                        let chapterText = (Bookmark.getChapter b).Title
                        let pageText =
                            Bookmark.tryGetPage b
                            |> Option.map (fun p -> sprintf " Page %s" p.Name)
                            |> Option.defaultValue ""
                        sprintf "Chapter %s%s" chapterText pageText
                    )
                    |> Option.defaultValue "None"
                printfn "%s,%A,%i,%s"
                    m.Title
                    m.Source.Direction
                    (NonEmptyList.length m.Chapters)
                    bookmarkText
            )
        | Version ->
            ()
    with
    | :? ArguParseException as e ->
        printf "%s" e.Message

    0 // return an integer exit code
