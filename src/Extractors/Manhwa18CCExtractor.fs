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

type Manwha18CCExtractor
    (
        httpFactory: IHttpClientFactory,
        db: MangaContext,
        mangaRepository: MangaRepository,
        pageSaver: PageSaver,
        logger: ILogger<Manwha18CCExtractor>
    ) =

    let hc = httpFactory.CreateClient()
    do hc.DefaultRequestHeaders.Referrer <- Uri("https://manhwa18.cc/")

    let getChaptersAsync
        (url: string)
        (html: HtmlDocument)
        (manga: Manga)
        : TaskResult<ResizeArray<Chapter>, CommonError> =
        taskResult {
            let! chapterUrls =
                extractChapterUrls ".chapter-name" url html
                |> Result.mapError QuerySelectorAllError

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
        (chapterUrl: string)
        : TaskResult<unit, CommonError> =
        taskResult {
            let! imgs =
                extractImageUrls ".read-content img" chapterUrl chapterHtml
                |> Result.mapError QuerySelectorAllError

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
                    regexMatch (Regex("chapter-(\d+(-\d+)*)")) newChapter.Url
                    |> Result.mapError RegexMatchError

                logger.LogInformation(
                    "Downloading {Title} Chapter {ChapterTitle} ({ChapterNumber}/{NumberOfChapters})",
                    manga.Title,
                    chapterTitle,
                    (i + 1),
                    newChapters.Length
                )

                do! downloadChapter newChapter chapterHtml chapterTitle manga.Title newChapter.Url
                newChapter.Title <- chapterTitle
                newChapter.DownloadStatus <- DownloadStatus.Downloaded
                let! _ = db.SaveChangesAsync()
                ()
        }

    interface IMangaExtractor with

        member this.IsMatch(url: string) =
            Regex("https://manhwa18\.cc/.*").IsMatch(url)

        member this.DownloadAsync(url: string, direction: Direction) =
            taskResult {
                let! html = HtmlDocument.tryLoadAsync hc url |> TaskResult.mapError TryLoadAsyncError

                let! title =
                    querySelector html ".post-title h1"
                    |> Result.eitherMap (_.DirectInnerText().Trim()) QuerySelectorError

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
