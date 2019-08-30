open System.Text.RegularExpressions
open FSharp.Data
open Manga

// Sample implementation for https://manganelo.com/manga/
let extractTitle (html: HtmlDocument) =
    let r = Regex("Read (.*) Manga Online For Free")
    html.CssSelect("title")
    |> Seq.head
    |> HtmlNode.innerText
    |> r.Match
    |> fun m -> m.Groups.[1].Value

let extractChapterUrls (html: HtmlDocument) =
    html.CssSelect(".chapter-list .row a")
    |> Seq.rev
    |> Seq.map (HtmlNode.attributeValue "href")

let extractChapterTitle (html: HtmlDocument) =
    let r = Regex(".*Chapter (\d+(\.\d+)?).*")
    html.CssSelect("title")
    |> Seq.head
    |> HtmlNode.innerText
    |> r.Match
    |> fun m -> m.Groups.[1].Value

let extractImageUrls (html: HtmlDocument) =
    html.CssSelect("#vungdoc img")
    |> Seq.map (HtmlNode.attributeValue "src")

[<EntryPoint>]
let main argv =
    let indexUrl = argv.[0]
    let mangaSource = {
        Url = indexUrl
        TitleExtractor = extractTitle
        ChapterUrlsExtractor = extractChapterUrls
        ChapterTitleExtractor = extractChapterTitle
        ImageExtractor = extractImageUrls
        Direction = Vertical
    }
    downloadManga mangaSource
    0 // return an integer exit code
