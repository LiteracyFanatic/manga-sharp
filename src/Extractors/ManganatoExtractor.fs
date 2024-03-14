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

type ManganatoExtractor
    (
        httpFactory: IHttpClientFactory,
        db: MangaContext,
        mangaRepository: MangaRepository,
        pageSaver: PageSaver,
        logger: ILogger<ManganatoExtractor>
    ) =

    let hc = httpFactory.CreateClient()
    do hc.DefaultRequestHeaders.Referrer <- Uri("https://chapmanganato.com/")

    let getChaptersAsync (url: string) (html: HtmlDocument) (manga: Manga) =
        taskResult {
            let! chapterUrls = extractChapterUrls ".chapter-name" url html

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

    let downloadChapter (newChapter: Chapter) (chapterHtml: HtmlDocument) (chapterTitle: string) (mangaTitle: string) =
        taskResult {
            let! imgs =
                querySelectorAll chapterHtml ".container-chapter-reader img"
                |> Result.map (List.map (HtmlNode.attributeValue "src"))

            for i, img in List.indexed imgs do
                use! imageStream = hc.GetStreamAsync(img)
                let! newPage = pageSaver.SavePageAsync(mangaTitle, chapterTitle, i, imageStream)
                newChapter.Pages.Add(newPage)
        }

    let downloadChapters (manga: Manga) =
        taskResult {
            let newChapters =
                manga.Chapters
                |> Seq.filter (fun c -> c.DownloadStatus = DownloadStatus.NotDownloaded)
                |> Seq.toList

            for i, newChapter in Seq.indexed newChapters do
                let! chapterHtml = HtmlDocument.tryLoadAsync hc newChapter.Url
                let! chapterTitle = regexMatch (Regex("chapter-(.*)")) newChapter.Url

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
            Regex("https://(chap)?manganato\.(com|to)/manga.*").IsMatch(url)

        member this.DownloadAsync(url: string, direction: Direction) =
            taskResult {
                let! html = HtmlDocument.tryLoadAsync hc url
                let! title = cssAndRegex "title" (Regex("(.*) Manga Online Free - Manganato")) html
                let! manga = mangaRepository.GetOrCreateAsync(title, direction, url)
                let! chapters = getChaptersAsync url html manga
                manga.Chapters <- chapters
                let! _ = db.SaveChangesAsync()

                if manga.Chapters.Exists(fun chapter -> chapter.DownloadStatus = DownloadStatus.NotDownloaded) then
                    do! downloadChapters manga
                    logger.LogInformation("Finished downloading {Title}", title)
            }

        member this.UpdateAsync(mangaId: Guid) =
            taskResult {
                let! manga = mangaRepository.GetByIdAsync(mangaId)
                let! html = HtmlDocument.tryLoadAsync hc manga.Url
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
