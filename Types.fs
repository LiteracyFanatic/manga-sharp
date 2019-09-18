[<AutoOpen>]
module MangaSharp.Types

open System
open System.IO
open System.Text.RegularExpressions
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
    TitleExtractor: HtmlDocument -> string
    ChapterUrlsExtractor: HtmlDocument -> string seq
    ChapterTitleExtractor: HtmlDocument -> string
    ImageExtractor: HtmlDocument -> string seq
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

type Bookmark =
    | HorizontalBookmark of Chapter * Page
    | VerticalBookmark of Chapter

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
