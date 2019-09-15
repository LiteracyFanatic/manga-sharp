﻿open System.IO
open MangaSharp
open Argu

type DownloadArgs =
    | [<Mandatory; MainCommand; ExactlyOnce; Last>] Url of string
    | [<Mandatory>] Direction of Direction
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Url _ -> "the url of the index page for the manga to download."
            | Direction _ -> "the orientation of the manga."

type UpdateArgs =
    | Manga of string
    | All
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Manga _ -> "the manga to update."
            | All -> "update all manga."

type ReadArgs =
    | Title of title: string
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
            let manga = {
                Url = indexUrl
                Direction = direction
                Provider = Provider.tryFromTable indexUrl |> Option.get
            }
            Manga.download manga
        | Update updateArgs ->
            if updateArgs.Contains(All) then
                Seq.iter (fun m -> Manga.download m.Source) (Manga.getStoredManga ())
            else
                let manga = updateArgs.GetResult(Manga)
                let { Source = source } =
                    Seq.find (fun m -> m.Title = manga) (Manga.getStoredManga ())
                Manga.download source
        | Read readArgs ->
            ()
        | Ls ->
            Seq.iter (fun m ->
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
                printfn "%s %i %s"
                    m.Title
                    m.Chapters.Length
                    bookmarkText

            ) (Manga.getStoredManga ())
        | Version ->
            ()
    with
    | :? ArguParseException as e ->
        printf "%s" e.Message

    0 // return an integer exit code
