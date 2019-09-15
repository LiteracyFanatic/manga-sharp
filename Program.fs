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
    | [<Mandatory; MainCommand; ExactlyOnce; Last>] Manga of string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Manga _ -> "the manga to read."

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
                Seq.iter (fun m -> Manga.download m.Source) Manga.storedManga
            else
                let manga = updateArgs.GetResult(UpdateArgs.Manga)
                let { Source = source } =
                    Seq.find (fun m -> m.Title = manga) Manga.storedManga
                Manga.download source
        | Read readArgs ->
            ()
        | Ls ->
            Seq.iter (fun m ->
                printfn "%s %i %s"
                    m.Title
                    m.NumberOfChapters
                    (Option.defaultValue "None" m.Bookmark)
            ) Manga.storedManga
        | Version ->
            ()
    with
    | :? ArguParseException as e ->
        printf "%s" e.Message

    0 // return an integer exit code
