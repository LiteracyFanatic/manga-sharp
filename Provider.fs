module MangaSharp.Provider

open System.Text.RegularExpressions
open FSharp.Data
open MangaSharp

let extractTitle (cssQuery: string) (regex: Regex) = fun (html: HtmlDocument) ->
    html.CssSelect(cssQuery)
    |> Seq.head
    |> HtmlNode.innerText
    |> regex.Match
    |> fun m -> m.Groups.[1].Value

let extractChapterUrls (cssQuery: string) = fun (html: HtmlDocument) ->
    html.CssSelect(cssQuery)
    |> Seq.rev
    |> Seq.map (HtmlNode.attributeValue "href")

let extractChapterTitle (cssQuery: string) (regex: Regex) = fun (html: HtmlDocument) ->
    html.CssSelect(cssQuery)
    |> Seq.head
    |> HtmlNode.innerText
    |> regex.Match
    |> fun m -> m.Groups.[1].Value

let extractImageUrls (cssQuery: string) = fun (html: HtmlDocument) ->
    html.CssSelect(cssQuery)
    |> Seq.map (HtmlNode.attributeValue "src")

let providers = [
    {
        Pattern = Regex("https://mangazuki\.me/manga/.*")
        TitleExtractor = extractTitle ".post-title h1" (Regex("(.*)"))
        ChapterUrlsExtractor = extractChapterUrls ".wp-manga-chapter a"
        ChapterTitleExtractor = extractChapterTitle ".breadcrumb li.active" (Regex(".*Chapter (\d+(\.\d+)?).*"))
        ImageExtractor = extractImageUrls ".wp-manga-chapter-img"
    }
    
    {
        Pattern = Regex("https://mangazuki\.me/.*")
        TitleExtractor = extractTitle "title" (Regex("Read (.*) Manga.*"))
        ChapterUrlsExtractor = extractChapterUrls ".wp-manga-chapter a"
        ChapterTitleExtractor = extractChapterTitle ".breadcrumb li.active" (Regex(".*Chapter (\d+(\.\d+)?).*"))
        ImageExtractor = extractImageUrls ".wp-manga-chapter-img"
    }

    {
        Pattern = Regex("https://manganelo\.com/manga/.*")
        TitleExtractor = extractTitle "title" (Regex("Read (.*) Manga Online For Free"))
        ChapterUrlsExtractor = extractChapterUrls ".chapter-list .row a"
        ChapterTitleExtractor = extractChapterTitle "title" (Regex(".*Chapter (\d+(\.\d+)?).*"))
        ImageExtractor = extractImageUrls "#vungdoc img"
    }
]

let tryFromTable (url: string) =
    List.tryFind (fun p -> p.Pattern.IsMatch(url)) providers
