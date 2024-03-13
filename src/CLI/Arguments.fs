module MangaSharp.CLI.Arguments

open MangaSharp.Database
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
    | From of string
    | To of string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Title _ -> "the manga to update."
            | From _ -> "the chapter to start updating from."
            | To _ -> "the chapter to update to."

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
            | No_Open -> "don't automatically open the default browser."

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

type ArchiveArgs =
    | [<MainCommand; Unique>] Title of string
    | All
    | From_Chapter of string
    | To_Chapter of string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Title _ -> "the manga to archive."
            | All -> "archive all manga."
            | From_Chapter _ -> "the chapter to start archiving from."
            | To_Chapter _ -> "the chapter to archive to."

type UnarchiveArgs =
    | [<MainCommand; Unique>] Title of string
    | All
    | From_Chapter of string
    | To_Chapter of string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Title _ -> "the manga to unarchive."
            | All -> "unarchive all manga."
            | From_Chapter _ -> "the chapter to start unarchiving from."
            | To_Chapter _ -> "the chapter to unarchive to."

type Args =
    | [<CliPrefix(CliPrefix.None)>] Download of ParseResults<DownloadArgs>
    | [<CliPrefix(CliPrefix.None)>] Update of ParseResults<UpdateArgs>
    | [<CliPrefix(CliPrefix.None)>] Read of ParseResults<ReadArgs>
    | [<CliPrefix(CliPrefix.None)>] Ls of ParseResults<LsArgs>
    | [<CliPrefix(CliPrefix.None)>] Rm of ParseResults<RmArgs>
    | [<CliPrefix(CliPrefix.None)>] Archive of ParseResults<ArchiveArgs>
    | [<CliPrefix(CliPrefix.None)>] Unarchive of ParseResults<UnarchiveArgs>
    | [<SubCommand>] Version

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Download _ -> "download a new manga."
            | Update _ -> "update an existing manga."
            | Read _ -> "open manga to read in an external application."
            | Ls _ -> "list the downloaded manga."
            | Rm _ -> "remove manga."
            | Archive _ -> "archive manga."
            | Unarchive _ -> "unarchive manga."
            | Version -> "display the version info."
