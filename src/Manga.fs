module MangaSharp.Manga

open System.IO
open System.Web
open MangaSharp.Util
open FsToolkit.ErrorHandling

let private tryGetMangaInfo (manga: MangaSource) =
    taskResult {
        let! html =
            retryAsync 3 1000 (fun () -> HtmlDocument.tryLoadAsync manga.Url)
            |> TaskResult.mapError List.singleton
        let title = manga.Provider.TitleExtractor manga.Url html
        let chapterUrls = manga.Provider.ChapterUrlsExtractor manga.Url html
        match title, chapterUrls with
        | Ok title, Ok chapterUrls ->
            return { Title = title; ChapterUrls = chapterUrls }
        | title, chapterUrls ->
            return! Error [
                if Result.isError title then
                    $"Could not extract title from %s{manga.Url}."
                if Result.isError chapterUrls then
                    $"Could not extract chapter URLs from %s{manga.Url}."
            ]
    } |> Async.AwaitTask
    |> Async.RunSynchronously

let private tryGetChapterInfo (manga: MangaSource) (url: string) =
    taskResult {
        let! html =
            retryAsync 3 1000 (fun () -> HtmlDocument.tryLoadAsync url)
            |> TaskResult.mapError List.singleton
        let title = manga.Provider.ChapterTitleExtractor url html
        let imageUrls = manga.Provider.ImageExtractor url html
        match title, imageUrls with
        | Ok title, Ok imageUrls ->
            return { Title = title; ImageRequests = imageUrls }
        | title, imageUrls ->
            return! Error [
                if Result.isError title then
                    printfn "Could not extract title from %s." url
                if Result.isError imageUrls then
                    printfn "Could not extract image URLs from %s." url
            ]
    } |> Async.AwaitTask
    |> Async.RunSynchronously

let private downloadChapter (mangaInfo: MangaInfo) (chapterInfo: ChapterInfo) =
    let folder = Path.Combine(mangaData, mangaInfo.Title, chapterInfo.Title)
    Directory.CreateDirectory(folder) |> ignore
    task {
        for i, reqFunc in Seq.indexed chapterInfo.ImageRequests do
            let! res = retryAsync 3 1000 (fun () ->
                let req = reqFunc()
                let ext = Path.GetExtension(req.RequestUri.LocalPath)
                let path = Path.ChangeExtension(Path.Combine(folder, $"%03i{i + 1}"), ext)
                downloadFileAsync path req
            )
            match res with
            | Ok _ -> ()
            | Error _ -> printfn "Could not download %s." (reqFunc().RequestUri.ToString())
    } |> Async.AwaitTask
    |> Async.RunSynchronously

let private downloadChapters (manga: MangaSource) (mangaInfo: MangaInfo) (chapters: ChapterStatus list) =
    let chaptersPath = Path.Combine(mangaData, mangaInfo.Title, "chapters")
    let n =
        chapters
        |> List.filter (fun c -> c.DownloadStatus = NotDownloaded)
        |> List.length
    let rec loop i count chapters =
        if i < List.length chapters then
            if chapters[i].DownloadStatus = NotDownloaded then
                let chapters' =
                    match tryGetChapterInfo manga chapters[i].Url with
                    | Ok chapterInfo ->
                        printfn "Downloading %s Chapter %s (%i/%i)..." mangaInfo.Title chapterInfo.Title count n
                        downloadChapter mangaInfo chapterInfo
                        chapters
                        |> List.mapAt i (fun chapter ->
                            { chapter with Title = Some chapterInfo.Title; DownloadStatus = Downloaded })
                    | Error _ -> chapters
                ChapterStatus.save chaptersPath chapters'
                loop (i + 1) (count + 1) chapters'
            else
                loop (i + 1) count chapters
    loop 0 1 chapters
    printfn "Finished downloading %s." mangaInfo.Title

let private createMangaDir (title: string) (manga: MangaSource) =
    let dir = Path.Combine(mangaData, title)
    Directory.CreateDirectory(dir) |> ignore
    File.WriteAllText(Path.Combine(dir, "direction"), $"%s{manga.Direction.ToString()}\n")
    File.WriteAllText(Path.Combine(dir, "source"), $"%s{manga.Url}\n")

let download (manga: MangaSource) =
    match tryGetMangaInfo manga with
    | Ok mangaInfo ->
        createMangaDir mangaInfo.Title manga
        let chapterInfo = ChapterStatus.get mangaInfo.Title
        let chapters = ChapterStatus.merge mangaInfo (List.ofSeq mangaInfo.ChapterUrls) chapterInfo
        if List.exists (fun chapter -> chapter.DownloadStatus = NotDownloaded) chapters then
            downloadChapters manga mangaInfo chapters
            true
        else
            false
    | Error _ -> false

let private fromDir (dir: string) =
    try
        let title = Path.GetFileName(dir)
        let chapterStatuses = ChapterStatus.get title
        let chapters =
            chapterStatuses
            |> List.filter (fun c -> c.DownloadStatus = Downloaded)
            |> List.map (fun c -> Chapter.fromDir (Path.Combine(dir, c.Title.Value)))
        let indexUrl = File.ReadAllText (Path.Combine(dir, "source"))
        let directionText = File.ReadAllText(Path.Combine(dir, "direction")).Trim()
        let direction = Direction.parse directionText
        let provider = Provider.fromTable indexUrl
        let source = {
            Url = indexUrl
            Direction = direction
            Provider = provider
        }
        let manga = {
            Title = title
            Chapters = chapters
            Bookmark = Bookmark.tryReadBookmark title
            Source = source
        }
        manga
    with
    | e ->
        failwithf "Could not process %s." dir

let fromTitle (mangaTitle: string) =
    fromDir (Path.Combine(mangaData, mangaTitle))

let firstPage (manga: StoredManga) =
    let chapter = List.head manga.Chapters
    $"/manga/%s{HttpUtility.UrlEncode manga.Title}/%s{chapter.Title}"

let tryPreviousChapter (manga: StoredManga) (chapter: Chapter) =
    let i = List.findIndex ((=) chapter) manga.Chapters
    List.tryItem (i - 1) manga.Chapters

let tryNextChapter (manga: StoredManga) (chapter: Chapter) =
    let i = List.findIndex ((=) chapter) manga.Chapters
    List.tryItem (i + 1) manga.Chapters

let setLast (title: string) =
    let recentMangaPath = Path.Combine(mangaData, "recent-manga")
    let recentManga =
        if File.Exists(recentMangaPath) then
            File.ReadAllLines recentMangaPath
            |> Array.filter ((<>) title)
            |> Array.append [| title |]
            |> Array.truncate 5
        else
            [| title |]
    File.WriteAllLines(recentMangaPath, recentManga)
