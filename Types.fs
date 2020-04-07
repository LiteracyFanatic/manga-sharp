[<AutoOpen>]
module MangaSharp.Types

open System
open System.IO
open System.Text.RegularExpressions
open System.Net.Http
open FSharp.Data
open MangaSharp.Util

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
    Pages: NonEmptyList<Page>
}

type Bookmark =
    | HorizontalBookmark of Chapter * Page
    | VerticalBookmark of Chapter

type StoredManga = {
    Title: string
    Chapters: NonEmptyList<Chapter>
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
