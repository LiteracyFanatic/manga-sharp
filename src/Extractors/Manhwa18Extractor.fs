namespace MangaSharp.Extractors

open System
open System.Globalization
open FSharp.Data
open System.Net.Http
open System.Net.Http.Headers
open System.Text.RegularExpressions
open System.Linq
open Microsoft.Extensions.Logging
open MangaSharp.Database
open MangaSharp.Database.MangaDomain
open MangaSharp.Extractors.Util
open FsToolkit.ErrorHandling

type Manwha18Extractor
    (
        httpFactory: IHttpClientFactory,
        db: MangaContext,
        mangaRepository: MangaRepository,
        pageSaver: PageSaver,
        logger: ILogger<Manwha18Extractor>
    ) =

    let hc = httpFactory.CreateClient()

    let userAgent =
        "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:15.0) Gecko/20100101 Firefox/15.0.1"

    do hc.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent)

    let getChaptersAsync (html: HtmlDocument) (manga: Manga) =
        taskResult {
            let! chapterLinks = querySelectorAll html ".list-chapters a"

            let chapterUrls =
                chapterLinks
                |> Seq.rev
                |> Seq.map (HtmlNode.attributeValue "href")
                |> Seq.distinct

            let chapters =
                chapterUrls
                |> Seq.map (fun url -> Chapter(Url = url, Title = None, DownloadStatus = NotDownloaded))
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

    let downloadChapter (newChapter: Chapter) (chapterHtml: HtmlDocument) (chapterTitle: string) (mangaTitle: string) =
        taskResult {
            let! nodes = querySelectorAll chapterHtml "#chapter-content img"
            let imgs = nodes |> Seq.map (HtmlNode.attributeValue "data-src")

            for i, img in Seq.indexed imgs do
                use! imageStream = hc.GetStreamAsync(img)
                let! newPage = pageSaver.SavePageAsync(mangaTitle, chapterTitle, i, imageStream)
                newChapter.Pages.Add(newPage)
        }

    let downloadChapters (manga: Manga) =
        taskResult {
            let newChapters =
                manga.Chapters
                |> Seq.filter (fun c -> c.DownloadStatus = NotDownloaded)
                |> Seq.toList

            for i, newChapter in Seq.indexed newChapters do
                let! chapterHtml = HtmlDocument.tryLoadAsync hc newChapter.Url
                let! chapterTitle = regexMatch (Regex("(?:ch|chap|chapter)-(\d+)")) newChapter.Url

                logger.LogInformation(
                    "Downloading {Title} Chapter {ChapterTitle} ({ChapterNumber}/{NumberOfChapters})",
                    manga.Title,
                    chapterTitle,
                    (i + 1),
                    newChapters.Length
                )

                do! downloadChapter newChapter chapterHtml chapterTitle manga.Title
                newChapter.Title <- Some chapterTitle
                newChapter.DownloadStatus <- Downloaded
                let! _ = db.SaveChangesAsync()
                ()
        }

    interface IMangaExtractor with

        member this.IsMatch(url: string) =
            Regex("https://manhwa18\.com/.*").IsMatch(url)

        member this.DownloadAsync(url: string, direction: Direction) =
            taskResult {
                let! html = HtmlDocument.tryLoadAsync hc url
                let textInfo = CultureInfo("en-US", false).TextInfo
                let! node = querySelector html ".series-name a"

                let title =
                    node
                    |> HtmlNode.directInnerText
                    |> fun s -> s.ToLowerInvariant()
                    |> textInfo.ToTitleCase

                let! manga = mangaRepository.GetOrCreateAsync(title, direction, url)
                let! chapters = getChaptersAsync html manga
                manga.Chapters <- chapters
                let! _ = db.SaveChangesAsync()

                if manga.Chapters.Exists(fun chapter -> chapter.DownloadStatus = NotDownloaded) then
                    do! downloadChapters manga
                    logger.LogInformation("Finished downloading {Title}", title)
            }

        member this.UpdateAsync(mangaId: Guid) =
            taskResult {
                let! manga = mangaRepository.GetByIdAsync(mangaId)
                let! html = HtmlDocument.tryLoadAsync hc manga.Url
                let! chapters = getChaptersAsync html manga
                manga.Chapters <- chapters
                let! _ = db.SaveChangesAsync()

                if manga.Chapters.Exists(fun chapter -> chapter.DownloadStatus = NotDownloaded) then
                    do! downloadChapters manga
                    logger.LogInformation("Finished downloading {Title}", manga.Title)
                    return true
                else
                    return false
            }
