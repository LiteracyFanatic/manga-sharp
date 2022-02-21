module MangaSharp.Provider

open System
open System.IO
open System.Text.RegularExpressions
open System.Text.Json
open System.Globalization
open System.Net.Http
open FSharp.Data
open MangaSharp.Util
open Flurl
open FsToolkit.ErrorHandling

let private querySelector (html: HtmlDocument) (cssQuery: string) =
    match html.CssSelect(cssQuery) with
    | [] ->
        Error $"%s{cssQuery} did not match any elements."
    | [node] -> Ok node
    | _ ->
        Error $"%s{cssQuery} matched multiple elements."

let private querySelectorAll (html: HtmlDocument) (cssQuery: string) =
    match html.CssSelect(cssQuery) with
    | [] ->
        Error $"%s{cssQuery} didn't match any elements."
    | elements -> Ok elements

let private regexMatch (regex: Regex) (input: string) =
    let m = regex.Match(input)
    if m.Success then
        let groups = seq m.Groups
        match Seq.tryItem 1 groups with
        | Some group ->
            match group.Value.Trim() with
            | "" ->
                Error $"%A{regex} returned an empty match."
            | value -> Ok value
        | None ->
            Error $"%A{regex} capture group 1 did not exist."
    else
        Error $"%A{regex} did not match."

let private urlMatch (regex: Regex) = fun (url: string) (html: HtmlDocument) ->
    regexMatch regex url

let private cssAndRegex (cssQuery: string) (regex: Regex) = fun (url: string) (html: HtmlDocument) ->
    result {
        let! node = querySelector html cssQuery
        let text = HtmlNode.directInnerText node
        let! matchingText = regexMatch regex text
        return matchingText.Trim()
    }

let private resolveUrl (baseUrl: string) (url: string) =
    Uri(Uri(baseUrl), url).AbsoluteUri

let private extractChapterUrls (cssQuery: string) = fun (url: string) (html: HtmlDocument) ->
    result {
        let! chapters = querySelectorAll html cssQuery
        let urls =
            chapters
            |> Seq.rev
            |> Seq.map (HtmlNode.attributeValue "href" >> resolveUrl url)
            |> Seq.distinct
        return urls
    }

let toHttpRequestMessageFunc (url: string) =
    fun () -> new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url)

let private extractImageUrls (cssQuery: string) = fun (url: string) (html: HtmlDocument) ->
    result {
        let! nodes = querySelectorAll html cssQuery
        return Seq.map (HtmlNode.attributeValue "src" >> resolveUrl url >> toHttpRequestMessageFunc) nodes
    }

