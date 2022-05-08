namespace MangaSharp.Extractors

open System
open System.IO
open FSharp.Data
open System.Net.Http
open System.Text.RegularExpressions
open System.Linq
open Microsoft.Extensions.Logging
open MangaSharp.Database
open MangaSharp.Database.MangaDomain
open MangaSharp.Extractors.Util
open FsToolkit.ErrorHandling

type ManyToonExtractor(
    httpFactory: IHttpClientFactory,
    db: MangaContext,
    mangaRepository: MangaRepository,
    pageSaver: PageSaver,
    logger: ILogger<ManyToonExtractor>) =

    let hc = httpFactory.CreateClient()

    let getChaptersAsync (url: string) (manga: Manga) =
        taskResult {
            let chaptersUrl = Path.Join(url, "ajax/chapters")
            use! response = hc.PostAsync(chaptersUrl, null)
            response.EnsureSuccessStatusCode() |> ignore
            let! htmlContent = response.Content.ReadAsStringAsync()
            let htmlContent = $"<html><head></head><body>%s{htmlContent}</body></html>"
            let! htmlDoc = HtmlDocument.tryParse htmlContent
            let! chapters = querySelectorAll htmlDoc ".wp-manga-chapter a"
            let chapterUrls =
                chapters
                |> Seq.rev
                |> Seq.map (HtmlNode.attributeValue "href")
                |> Seq.distinct

            let chapters =
                chapterUrls
                |> Seq.map (fun url ->
                    Chapter(
                        Url = url,
                        Title = None,
                        DownloadStatus = NotDownloaded))
                |> Seq.toList
            let mergedChapters =
                chapters.Select(fun chapter i ->
                    let chapter =
                        match manga.Chapters |> Seq.tryFind (fun c -> c.Url = chapter.Url) with
                        | Some c -> c
                        | None -> chapter
                    chapter.Index <- i
                    chapter)
                    .ToList()
            return mergedChapters
        }

    let downloadChapter (newChapter: Chapter) (chapterHtml: HtmlDocument) (chapterTitle: string) (mangaTitle: string) (chapterUrl: string) =
        taskResult {
            let! nodes = querySelectorAll chapterHtml ".wp-manga-chapter-img"
            // Remove duplicates and data URLs
            let imgs =
                nodes
                |> Seq.collect (fun img -> [HtmlNode.attributeValue "src" img; HtmlNode.attributeValue "data-src" img])
                |> Seq.filter (fun src -> not (src.StartsWith("data")))
                |> Seq.distinct
                |> Seq.map (resolveUrl chapterUrl)
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
                let! chapterTitle = regexMatch (Regex("chapter-(\d+(-\d+)*)")) newChapter.Url
                logger.LogInformation(
                    "Downloading {Title} Chapter {ChapterTitle} ({ChapterNumber}/{NumberOfChapters})",
                    manga.Title,
                    chapterTitle,
                    (i + 1),
                    newChapters.Length)
                do! downloadChapter newChapter chapterHtml chapterTitle manga.Title newChapter.Url
                newChapter.Title <- Some chapterTitle
                newChapter.DownloadStatus <- Downloaded
                let! _ = db.SaveChangesAsync()
                ()
        }

    interface IMangaExtractor with

        member this.IsMatch(url: string) =
            Regex("https://manytoon\.com/comic/.*").IsMatch(url)

        member this.DownloadAsync(url: string, direction: Direction) =
            taskResult {
                let! html = HtmlDocument.tryLoadAsync hc url
                let! title = cssAndRegex ".post-title h1" (Regex("(.*)")) html
                let! manga = mangaRepository.GetOrCreateAsync(title, direction, url)
                let! chapters = getChaptersAsync url manga
                manga.Chapters <- chapters
                let! _ = db.SaveChangesAsync()

                if manga.Chapters.Exists(fun chapter -> chapter.DownloadStatus = NotDownloaded) then
                    do! downloadChapters manga
                    logger.LogInformation("Finished downloading {Title}", title)
            }

        member this.UpdateAsync(mangaId: Guid) =
            taskResult {
                let! manga = mangaRepository.GetByIdAsync(mangaId)
                let! chapters = getChaptersAsync manga.Url manga
                manga.Chapters <- chapters
                let! _ = db.SaveChangesAsync()

                if manga.Chapters.Exists(fun chapter -> chapter.DownloadStatus = NotDownloaded) then
                    do! downloadChapters manga
                    logger.LogInformation("Finished downloading {Title}", manga.Title)
                    return true
                else
                    return false
            }
