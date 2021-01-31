[<AutoOpen>]
module MangaSharp.Types

open System
open System.IO
open System.Text.RegularExpressions
open System.Net.Http
open FSharp.Data

type Direction =
    | Horizontal
    | Vertical
    override this.ToString() =
        match this with
        | Horizontal -> "horizontal"
        | Vertical -> "vertical"

type Provider = {
    Pattern: Regex
    TitleExtractor: string -> HtmlDocument -> string option
    ChapterUrlsExtractor: string -> HtmlDocument -> string seq option
    ChapterTitleExtractor: string -> HtmlDocument -> string option
    ImageExtractor: string -> HtmlDocument -> (unit -> HttpRequestMessage) seq option
}

type MangaSource = {
    Url: string
    Direction: Direction
    Provider: Provider
}

type Page = {
    Name: string
    File: string
}

type Chapter = {
    Title: string
    Pages: Page list
}

type DownloadStatus =
    | NotDownloaded
    | Downloaded
    | Ignored

type ChapterStatus = {
    Url: string
    Title: string option
    DownloadStatus: DownloadStatus
}

type Bookmark =
    | HorizontalBookmark of chapter: string * page: string
    | VerticalBookmark of chapter: string

type MangaInfo = {
    Title: string
    ChapterUrls: string seq
}

type ChapterInfo = {
    Title: string
    ImageRequests: (unit -> HttpRequestMessage) seq
}

type MangaListing = {
    Title: string
    Bookmark: Bookmark option
    Source: MangaSource
    ChapterIndex: int
    NumberOfChapters: int
}

type StoredManga = {
    Title: string
    Chapters: Chapter list
    Bookmark: Bookmark option
    Source: MangaSource
}

let dataHome =
    Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData,
        Environment.SpecialFolderOption.Create
    )
let mangaData = Path.Combine(dataHome, "manga")
Directory.CreateDirectory(mangaData) |> ignore
