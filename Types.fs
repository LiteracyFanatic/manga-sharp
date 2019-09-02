[<AutoOpen>]
module MangaSharp.Types

open System.Text.RegularExpressions
open FSharp.Data

type TitleExtractor = HtmlDocument -> string
type ChapterUrlsExtractor = HtmlDocument -> string seq
type ChapterTitleExtractor = HtmlDocument -> string
type ImageExtractor = HtmlDocument -> string seq

type Direction =
    | Horizontal
    | Vertical
    override this.ToString() =
        match this with
        | Horizontal -> "horizontal"
        | Vertical -> "vertical"

type Provider = {
    Pattern: Regex
    TitleExtractor: TitleExtractor
    ChapterUrlsExtractor: ChapterUrlsExtractor
    ChapterTitleExtractor: ChapterTitleExtractor
    ImageExtractor: ImageExtractor
}

type MangaSource = {
    Url: string
    Direction: Direction
    Provider: Provider
}

type StoredManga = {
    Title: string
    NumberOfChapters: int
    Bookmark: string option
    Source: MangaSource
}
