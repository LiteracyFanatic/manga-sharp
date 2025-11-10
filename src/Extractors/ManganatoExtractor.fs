namespace MangaSharp.Extractors

open System
open FSharp.Control
open FSharp.Data
open System.Net.Http
open System.Text.RegularExpressions
open System.Linq
open Microsoft.Extensions.Logging
open MangaSharp.Database
open MangaSharp.Extractors.Util
open FsToolkit.ErrorHandling

type ManganatoExtractor
    (
        httpFactory: IHttpClientFactory,
        db: MangaContext,
        mangaRepository: MangaRepository,
        pageSaver: PageSaver,
        logger: ILogger<ManganatoExtractor>
    ) =

    let hc = httpFactory.CreateClient()
    do hc.DefaultRequestHeaders.Referrer <- Uri("https://www.natomanga.com/")

    let getChaptersAsync
        (url: string)
        (html: HtmlDocument)
        (manga: Manga)
        : TaskResult<ResizeArray<Chapter>, CommonError> =
        taskResult {
            let! chapterUrls =
                extractChapterUrls ".chapter-list a" url html
                |> Result.mapError QuerySelectorAllError

            let chapters =
                chapterUrls
                |> Seq.map (fun url -> Chapter(Url = url, Title = null, DownloadStatus = DownloadStatus.NotDownloaded))
                |> Seq.toList

            let mergedChapters =
                chapters
                    .Select(fun chapter i ->
                        let chapter =
                            manga.Chapters
                            |> Seq.tryFind (fun c -> c.Url = chapter.Url)
                            |> Option.defaultValue chapter

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
            let! imgs =
                querySelectorAll chapterHtml ".container-chapter-reader img"
                |> Result.eitherMap (List.map (HtmlNode.attributeValue "src")) QuerySelectorAllError

            let imageStreams = taskSeq {
                for img in imgs do
                    try
                        let! stream = hc.GetStreamAsync(img)
                        yield stream
                    with :? HttpRequestException as e ->
                        logger.LogError(e, "Failed to download image {ImageUrl}", img)
            }

            let! newPages = pageSaver.SaveSlicedPagesAsync(mangaTitle, chapterTitle, imageStreams)
            newChapter.Pages.AddRange(newPages)
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
                    regexMatch (Regex("chapter-(.*)")) newChapter.Url
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
            Regex("https://www.natomanga.com/manga.*").IsMatch(url)

        member this.DownloadAsync(url: string, direction: Direction) =
            taskResult {
                let! html = HtmlDocument.tryLoadAsync hc url |> TaskResult.mapError TryLoadAsyncError
                let! titleNode = querySelector html "title" |> Result.mapError QuerySelectorError

                let! title =
                    regexMatch (Regex("(?:Read)?(.*) Manga Online.*")) (titleNode.DirectInnerText())
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
