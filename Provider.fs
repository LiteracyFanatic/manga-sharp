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
open Flurl
open Giraffe.ComputationExpressions

let private querySelector (html: HtmlDocument) (cssQuery: string) =
    opt {
        match html.CssSelect(cssQuery) with
        | [] -> printfn "%s did not match any elements." cssQuery
        | [node] -> return node
        | _ -> printfn "%s matched multiple elements." cssQuery
    }

let private querySelectorAll (html: HtmlDocument) (cssQuery: string) =
    opt {
        match html.CssSelect(cssQuery) with
        | [] -> printfn "%s didn't match any elements." cssQuery
        | elements -> return elements
    }

let private regexMatch (regex: Regex) (input: string) =
    opt {
        let m = regex.Match(input)
        if m.Success then
            let groups = seq m.Groups
            match Seq.tryItem 1 groups with
            | Some group ->
                match group.Value.Trim() with
                | "" -> printfn "%A returned an empty match." regex
                | value -> return value
            | None -> printfn "%A capture group 1 did not exist." regex
        else
            printfn "%A did not match." regex
    }

let private urlMatch (regex: Regex) = fun (url: string) (html: HtmlDocument) ->
    regexMatch regex url

let private cssAndRegex (cssQuery: string) (regex: Regex) = fun (url: string) (html: HtmlDocument) ->
    opt {
        let! node = querySelector html cssQuery
        let text = HtmlNode.directInnerText node
        return! regexMatch regex text
    }

let private resolveUrl (baseUrl: string) (url: string) =
    Uri(Uri(baseUrl), url).AbsoluteUri

let private extractChapterUrls (cssQuery: string) = fun (url: string) (html: HtmlDocument) ->
    opt {
        let! chapters = querySelectorAll html cssQuery
        let urls =
            chapters
            |> Seq.rev
            |> Seq.map (HtmlNode.attributeValue "href" >> resolveUrl url)
            |> Seq.distinct
        return urls
    }

let toHttpRequestMessage (url: string) =
    new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url)

