namespace MangaSharp.Extractors

open System
open FSharp.Data
open System.Net.Http
open System.Text.RegularExpressions
open System.Linq
open Microsoft.Extensions.Logging
open MangaSharp.Database
open MangaSharp.Extractors.Util
open FsToolkit.ErrorHandling
open Flurl

type WebToonExtractor
    (
        httpFactory: IHttpClientFactory,
        db: MangaContext,
        mangaRepository: MangaRepository,
        pageSaver: PageSaver,
        logger: ILogger<WebToonExtractor>
    ) =

    let hc = httpFactory.CreateClient()
    do hc.DefaultRequestHeaders.Referrer <- Uri("https://www.webtoons.com/")

    let rec getLastPageGroup (urlArg: string) (htmlArg: HtmlDocument) : TaskResult<string * HtmlDocument, CommonError> =
        taskResult {
            match querySelector htmlArg "a.pg_next" with
            | Ok el ->
                let nextPageGroupHref = HtmlNode.attributeValue "href" el |> resolveUrl urlArg

                let! html =
                    HtmlDocument.tryLoadAsync hc nextPageGroupHref
                    |> TaskResult.mapError TryLoadAsyncError

                return! getLastPageGroup nextPageGroupHref html
            | Error _ -> return (urlArg, htmlArg)
        }

    let getChapterUrls (url: string) (html: HtmlDocument) : TaskResult<string seq, CommonError> =
        taskResult {
            let! url, html = getLastPageGroup url html
            let! paginateLinks = querySelectorAll html ".paginate a" |> Result.mapError QuerySelectorAllError

            let lastPageHref =
                paginateLinks |> List.last |> HtmlNode.attributeValue "href" |> resolveUrl url

            let lastPageNumber =
                match Url(lastPageHref).QueryParams.FirstOrDefault("page") |> string with
                | "" -> 1
                | n -> int n

            let pageUrls =
                [ 1..lastPageNumber ] |> List.map (fun i -> url.SetQueryParam("page", i))

            let! chapterUrlGroups =
                pageUrls
                |> List.traverseTaskResultM (fun page ->
                    taskResult {
                        let! html =
                            HtmlDocument.tryLoadAsync hc (page.ToString())
                            |> TaskResult.mapError TryLoadAsyncError

                        let! chapterLinks = querySelectorAll html "#_listUl a" |> Result.mapError QuerySelectorAllError
                        return Seq.map (HtmlNode.attributeValue "href") chapterLinks
                    })

            let chapterUrls = chapterUrlGroups |> Seq.collect id |> Seq.rev
            return chapterUrls
        }

    let getChaptersAsync
        (url: string)
        (html: HtmlDocument)
        (manga: Manga)
        : TaskResult<ResizeArray<Chapter>, CommonError> =
        taskResult {
            let! chapterUrls = getChapterUrls url html

            let chapters =
                chapterUrls
                |> Seq.map (fun url -> Chapter(Url = url, Title = null, DownloadStatus = DownloadStatus.NotDownloaded))
                |> Seq.toList

            let mergedChapters =
                chapters
                    .Select(fun chapter i ->
                        let chapter =
                            match manga.Chapters |> Seq.tryFind (fun c -> c.Url = chapter.Url) with
                            | Some c -> c
                            | None -> chapter

                        chapter.Index <- i
                        chapter)
                    .ToList()

            return mergedChapters
        }

    let downloadChapter
        (newChapter: Chapter)
        (chapterHtml: HtmlDocument)
        (chapterTitle: string)
        (mangaTitle: string)
        : TaskResult<unit, CommonError> =
        taskResult {
            let! nodes =
                querySelectorAll chapterHtml "#_imageList img"
                |> Result.mapError QuerySelectorAllError

            let imgs = nodes |> List.map (fun img -> HtmlNode.attributeValue "data-url" img)

            for i, img in Seq.indexed imgs do
                use! imageStream = hc.GetStreamAsync(img)
                let! newPage = pageSaver.SavePageAsync(mangaTitle, chapterTitle, i, imageStream)
                newChapter.Pages.Add(newPage)
        }

    let downloadChapters (manga: Manga) : TaskResult<unit, CommonError> =
        taskResult {
            let newChapters =
                manga.Chapters
                |> Seq.filter (fun c -> c.DownloadStatus = DownloadStatus.NotDownloaded)
                |> Seq.toList

            for i, newChapter in Seq.indexed newChapters do
                let! chapterHtml =
                    HtmlDocument.tryLoadAsync hc newChapter.Url
                    |> TaskResult.mapError TryLoadAsyncError

                let! chapterTitle =
                    regexMatch (Regex("episode_no=(\d+)")) newChapter.Url
                    |> Result.mapError RegexMatchError

                logger.LogInformation(
                    "Downloading {Title} Chapter {ChapterTitle} ({ChapterNumber}/{NumberOfChapters})",
                    manga.Title,
                    chapterTitle,
                    (i + 1),
                    newChapters.Length
                )

                do! downloadChapter newChapter chapterHtml chapterTitle manga.Title
                newChapter.Title <- chapterTitle
                newChapter.DownloadStatus <- DownloadStatus.Downloaded
                let! _ = db.SaveChangesAsync()
                ()
        }

    interface IMangaExtractor with

        member this.IsMatch(url: string) =
            Regex("https://www\.webtoons\.com/en/.*/.*/list\?.*").IsMatch(url)

        member this.DownloadAsync(url: string, direction: Direction) =
            taskResult {
                let! html = HtmlDocument.tryLoadAsync hc url |> TaskResult.mapError TryLoadAsyncError
                let! titleNode = querySelector html "title" |> Result.mapError QuerySelectorError

                let! title =
                    regexMatch (Regex("(.*) \| WEBTOON")) (titleNode.DirectInnerText())
                    |> Result.mapError RegexMatchError

                let! manga = mangaRepository.GetOrCreateAsync(title, direction, url)
                let! chapters = getChaptersAsync url html manga
                manga.Chapters <- chapters
                let! _ = db.SaveChangesAsync()

                if manga.Chapters.Exists(fun chapter -> chapter.DownloadStatus = DownloadStatus.NotDownloaded) then
                    do! downloadChapters manga
                    logger.LogInformation("Finished downloading {Title}", title)
            }
            |> TaskResult.catch Other
            |> TaskResult.mapError (fun e -> e :> IMangaSharpError)

        member this.UpdateAsync(mangaId: Guid) =
            taskResult {
                let! manga = mangaRepository.GetByIdAsync(mangaId)
                let! html = HtmlDocument.tryLoadAsync hc manga.Url |> TaskResult.mapError TryLoadAsyncError
                let! chapters = getChaptersAsync manga.Url html manga
                manga.Chapters <- chapters
                let! _ = db.SaveChangesAsync()

                if manga.Chapters.Exists(fun chapter -> chapter.DownloadStatus = DownloadStatus.NotDownloaded) then
                    do! downloadChapters manga
                    logger.LogInformation("Finished downloading {Title}", manga.Title)
                    return true
                else
                    return false
            }
            |> TaskResult.catch Other
            |> TaskResult.mapError (fun e -> e :> IMangaSharpError)
