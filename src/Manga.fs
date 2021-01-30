module MangaSharp.Manga

open System.IO
open System.Web
open FSharp.Data
open MangaSharp
open MangaSharp.Util

let private tryGetMangaInfo (manga: MangaSource) =
    let html =
        retryAsync 3 1000 (fun () -> HtmlDocument.tryLoadAsync manga.Url)
        |> Async.RunSynchronously
    match html with
    | Some index ->
        let title = manga.Provider.TitleExtractor manga.Url index
        let chapterUrls = manga.Provider.ChapterUrlsExtractor manga.Url index
        match title, chapterUrls with
        | Some title, Some chapterUrls ->
            Some { Title = title; ChapterUrls = chapterUrls }
        | title, chapterUrls ->
            if title.IsNone then
                printfn "Could not extract title from %s." manga.Url
            if chapterUrls.IsNone then
                printfn "Could not extract chapter URLs from %s." manga.Url
            None
    | None -> None

let private tryGetChapterInfo (manga: MangaSource) (url: string) =
    let html =
        retryAsync 3 1000 (fun () -> HtmlDocument.tryLoadAsync url)
        |> Async.RunSynchronously
    match html with
    | Some chapterPage ->
        let title = manga.Provider.ChapterTitleExtractor url chapterPage
        let imageUrls = manga.Provider.ImageExtractor url chapterPage
        match title, imageUrls with
        | Some title, Some imageUrls ->
            Some { Title = title; ImageRequests = imageUrls }
        | title, imageUrls ->
            if title.IsNone then
                printfn "Could not extract title from %s." url
            if imageUrls.IsNone then
                printfn "Could not extract image URLs from %s." url
            None
    | None -> None

let private downloadChapter (mangaInfo: MangaInfo) (chapterInfo: ChapterInfo) =
    let folder = Path.Combine(mangaData, mangaInfo.Title, chapterInfo.Title)
    Directory.CreateDirectory(folder) |> ignore
    chapterInfo.ImageRequests
    |> Seq.mapi (fun i reqFunc ->
        async {
            let! res = retryAsync 3 1000 (fun () ->
                let req = reqFunc()
                let ext = Path.GetExtension(req.RequestUri.LocalPath)
                let path = Path.ChangeExtension(Path.Combine(folder, sprintf "%03i" (i + 1)), ext)
                downloadFileAsync path req
            )
            match res with
            | Some _ -> ()
            | None -> printfn "Could not download %s." (reqFunc().RequestUri.ToString())
        }
    )
    |> Async.Sequential
    |> Async.Ignore
    |> Async.RunSynchronously

let private downloadChapters (manga: MangaSource) (mangaInfo: MangaInfo) (chapters: ChapterStatus list) =
    let chaptersPath = Path.Combine(mangaData, mangaInfo.Title, "chapters")
    let n =
        chapters
        |> List.filter (fun c -> c.DownloadStatus = NotDownloaded)
        |> List.length
    let rec loop i count chapters =
        if i < List.length chapters then
            if chapters.[i].DownloadStatus = NotDownloaded then
                let chapters' =
                    match tryGetChapterInfo manga chapters.[i].Url with
                    | Some chapterInfo ->
                        printfn "Downloading %s Chapter %s (%i/%i)..." mangaInfo.Title chapterInfo.Title count n
                        downloadChapter mangaInfo chapterInfo
                        chapters
                        |> List.mapAt i (fun chapter ->
                            { chapter with Title = Some chapterInfo.Title; DownloadStatus = Downloaded })
                    | None -> chapters
                ChapterStatus.save chaptersPath chapters'
                loop (i + 1) (count + 1) chapters'
            else
                loop (i + 1) count chapters
    loop 0 1 chapters
    printfn "Finished downloading %s." mangaInfo.Title

let private createMangaDir (title: string) (manga: MangaSource) =
    let dir = Path.Combine(mangaData, title)
    Directory.CreateDirectory(dir) |> ignore
    File.WriteAllText(Path.Combine(dir, "direction"), sprintf "%s\n" (manga.Direction.ToString()))
    File.WriteAllText(Path.Combine(dir, "source"), sprintf "%s\n" manga.Url)

let download (manga: MangaSource) =
    match tryGetMangaInfo manga with
    | Some mangaInfo ->
        createMangaDir mangaInfo.Title manga
        let chapterInfo = ChapterStatus.get mangaInfo.Title
        let chapters = ChapterStatus.merge mangaInfo (List.ofSeq mangaInfo.ChapterUrls) chapterInfo
        if List.exists (fun chapter -> chapter.DownloadStatus = NotDownloaded) chapters then
            downloadChapters manga mangaInfo chapters
            true
        else
            false
    | None -> false

let private fromDir (dir: string) =
    try
        let title = Path.GetFileName(dir)
        let chapterStatuses = ChapterStatus.get title
        let chapters =
            chapterStatuses
            |> List.filter (fun c -> c.DownloadStatus = Downloaded)
            |> List.map (fun c -> Chapter.fromDir (Path.Combine(dir, c.Title.Value)))
            |> NonEmptyList.create
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

let getStoredManga () =
    Directory.GetDirectories(mangaData)
    |> Seq.map fromDir
    |> Seq.sortBy (fun m -> m.Title)
    |> Seq.toList

let firstPage (manga: StoredManga) =
    let chapter = NonEmptyList.head manga.Chapters
    sprintf "/manga/%s/%s" (HttpUtility.UrlEncode manga.Title) chapter.Title

let tryPreviousChapter (manga: StoredManga) (chapter: Chapter) =
    let i = NonEmptyList.findIndex ((=) chapter) manga.Chapters
    NonEmptyList.tryItem (i - 1) manga.Chapters

let tryNextChapter (manga: StoredManga) (chapter: Chapter) =
    let i = NonEmptyList.findIndex ((=) chapter) manga.Chapters
    NonEmptyList.tryItem (i + 1) manga.Chapters

let tryFromTitle (storedManga: StoredManga list) (title: string) =
    match storedManga |> List.tryFind (fun m -> m.Title = title) with
    | Some manga ->
        Some manga
    | None ->
        printfn "Couldn't find a manga titled %s." title
        None

let getRecent (storedManga: StoredManga list) =
    let recentMangaPath = Path.Combine(mangaData, "recent-manga")
    if File.Exists(recentMangaPath) then
        File.ReadAllLines(recentMangaPath)
        |> Seq.toList
        |> List.map (tryFromTitle storedManga)
        |> List.choose id
        |> List.truncate 5
    else
        printfn "Could not read %s." recentMangaPath
        []

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