let private providers = [
    {
        Pattern = Regex("https://manganelo\.com/manga/.*")
        TitleExtractor = cssAndRegex "title" (Regex("(.*) Manga Online Free - Manganelo"))
        ChapterUrlsExtractor = extractChapterUrls ".chapter-name"
        ChapterTitleExtractor = urlMatch (Regex("chapter_(.*)"))
        ImageExtractor = fun (url: string) (html: HtmlDocument) ->
            result {
                let! imgs = querySelectorAll html ".container-chapter-reader img"
                let urls =
                    imgs
                    |> List.map (fun img ->
                        let reqUrl = HtmlNode.attributeValue "src" img
                        fun () ->
                            let req = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, reqUrl)
                            req.Headers.Referrer <- Uri(url)
                            req
                    )
                    |> List.toSeq
                return urls
            }
    }

    {
        Pattern = Regex("https://(read)?manganato\.com/manga.*")
        TitleExtractor = cssAndRegex "title" (Regex("(.*) Manga Online Free - Manganato"))
        ChapterUrlsExtractor = extractChapterUrls ".chapter-name"
        ChapterTitleExtractor = urlMatch (Regex("chapter-(.*)"))
        ImageExtractor = fun (url: string) (html: HtmlDocument) ->
            result {
                let! imgs = querySelectorAll html ".container-chapter-reader img"
                let urls =
                    imgs
                    |> List.map (fun img ->
                        let reqUrl = HtmlNode.attributeValue "src" img
                        fun () ->
                            let req = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, reqUrl)
                            req.Headers.Referrer <- Uri(url)
                            req
                    )
                    |> List.toSeq
                return urls
            }
    }

    {
        Pattern = Regex("https://manytoon\.com/comic/.*")
        TitleExtractor = cssAndRegex ".post-title h1" (Regex("(.*)"))
        ChapterUrlsExtractor = fun (url: string) (html: HtmlDocument) ->
            taskResult {
                let chaptersUrl = Path.Join(url, "ajax/chapters")
                let! response = hc.Force().PostAsync(chaptersUrl, null)
                response.EnsureSuccessStatusCode() |> ignore
                let! htmlContent = response.Content.ReadAsStringAsync()
                let htmlContent = $"<html><head></head><body>%s{htmlContent}</body></html>"
                let! htmlDoc = HtmlDocument.tryParse htmlContent
                let! chapters = querySelectorAll htmlDoc ".wp-manga-chapter a"
                let urls =
                    chapters
                    |> Seq.rev
                    |> Seq.map (HtmlNode.attributeValue "href")
                    |> Seq.distinct
                return urls
            } |> Async.AwaitTask
            |> Async.RunSynchronously
        ChapterTitleExtractor = urlMatch (Regex("chapter-(\d+(-\d+)*)"))
        ImageExtractor = fun (url: string) (html: HtmlDocument) ->
            result {
                let! nodes = querySelectorAll html ".wp-manga-chapter-img"
                // Remove duplicates and data URLs
                let urls =
                    nodes
                    |> Seq.collect (fun img -> [HtmlNode.attributeValue "src" img; HtmlNode.attributeValue "data-src" img])
                    |> Seq.filter (fun src -> not (src.StartsWith("data")))
                    |> Seq.distinct
                    |> Seq.map (resolveUrl url >> toHttpRequestMessageFunc)
                return urls
            }
    }

    {
        Pattern = Regex("https://manhwa18\.com/.*")
        TitleExtractor = fun (url: string) (html: HtmlDocument) ->
            result {
                let textInfo = CultureInfo("en-US", false).TextInfo
                let! node = querySelector html ".series-name a"
                let title =
                    node
                    |> HtmlNode.directInnerText
                    |> fun s -> s.ToLowerInvariant()
                    |> textInfo.ToTitleCase
                return title
            }
        ChapterUrlsExtractor = fun (url: string) (html: HtmlDocument) ->
            result {
                let! chapters = querySelectorAll html ".list-chapters a"
                let urls =
                    chapters
                    |> Seq.rev
                    |> Seq.map (HtmlNode.attributeValue "href")
                    |> Seq.distinct
                return urls
            }
        ChapterTitleExtractor = urlMatch (Regex("ch(?:ap)?-(\d+)"))
        ImageExtractor = fun (url: string) (html: HtmlDocument) ->
            result {
                let! nodes = querySelectorAll html "#chapter-content img"
                let urls =
                    nodes
                    |> Seq.map (HtmlNode.attributeValue "data-src" >> toHttpRequestMessageFunc)
                return urls
            }
    }

    {
        Pattern = Regex("https://manhwa18\.cc/.*")
        TitleExtractor = cssAndRegex ".post-title h1" (Regex("(.*)"))
        ChapterUrlsExtractor = extractChapterUrls ".chapter-name"
        ChapterTitleExtractor = urlMatch (Regex("chapter-(\d+(-\d+)*)"))
        ImageExtractor = extractImageUrls ".read-content img"
    }

    {
        Pattern = Regex("https://mangadex\.org/title/.*")
        TitleExtractor = fun (url: string) (html: HtmlDocument) ->
            taskResult {
                let! mangaId = regexMatch (Regex("https://mangadex\.org/title/([^/]*)(/.*)?")) url
                let apiUrl = $"https://api.mangadex.org/manga/%s{mangaId}"
                let! json = tryDownloadStringAsync apiUrl
                let doc = JsonDocument.Parse(json).RootElement.GetProperty("data")
                match doc.GetProperty("attributes").GetProperty("title").TryGetProperty("en") with
                | true, title ->
                    return title.GetString()
                | _ ->
                    let title =
                        doc.GetProperty("attributes").GetProperty("altTitles").EnumerateArray()
                        |> Seq.pick (fun altTitle ->
                            match altTitle.TryGetProperty("en") with
                            | true, title -> Some title
                            | _ -> None)
                    return title.GetString()
            } |> Async.AwaitTask
            |> Async.RunSynchronously
        ChapterUrlsExtractor = fun (url: string) (html: HtmlDocument) ->
            taskResult {
                let! mangaId = regexMatch (Regex("https://mangadex\.org/title/([^/]*)(/.*)?")) url
                let rec loop offset acc =
                    taskResult {
                        let apiUrl = $"https://api.mangadex.org/chapter?manga=%s{mangaId}&limit=100&offset=%i{offset}&order%%5bchapter%%5d=asc"
                        let! json = tryDownloadStringAsync apiUrl
                        let doc = JsonDocument.Parse(json).RootElement
                        let chapters = doc.GetProperty("data").EnumerateArray()
                        if Seq.isEmpty chapters then
                            return acc
                        else
                            return! loop (offset + 100) (Seq.append acc chapters)
                    }
                let! chapters = loop 0 Seq.empty
                let urls =
                    chapters
                    |> Seq.choose (fun c ->
                        let chapterId = c.GetProperty("id").GetString()
                        let langCode = c.GetProperty("attributes").GetProperty("translatedLanguage").GetString()
                        let timeStamp = c.GetProperty("attributes").GetProperty("publishAt").GetDateTime()
                        if langCode = "en" && timeStamp <= DateTime.Now then
                            Some $"https://mangadex.org/chapter/%s{chapterId}"
                        else
                            None)
                return urls
            } |> Async.AwaitTask
            |> Async.RunSynchronously
        ChapterTitleExtractor = fun (url: string) (html: HtmlDocument) ->
            taskResult {
                let! chapterId = regexMatch (Regex(".*/chapter/(.*)")) url
                let apiUrl = $"https://api.mangadex.org/chapter/%s{chapterId}"
                let! json = tryDownloadStringAsync apiUrl
                let doc = JsonDocument.Parse(json).RootElement.GetProperty("data")
                return doc.GetProperty("attributes").GetProperty("chapter").GetString()
            } |> Async.AwaitTask
            |> Async.RunSynchronously
        ImageExtractor = fun (url: string) (html: HtmlDocument) ->
            taskResult {
                let! chapterId = regexMatch (Regex(".*/chapter/(.*)")) url
                let apiUrl = $"https://api.mangadex.org/at-home/server/%s{chapterId}"
                let! json = tryDownloadStringAsync apiUrl
                let doc = JsonDocument.Parse(json).RootElement
                let baseUrl = doc.GetProperty("baseUrl").GetString()
                let hash = doc.GetProperty("chapter").GetProperty("hash").GetString()
                let pages =
                    doc.GetProperty("chapter").GetProperty("data").EnumerateArray()
                    |> Seq.map (fun el -> el.GetString())
                let reqs =
                    pages
                    |> Seq.map (fun p -> Path.Combine(baseUrl, "data", hash, p) |> toHttpRequestMessageFunc)
                return reqs
            } |> Async.AwaitTask
            |> Async.RunSynchronously
    }

    {
        Pattern = Regex("https://www\.webtoons\.com/en/.*/.*/list\?.*")
        TitleExtractor = cssAndRegex "title" (Regex("(.*) \| WEBTOON"))
        ChapterUrlsExtractor = fun (url: string) (html: HtmlDocument) ->
            taskResult {
                let rec getLastPageGroup urlArg htmlArg =
                    taskResult {
                        match querySelector htmlArg "a.pg_next" with
                        | Ok el ->
                            let nextPageGroupHref =
                                HtmlNode.attributeValue "href" el
                                |> resolveUrl urlArg
                            let! html = retryAsync 3 1000 (fun () -> HtmlDocument.tryLoadAsync nextPageGroupHref)
                            return! getLastPageGroup nextPageGroupHref html
                        | Error _ -> return (urlArg, htmlArg)
                    }
                let! url, html = getLastPageGroup url html
                let! paginateLinks = querySelectorAll html ".paginate a"
                let lastPageHref =
                    paginateLinks
                    |> List.last
                    |> HtmlNode.attributeValue "href"
                    |> resolveUrl url
                let lastPageNumber =
                    match Url(lastPageHref).QueryParams.FirstOrDefault("page") |> string with
                    | "" -> 1
                    | n -> int n
                let pageUrls =
                    [ 1 .. lastPageNumber ]
                    |> List.map (fun i -> url.SetQueryParam("page", i))
                let! chapterUrlGroups =
                    pageUrls
                    |> List.traverseTaskResultM (fun page ->
                        taskResult {
                            let! html = retryAsync 3 1000 (fun () -> HtmlDocument.tryLoadAsync (page.ToString()))
                            let! chapterLinks = querySelectorAll html "#_listUl a"
                            return Seq.map (HtmlNode.attributeValue "href") chapterLinks
                        }
                    )
                let chapterUrls =
                    chapterUrlGroups
                    |> Seq.collect id
                    |> Seq.rev
                return chapterUrls
            } |> Async.AwaitTask
            |> Async.RunSynchronously
        ChapterTitleExtractor = urlMatch (Regex("episode_no=(\d+)"))
        ImageExtractor = fun (url: string) (html: HtmlDocument) ->
            result {
                let! imgs = querySelectorAll html "#_imageList img"
                let urls =
                    imgs
                    |> List.map (fun img ->
                        let reqUrl = HtmlNode.attributeValue "data-url" img
                        fun () ->
                            let req = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, reqUrl)
                            req.Headers.Referrer <- Uri(url)
                            req
                    )
                    |> List.toSeq
                return urls
            }
    }
]

let tryFromTable (url: string) =
    List.tryFind (fun p -> p.Pattern.IsMatch(url)) providers

let fromTable (url: string) =
    List.find (fun p -> p.Pattern.IsMatch(url)) providers
