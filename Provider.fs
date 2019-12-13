module MangaSharp.Provider

open System
open System.IO
open System.Text.RegularExpressions
open System.Text.Json
open System.Globalization
open System.Net.Http
open FSharp.Data
open MangaSharp
open MangaSharp.Util

let private querySelector (html: HtmlDocument) (cssQuery: string) =
    match html.CssSelect(cssQuery) with
    | [] ->
        printfn "%s did not match any elements." cssQuery
        None
    | [node] ->
        Some node
    | _ ->
        printfn "%s matched multiple elements." cssQuery
        None

let private querySelectorAll (html: HtmlDocument) (cssQuery: string) =
    match html.CssSelect(cssQuery) with
    | [] ->
        printfn "%s didn't match any elements." cssQuery
        None
    | elements ->
        Some elements

let private regexMatch (regex: Regex) (input: string) =
    let m = regex.Match(input)
    if m.Success then
        let groups = seq m.Groups
        match Seq.tryItem 1 groups with
        | Some group ->
            match group.Value.Trim() with
            | "" ->
                printfn "%A returned an empty match." regex
                None
            | value ->
                Some value
        | None ->
            printfn "%A capture group 1 did not exist." regex
            None
    else
        printfn "%A did not match." regex
        None

let private urlMatch (regex: Regex) = fun (url: string) (html: HtmlDocument) ->
    regexMatch regex url

let private cssAndRegex (cssQuery: string) (regex: Regex) = fun (url: string) (html: HtmlDocument) ->
    querySelector html cssQuery
    |> Option.map (fun node ->
        let text = HtmlNode.directInnerText node
        regexMatch regex text
    )
    |> Option.flatten

let private resolveUrl (baseUrl: string) (url: string) =
    Uri(Uri(baseUrl), url).AbsoluteUri

let private extractChapterUrls (cssQuery: string) = fun (url: string) (html: HtmlDocument) ->
    querySelectorAll html cssQuery
    |> Option.map (fun chapters ->
        chapters
        |> Seq.rev
        |> Seq.map (HtmlNode.attributeValue "href" >> resolveUrl url)
        |> Seq.distinct
    )

let toHttpRequestMessage (url: string) =
    new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url)

let private extractImageUrls (cssQuery: string) = fun (url: string) (html: HtmlDocument) ->
    querySelectorAll html cssQuery
    |> Option.map (Seq.map (HtmlNode.attributeValue "src" >> resolveUrl url >> toHttpRequestMessage))

let private providers = [
    {
        Pattern = Regex("https://manganelo\.com/manga/.*")
        TitleExtractor = cssAndRegex "title" (Regex("Read (.*) Manga Online For Free"))
        ChapterUrlsExtractor = extractChapterUrls ".chapter-list .row a"
        ChapterTitleExtractor = urlMatch (Regex("chapter_(.*)"))
        ImageExtractor = extractImageUrls "#vungdoc img"
    }

    {
        Pattern = Regex("https://manytoon\.com/comic/.*")
        TitleExtractor = cssAndRegex ".post-title h3" (Regex("(.*)"))
        ChapterUrlsExtractor = extractChapterUrls ".wp-manga-chapter a"
        ChapterTitleExtractor = urlMatch (Regex("chapter-(\d+(-\d+)*)"))
        ImageExtractor = extractImageUrls ".wp-manga-chapter-img"
    }

    {
        Pattern = Regex("https://manhwa18\.com/.*")
        TitleExtractor = fun (url: string) (html: HtmlDocument) ->
            let textInfo = CultureInfo("en-US", false).TextInfo
            querySelector html ".manga-info h1"
            |> Option.map (fun node ->
                node
                |> HtmlNode.directInnerText
                |> fun s -> s.ToLowerInvariant()
                |> textInfo.ToTitleCase
            )
        ChapterUrlsExtractor = fun (url: string) (html: HtmlDocument) ->
            querySelectorAll html ".chapter"
            |> Option.map (fun chapters ->
                chapters
                |> Seq.rev
                |> Seq.map (HtmlNode.attributeValue "href" >> resolveUrl url)
                |> Seq.distinct
            )
        ChapterTitleExtractor = urlMatch (Regex("chapter-(\d+(-\d+)*)"))
        ImageExtractor = extractImageUrls ".chapter-img"
    }

    {
        Pattern = Regex("https://mangadex\.org/title/.*")
        TitleExtractor = cssAndRegex "title" (Regex("(.*) \(Title\) - MangaDex"))
        ChapterUrlsExtractor = fun (url: string) (html: HtmlDocument) ->
            let mangaId = regexMatch (Regex("https://mangadex\.org/title/(\d+)/.*")) url
            mangaId
            |> Option.map (fun mi ->
                let apiUrl = sprintf "https://mangadex.org/api/?id=%s&type=manga" mi
                let json = tryDownloadStringAsync apiUrl |> Async.RunSynchronously
                json
                |> Option.map (fun j ->
                    let doc = JsonDocument.Parse(j).RootElement
                    let chapters = doc.GetProperty("chapter").EnumerateObject()
                    chapters
                    |> Seq.filter (fun c -> c.Value.GetProperty("lang_code").GetString() = "gb")
                    |> Seq.map (fun c -> sprintf "https://mangadex.org/chapter/%s" c.Name)
                    |> Seq.rev
                )
            )
            |> Option.flatten
        ChapterTitleExtractor = cssAndRegex "title" (Regex(".*Ch. (\d+(.\d+)*).*"))
        ImageExtractor = fun (url: string) (html: HtmlDocument) ->
            let chapterId = regexMatch (Regex(".*/chapter/(\d+)")) url
            chapterId
            |> Option.map(fun ci ->
                let apiUrl = sprintf "https://mangadex.org/api/?id=%s&type=chapter" ci
                let json = tryDownloadStringAsync apiUrl |> Async.RunSynchronously
                json
                |> Option.map (fun j ->
                    let doc = JsonDocument.Parse(j).RootElement
                    let server = doc.GetProperty("server").GetString()
                    let hash = doc.GetProperty("hash").GetString()
                    let pages =
                        doc.GetProperty("page_array").EnumerateArray()
                        |> Seq.map (fun el -> el.GetString())
                    pages
                    |> Seq.map (fun p -> Path.Combine(server, hash, p) |> toHttpRequestMessage)
                )
            )
            |> Option.flatten
    }
]

let tryFromTable (url: string) =
    match List.tryFind (fun p -> p.Pattern.IsMatch(url)) providers with
    | Some provider ->
        Some provider
    | None ->
        printfn "Could not find a provider that matched %s." url
        None
