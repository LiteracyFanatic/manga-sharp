module MangaSharp.Provider

open System.Text.RegularExpressions
open FSharp.Data
open MangaSharp

let extractTitle (cssQuery: string) (regex: Regex) = fun (html: HtmlDocument) ->
    html.CssSelect(cssQuery)
    |> Seq.head
    |> HtmlNode.directInnerText
    |> regex.Match
    |> fun m -> m.Groups.[1].Value.Trim()

let extractChapterUrls (cssQuery: string) = fun (html: HtmlDocument) ->
    html.CssSelect(cssQuery)
    |> Seq.rev
    |> Seq.map (HtmlNode.attributeValue "href")
    |> Seq.distinct

let extractChapterTitle (cssQuery: string) (regex: Regex) = fun (html: HtmlDocument) ->
    html.CssSelect(cssQuery)
    |> Seq.head
    |> HtmlNode.directInnerText
    |> regex.Match
    |> fun m -> m.Groups.[1].Value

let extractImageUrls (cssQuery: string) = fun (html: HtmlDocument) ->
    html.CssSelect(cssQuery)
    |> Seq.map (HtmlNode.attributeValue "src")

let chapterNumberRegex = Regex("Chapter (\d+(\.\d+)?)")

let providers = [
    {
        Pattern = Regex("https://mangazuki\.me/manga/.*")
        TitleExtractor = extractTitle ".post-title h1" (Regex("(.*)"))
        ChapterUrlsExtractor = extractChapterUrls ".wp-manga-chapter a"
        ChapterTitleExtractor = extractChapterTitle ".breadcrumb li.active" chapterNumberRegex
        ImageExtractor = extractImageUrls ".wp-manga-chapter-img"
    }
    
    {
        Pattern = Regex("https://mangazuki\.me/.*")
        TitleExtractor = extractTitle "title" (Regex("Read (.*) Manga.*"))
        ChapterUrlsExtractor = extractChapterUrls ".wp-manga-chapter a"
        ChapterTitleExtractor = extractChapterTitle ".breadcrumb li.active" chapterNumberRegex
        ImageExtractor = extractImageUrls ".wp-manga-chapter-img"
    }

    {
        Pattern = Regex("https://manganelo\.com/manga/.*")
        TitleExtractor = extractTitle "title" (Regex("Read (.*) Manga Online For Free"))
        ChapterUrlsExtractor = extractChapterUrls ".chapter-list .row a"
        ChapterTitleExtractor = extractChapterTitle "title" chapterNumberRegex
        ImageExtractor = extractImageUrls "#vungdoc img"
    }
    
    {
        Pattern = Regex("https://manytoon\.com/comic/.*")
        TitleExtractor = extractTitle ".post-title h3" (Regex("(.*)"))
        ChapterUrlsExtractor = extractChapterUrls ".wp-manga-chapter a"
        ChapterTitleExtractor = extractChapterTitle ".single-chapter-select option[selected]" chapterNumberRegex
        ImageExtractor = extractImageUrls ".reading-content img"
    }
]

let tryFromTable (url: string) =
    List.tryFind (fun p -> p.Pattern.IsMatch(url)) providers
