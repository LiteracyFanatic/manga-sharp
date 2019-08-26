module Manga

open System
open System.IO
open System.Net
open FSharp.Data

type TitleExtractor = HtmlDocument -> string
type ChapterUrlsExtractor = HtmlDocument -> string seq
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

let downloadChapter (dir: string) (imageExtractor: ImageExtractor) (n: int) (url: string) =
    let folder = Path.Combine(dir, string n)
    Directory.CreateDirectory(folder) |> ignore
    HtmlDocument.Load(url)
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
    Seq.iteri (fun i u ->
        downloadChapter dir manga.ImageExtractor (i + 1) u
        File.AppendAllText(Path.Combine(dir, "chapters"), sprintf "%s\n" u)
    ) chapterUrls
