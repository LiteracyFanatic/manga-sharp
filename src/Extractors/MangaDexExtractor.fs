namespace MangaSharp.Extractors

open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Net.Http.Headers
open System.Text.RegularExpressions
open System.Linq
open MangaSharp
open Microsoft.Extensions.Logging
open MangaSharp.Database
open MangaSharp.Extractors.MangaDex
open MangaSharp.Extractors.Util
open FsToolkit.ErrorHandling

type MangaDexExtractor
    (
        httpFactory: IHttpClientFactory,
        db: MangaContext,
        mangaRepository: MangaRepository,
        pageSaver: PageSaver,
        mangaDexApi: MangaDexApi,
        logger: ILogger<MangaDexExtractor>,
        versionInfo: VersionInfo
    ) =

    let hc = httpFactory.CreateClient()

    let userAgent = ProductInfoHeaderValue("MangaSharp", versionInfo.Version)

    do hc.DefaultRequestHeaders.UserAgent.Add(userAgent)

    let getIdFromUrl (url: string) =
        regexMatch (Regex("https://mangadex\.org/title/([^/]*)(/.*)?")) url
        |> Result.mapError RegexMatchError

    let getChaptersAsync (mangaId: string) (manga: Manga) (chapterNumbersResetOnNewVolume: bool) =
        taskResult {
            let! res = mangaDexApi.GetChaptersAsync(mangaId)

            let chapters =
                res
                |> Seq.map (fun c ->
                    let chapterName =
                        c.attributes.chapter |> Option.defaultWith (fun () -> c.attributes.title.Value)

                    let title =
                        if chapterNumbersResetOnNewVolume then
                            $"%s{c.attributes.volume.Value}.%s{chapterName}"
                        else
                            chapterName

                    Chapter(
                        Url = $"https://mangadex.org/chapter/%s{c.id}",
                        Title = title,
                        DownloadStatus = DownloadStatus.NotDownloaded
                    ))
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
        |> TaskResult.catch Other

    let downloadPage
        (mangaTitle: string)
        (chapterTitle: string)
        (i: int)
        (img: string)
        : TaskResult<Page, CommonError> =
        taskResult {
            let sw = Stopwatch()
            sw.Start()

            try
                use! res = hc.GetAsync(img)
                res.EnsureSuccessStatusCode() |> ignore
                use! imageStream = res.Content.ReadAsStreamAsync()
                sw.Stop()
                let! page = pageSaver.SavePageAsync(mangaTitle, chapterTitle, i, imageStream)

                let cached =
                    if res.Headers.Contains("X-Cache") then
                        res.Headers.GetValues("X-Cache")
                        |> Seq.exists (fun header -> header.StartsWith("HIT"))
                    else
                        false

                let request = {
                    url = img
                    success = true
                    cached = cached
                    bytes = imageStream.Length
                    duration = sw.ElapsedMilliseconds
                }

                do! mangaDexApi.PostHealthReportAsync(request)
                return page
            with e ->
                sw.Stop()

                let request = {
                    url = img
                    success = false
                    cached = false
                    bytes = 0
                    duration = sw.ElapsedMilliseconds
                }

                do! mangaDexApi.PostHealthReportAsync(request)
                return! Error(Other e)
        }

    let downloadChapter (newChapter: Chapter) (chapterTitle: string) (chapterId: string) (mangaTitle: string) =
        taskResult {
            let! res = mangaDexApi.GetAtHomeAsync(chapterId)

            let imgs =
                res.chapter.data
                |> Seq.map (fun p -> Path.Combine(res.baseUrl, "data", res.chapter.hash, p))

            for i, img in Seq.indexed imgs do
                let! newPage = downloadPage mangaTitle chapterTitle i img
                newChapter.Pages.Add(newPage)
        }

    let downloadChapters (manga: Manga) : TaskResult<unit, CommonError> =
        taskResult {
            let newChapters =
                manga.Chapters
                |> Seq.filter (fun c -> c.DownloadStatus = DownloadStatus.NotDownloaded)
                |> Seq.toList

            for i, newChapter in Seq.indexed newChapters do
                let! chapterId =
                    regexMatch (Regex("https://mangadex.org/chapter/(.*)")) newChapter.Url
                    |> Result.mapError RegexMatchError

                let chapterTitle = newChapter.Title

                logger.LogInformation(
                    "Downloading {Title} Chapter {ChapterTitle} ({ChapterNumber}/{NumberOfChapters})",
                    manga.Title,
                    chapterTitle,
                    (i + 1),
                    newChapters.Length
                )

                do! downloadChapter newChapter chapterTitle chapterId manga.Title
                newChapter.DownloadStatus <- DownloadStatus.Downloaded
                let! _ = db.SaveChangesAsync()
                ()
        }

    interface IMangaExtractor with

        member this.IsMatch(url: string) =
            Regex("https://mangadex\.org/title/.*").IsMatch(url)

        member this.DownloadAsync(url: string, direction: Direction) =
            taskResult {
                let! mangaId = getIdFromUrl url
                let! res = mangaDexApi.GetMangaAsync(mangaId)

                let title =
                    res.data.attributes.title.en
                    |> Option.orElseWith (fun () ->
                        res.data.attributes.altTitles |> Seq.tryPick (fun altTitle -> altTitle.en))
                    |> Option.orElse res.data.attributes.title.ja
                    |> Option.defaultWith (fun () ->
                        res.data.attributes.altTitles |> Seq.pick (fun altTitle -> altTitle.ja))

                let! manga = mangaRepository.GetOrCreateAsync(title, direction, url)
                let! chapters = getChaptersAsync mangaId manga res.data.attributes.chapterNumbersResetOnNewVolume
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
                let! mangaId = getIdFromUrl manga.Url
                let! res = mangaDexApi.GetMangaAsync(mangaId)
                let! chapters = getChaptersAsync mangaId manga res.data.attributes.chapterNumbersResetOnNewVolume
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
