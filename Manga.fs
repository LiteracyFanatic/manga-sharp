module MangaSharp.Manga

open System
open System.IO
open System.Web
open FSharp.Data
open MangaSharp
open MangaSharp.Util

type private MangaInfo = {
    Title: string
    ChapterUrls: string seq
}

type private ChapterInfo = {
    Title: string
    ImageUrls: string seq
}

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
    | None ->  None

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
            Some { Title = title; ImageUrls = imageUrls }
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
    chapterInfo.ImageUrls
    |> Seq.mapi (fun i url ->
        async {
            let ext = Path.GetExtension(Uri(url).LocalPath)
            let path = Path.ChangeExtension(Path.Combine(folder, sprintf "%03i" (i + 1)), ext)
            match! retryAsync 3 1000 (fun () -> downloadFileAsync path url) with
            | Some _ -> ()
            | None -> printfn "Could not download %s." url
        }
    )
    |> Async.Sequential
    |> Async.Ignore
    |> Async.RunSynchronously

let private getExistingChapterUrls (mangaInfo: MangaInfo) =
    let chaptersPath = Path.Combine(mangaData, mangaInfo.Title, "chapters")
    if File.Exists(chaptersPath) then
        Seq.ofArray (File.ReadAllLines(chaptersPath))
    else
        Seq.empty

let private getNewChapterUrls (mangaInfo: MangaInfo) =
    let chapterUrlsSet = Set.ofSeq mangaInfo.ChapterUrls
    let existingChaptersSet = Set.ofSeq (getExistingChapterUrls mangaInfo)
    if not (existingChaptersSet.IsSubsetOf chapterUrlsSet) then
        printfn "WARNING: There are previously downloaded chapters for %s from URLs not listed by the current source. This may indicate a change in page structure."
            mangaInfo.Title

    let newChapters = Set.difference chapterUrlsSet existingChaptersSet
    if Set.isEmpty newChapters then
        Seq.empty
    else
        Seq.filter newChapters.Contains mangaInfo.ChapterUrls

let private downloadChapterSeq (manga: MangaSource) (mangaInfo: MangaInfo) (urls: string seq) =
    let n = Seq.length urls
    urls
    |> Seq.iteri (fun i url ->
        match tryGetChapterInfo manga url with
        | Some chapterInfo ->
            printfn "Downloading %s Chapter %s (%i/%i)..." mangaInfo.Title chapterInfo.Title (i + 1) n
            downloadChapter mangaInfo chapterInfo
            let chaptersPath = Path.Combine(mangaData, mangaInfo.Title, "chapters")
            File.AppendAllText(chaptersPath, sprintf "%s\n" url)
        | None ->
            ()
    )
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
        downloadChapterSeq manga mangaInfo mangaInfo.ChapterUrls
    | None ->
        ()

let update (manga: MangaSource) =
    match tryGetMangaInfo manga with
    | Some mangaInfo ->
        let chapters = getNewChapterUrls mangaInfo
        if Seq.isEmpty chapters then
            false
        else
            downloadChapterSeq manga mangaInfo chapters
            true
    | None ->
        false

let private fromDir (dir: string) =
    let title = Path.GetFileName(dir)
    let chapters =
        Directory.GetDirectories(dir)
        |> Array.toList
        |> List.choose Chapter.tryFromDir
        |> List.sortBy (fun c -> float c.Title)
        |> NonEmptyList.tryCreate
    let indexUrl = File.tryReadAllText (Path.Combine(dir, "source"))
    let direction =
        File.tryReadAllText (Path.Combine(dir, "direction"))
        |> Option.map Direction.tryParse
        |> Option.flatten
    let provider =
        indexUrl
        |> Option.map Provider.tryFromTable
        |> Option.flatten
    match chapters, indexUrl, direction, provider with
    | Some chapters, Some indexUrl, Some direction, Some provider ->
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
        Some manga
    | _ ->
        printfn "Could not process %s." dir
        None

let getStoredManga () =
    Directory.GetDirectories(mangaData)
    |> Seq.choose fromDir
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

let tryFromTitle (title: string) =
    match getStoredManga () |> List.tryFind (fun m -> m.Title = title) with
    | Some manga ->
        Some manga
    | None ->
        printfn "Couldn't find a manga titled %s." title
        None

let getRecent () =
    let recentMangaPath = Path.Combine(mangaData, "recent-manga")
    if File.Exists(recentMangaPath) then
        File.ReadAllLines(recentMangaPath)
        |> Seq.toList
        |> List.map tryFromTitle
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
