module Manga

open System
open System.IO
open System.Net
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

type MangaSource = {
    Url: string
    TitleExtractor: TitleExtractor
    ChapterUrlsExtractor: ChapterUrlsExtractor
    ChapterTitleExtractor: ChapterTitleExtractor
    ImageExtractor: ImageExtractor
    Direction: Direction
}

let dataHome =
    Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData,
        Environment.SpecialFolderOption.Create
    )
let mangaData = Path.Combine(dataHome, "manga")
printfn "%s" mangaData
Directory.CreateDirectory(mangaData) |> ignore

let downloadImage (dir: string) (url: string) =
    let name = Path.GetFileName(Uri(url).LocalPath)
    printfn "Image = %s" name
    use wc = new WebClient()
    wc.DownloadFile(url, Path.Combine(dir, name))

let downloadChapter (dir: string) (title: string) (chapterTitleExtractor: ChapterTitleExtractor) (imageExtractor: ImageExtractor) (url: string) =
    let html = HtmlDocument.Load(url)
    let chapterTitle = chapterTitleExtractor html
    let folder = Path.Combine(dir, chapterTitle)
    Directory.CreateDirectory(folder) |> ignore
    html
    |> imageExtractor
    |> Seq.iter (downloadImage folder)

let downloadManga (manga: MangaSource): unit =
    let index = HtmlDocument.Load(manga.Url)
    let title = manga.TitleExtractor index
    printfn "%s" title
    let chapterUrls = manga.ChapterUrlsExtractor index
    printfn "%A" chapterUrls
    let dir = Path.Combine(mangaData, title)
    Directory.CreateDirectory(dir) |> ignore
    File.WriteAllText(Path.Combine(dir, "direction"), manga.Direction.ToString())
    File.WriteAllText(Path.Combine(dir, "source"), manga.Url)
    let existingChapters =
        if File.Exists(Path.Combine(dir, "chapters")) then
            File.ReadAllLines (Path.Combine(dir, "chapters"))
            |> Set.ofArray
        else
            Set.empty
    if not (Set.isSubset existingChapters (Set.ofSeq chapterUrls)) then
        printfn "WARNING: There are previously downloaded chapters from URLs not listed by the current source. This may indicate a change in page structure."
    let newChapters = Set.difference (Set.ofSeq chapterUrls) existingChapters
    if Set.isEmpty newChapters then
        printfn "No new chapters."
    else
        chapterUrls
        |> Seq.filter newChapters.Contains
        |> Seq.iteri (fun i u ->
            downloadChapter dir title manga.ChapterTitleExtractor manga.ImageExtractor u
            File.AppendAllText(Path.Combine(dir, "chapters"), sprintf "%s\n" u)
        )
