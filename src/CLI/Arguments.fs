module MangaSharp.CLI.Arguments

open MangaSharp.Database.MangaDomain
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
    | [<MainCommand; Unique>] Title of string
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

type LsArgs =
    | Json
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Json -> "format as JSON."

type RmArgs =
    | [<MainCommand; Unique>] Title of string
    | All
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Title _ -> "the manga to remove."
            | All -> "remove all manga."

type Args =
    | [<CliPrefix(CliPrefix.None)>] Download of ParseResults<DownloadArgs>
    | [<CliPrefix(CliPrefix.None)>] Update of ParseResults<UpdateArgs>
    | [<CliPrefix(CliPrefix.None)>] Read of ParseResults<ReadArgs>
    | [<CliPrefix(CliPrefix.None)>] Ls of ParseResults<LsArgs>
    | [<CliPrefix(CliPrefix.None)>] Rm of ParseResults<RmArgs>
    | [<SubCommand>] Version
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Download _ -> "download a new manga."
            | Update _ -> "update an existing manga."
            | Read _ -> "open manga to read in an external application."
            | Ls _ -> "list the downloaded manga."
            | Rm _ -> "remove manga."
            | Version -> "display the version info."