let private extractImageUrls (cssQuery: string) = fun (url: string) (html: HtmlDocument) ->
    opt {
        let! nodes = querySelectorAll html cssQuery
        return Seq.map (HtmlNode.attributeValue "src" >> resolveUrl url >> toHttpRequestMessage) nodes
    }

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
            opt {
                let textInfo = CultureInfo("en-US", false).TextInfo
                let! node = querySelector html ".manga-info h1"
                let title =
                    node
                    |> HtmlNode.directInnerText
                    |> fun s -> s.ToLowerInvariant()
                    |> textInfo.ToTitleCase
                return title
            }
        ChapterUrlsExtractor = fun (url: string) (html: HtmlDocument) ->
            opt {
                let! chapters = querySelectorAll html ".chapter"
                let urls =
                    chapters
                    |> Seq.rev
                    |> Seq.map (HtmlNode.attributeValue "href" >> resolveUrl url)
                    |> Seq.distinct
                return urls
            }
        ChapterTitleExtractor = urlMatch (Regex("chapter-(\d+(-\d+)*)"))
        ImageExtractor = extractImageUrls ".chapter-img"
    }

    {
        Pattern = Regex("https://mangadex\.org/title/.*")
        TitleExtractor = cssAndRegex "title" (Regex("(.*) \(Title\) - MangaDex"))
        ChapterUrlsExtractor = fun (url: string) (html: HtmlDocument) ->
            opt {
                let! mangaId = regexMatch (Regex("https://mangadex\.org/title/(\d+)/.*")) url
                let apiUrl = sprintf "https://mangadex.org/api/?id=%s&type=manga" mangaId
                let! json = tryDownloadStringAsync apiUrl |> Async.RunSynchronously
                let doc = JsonDocument.Parse(json).RootElement
                let chapters = doc.GetProperty("chapter").EnumerateObject()
                let urls =
                    chapters
                    |> Seq.filter (fun c ->
                        let langCode = c.Value.GetProperty("lang_code").GetString()
                        let timeStamp = c.Value.GetProperty("timestamp").GetInt64()
                        langCode = "gb" && timeStamp <= DateTimeOffset.Now.ToUnixTimeSeconds()
                    )
                    |> Seq.map (fun c -> sprintf "https://mangadex.org/chapter/%s" c.Name)
                    |> Seq.rev
                return urls
            }
        ChapterTitleExtractor = fun (url: string) (html: HtmlDocument) ->
            opt {
                let! chapterId = regexMatch (Regex(".*/chapter/(\d+)")) url
                let apiUrl = sprintf "https://mangadex.org/api/?id=%s&type=chapter" chapterId
                let! json = tryDownloadStringAsync apiUrl |> Async.RunSynchronously
                let doc = JsonDocument.Parse(json).RootElement
                return doc.GetProperty("chapter").GetString()
            }
        ImageExtractor = fun (url: string) (html: HtmlDocument) ->
            opt {
                let! chapterId = regexMatch (Regex(".*/chapter/(\d+)")) url
                let apiUrl = sprintf "https://mangadex.org/api/?id=%s&type=chapter" chapterId
                let! json = tryDownloadStringAsync apiUrl |> Async.RunSynchronously
                let doc = JsonDocument.Parse(json).RootElement
                let server = doc.GetProperty("server").GetString()
                let hash = doc.GetProperty("hash").GetString()
                let pages =
                    doc.GetProperty("page_array").EnumerateArray()
                    |> Seq.map (fun el -> el.GetString())
                let reqs =
                    pages
                    |> Seq.map (fun p -> Path.Combine(server, hash, p) |> toHttpRequestMessage)
                return reqs
            }
    }

    {
        Pattern = Regex("https://www\.webtoons\.com/en/.*/.*/list\?.*")
        TitleExtractor = cssAndRegex "title" (Regex("(.*) \| WEBTOON"))
        ChapterUrlsExtractor = fun (url: string) (html: HtmlDocument) ->
            opt {
                let rec getLastPageGroup urlArg htmlArg =
                    match querySelector htmlArg "a.pg_next" with
                    | Some el ->
                        let nextPageGroupHref =
                            HtmlNode.attributeValue "href" el
                            |> resolveUrl urlArg
                        retryAsync 3 1000 (fun () -> HtmlDocument.tryLoadAsync nextPageGroupHref)
                        |> Async.RunSynchronously
                        |> Option.map (fun h -> getLastPageGroup nextPageGroupHref h)
                        |> Option.flatten
                    | None -> Some (urlArg, htmlArg)
                let! url, html = getLastPageGroup url html
                let! paginateLinks = querySelectorAll html ".paginate a"
                let lastPageHref =
                    paginateLinks
                    |> List.last
                    |> HtmlNode.attributeValue "href"
                    |> resolveUrl url
                let lastPageNumber =
                    match Url(lastPageHref).QueryParams.["page"] |> string with
                    | "" -> 1
                    | n -> int n
                let pageUrls =
                    [ 1 .. lastPageNumber ]
                    |> List.map (fun i -> url.SetQueryParam("page", i))
                let lift l =
                    if Seq.contains None l then None
                    else Some (Seq.map Option.get l)
                let! chapterUrlGroups =
                    pageUrls
                    |> List.toSeq
                    |> Seq.map (fun page ->
                        opt {
                            let! html =
                                retryAsync 3 1000 (fun () -> HtmlDocument.tryLoadAsync (page.ToString()))
                                |> Async.RunSynchronously
                            let! chapterLinks = querySelectorAll html "#_listUl a"
                            return Seq.map (HtmlNode.attributeValue "href") chapterLinks
                        }
                    )
                    |> lift
                let chapterUrls =
                    chapterUrlGroups
                    |> Seq.collect id
                    |> Seq.rev
                return chapterUrls
            }
        ChapterTitleExtractor = urlMatch (Regex("episode_no=(\d+)"))
        ImageExtractor = fun (url: string) (html: HtmlDocument) ->
            opt {
                let! imgs = querySelectorAll html "#_imageList img"
                let urls =
                    imgs
                    |> List.map (fun img ->
                        let reqUrl = HtmlNode.attributeValue "data-url" img
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
    match List.tryFind (fun p -> p.Pattern.IsMatch(url)) providers with
    | Some provider ->
        Some provider
    | None ->
        printfn "Could not find a provider that matched %s." url
        None
