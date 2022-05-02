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

type Manwha18CCExtractor(
    httpFactory: IHttpClientFactory,
    db: MangaContext,
    mangaRepository: MangaRepository,
    logger: ILogger<Manwha18CCExtractor>) =

    let hc = httpFactory.CreateClient()
    do hc.DefaultRequestHeaders.Referrer <- Uri("https://manhwa18.cc/")

    let getChaptersAsync (url: string) (html: HtmlDocument) (manga: Manga) =
        taskResult {
            let! chapterUrls = extractChapterUrls ".chapter-name" url html
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

    let downloadImage (chapterFolder: string) (i: int) (img: string) =
        taskResult {
            let path = urlToFilePath chapterFolder img i
            use! downloadStream = hc.GetStreamAsync(img)
            do! saveStreamToFileAsync path downloadStream
            return path
        }

    let downloadChapter (newChapter: Chapter) (chapterHtml: HtmlDocument) (chapterTitle: string) (mangaTitle: string) (chapterUrl: string) =
        taskResult {
            let! imgs = extractImageUrls ".read-content img" chapterUrl chapterHtml
            let folder = Path.Combine(mangaData, mangaTitle, chapterTitle)
            Directory.CreateDirectory(folder) |> ignore
            for i, img in Seq.indexed imgs do
                let! path = downloadImage folder i img
                let newPage = Page(Name = Path.GetFileNameWithoutExtension(path), File = path)
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
            Regex("https://manhwa18\.cc/.*").IsMatch(url)

        member this.DownloadAsync(url: string, direction: Direction) =
            taskResult {
                let! html = HtmlDocument.tryLoadAsync hc url
                let! title = cssAndRegex ".post-title h1" (Regex("(.*)")) html
                let! manga = mangaRepository.GetOrCreateAsync(title, direction, url)
                let! chapters = getChaptersAsync url html manga
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
                let! chapters = getChaptersAsync manga.Url html manga
                manga.Chapters <- chapters
                let! _ = db.SaveChangesAsync()

                if manga.Chapters.Exists(fun chapter -> chapter.DownloadStatus = NotDownloaded) then
                    do! downloadChapters manga
                    logger.LogInformation("Finished downloading {Title}", manga.Title)
                    return true
                else
                    return false
            }
