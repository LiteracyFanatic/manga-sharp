module MangaSharp.Manga

open System
open System.IO
open System.Net
open FSharp.Data
open MangaSharp

type MangaInfo = {
    Title: string
    ChapterUrls: string seq
}

type ChapterInfo = {
    Title: string
    ImageUrls: string seq
}

let getMangaInfo (manga: MangaSource) =
    let index = HtmlDocument.Load(manga.Url)
    {
        Title =  manga.Provider.TitleExtractor index
        ChapterUrls = manga.Provider.ChapterUrlsExtractor index
    }

let getChapterInfo (manga: MangaSource) (url: string) =
    let chapterPage = HtmlDocument.Load(url)
    {
        Title = manga.Provider.ChapterTitleExtractor chapterPage
        ImageUrls = manga.Provider.ImageExtractor chapterPage
    }

let downloadFileAsync (path: string) (url: string) =
    async {
        use wc = new WebClient()
        return! wc.AsyncDownloadFile(Uri(url), path)
    }

let downloadChapter (mangaInfo: MangaInfo) (chapterInfo: ChapterInfo) =
    let folder = Path.Combine(mangaData, mangaInfo.Title, chapterInfo.Title)
    Directory.CreateDirectory(folder) |> ignore
    chapterInfo.ImageUrls
    |> Seq.mapi (fun i url ->
        let ext = Path.GetExtension(Uri(url).LocalPath)
        let path = Path.ChangeExtension(Path.Combine(folder, sprintf "%03i" (i + 1)), ext)
        downloadFileAsync path url
    )
    |> Async.Sequential
    |> Async.Ignore
    |> Async.RunSynchronously

let getExistingChapterUrls (mangaInfo: MangaInfo) =
    let chaptersPath = Path.Combine(mangaData, mangaInfo.Title, "chapters")
    if File.Exists(chaptersPath) then
        Seq.ofArray (File.ReadAllLines(chaptersPath))
    else
        Seq.empty

let getNewChapterUrls (mangaInfo: MangaInfo) =
    let chapterUrlsSet = Set.ofSeq mangaInfo.ChapterUrls
    let existingChaptersSet = Set.ofSeq (getExistingChapterUrls mangaInfo)
    if not (existingChaptersSet.IsSubsetOf chapterUrlsSet) then
        printfn "WARNING: There are previously downloaded chapters from URLs not listed by the current source. This may indicate a change in page structure."

    let newChapters = Set.difference chapterUrlsSet existingChaptersSet
    if Set.isEmpty newChapters then
        Seq.empty
    else
        Seq.filter newChapters.Contains mangaInfo.ChapterUrls

let downloadChapterSeq (manga: MangaSource) (mangaInfo: MangaInfo) (urls: string seq) =
    let n = Seq.length urls
    urls
    |> Seq.iteri (fun i url ->
        let chapterInfo = getChapterInfo manga url
        printfn "Downloading %s Chapter %s (%i/%i)..." mangaInfo.Title chapterInfo.Title (i + 1) n
        downloadChapter mangaInfo chapterInfo
        let chaptersPath = Path.Combine(mangaData, mangaInfo.Title, "chapters")
        File.AppendAllText(chaptersPath, sprintf "%s\n" url)
    )
    printfn "Finished downloading %s." mangaInfo.Title

let createMangaDir (title: string) (manga: MangaSource) =
    let dir = Path.Combine(mangaData, title)
    Directory.CreateDirectory(dir) |> ignore
    File.WriteAllText(Path.Combine(dir, "direction"), sprintf "%s\n" (manga.Direction.ToString()))
    File.WriteAllText(Path.Combine(dir, "source"), sprintf "%s\n" manga.Url)

let download (manga: MangaSource) =
    let mangaInfo = getMangaInfo manga
    createMangaDir mangaInfo.Title manga
    downloadChapterSeq manga mangaInfo mangaInfo.ChapterUrls

let update (manga: MangaSource) =
    let mangaInfo = getMangaInfo manga
    let chapters = getNewChapterUrls mangaInfo
    if Seq.isEmpty chapters then
        false
    else
        downloadChapterSeq manga mangaInfo chapters
        true

let fromDir (dir: string) =
    let title = Path.GetFileName(dir)
    let chapters =
        Directory.GetDirectories(dir)
        |> Array.toList
        |> List.map Chapter.fromDir
        |> List.sortBy (fun c -> float c.Title)
    let indexUrl = File.ReadAllText(Path.Combine(dir, "source")).Trim()
    let directionPath = Path.Combine(dir, "direction")
    let direction =
        match File.ReadAllText(directionPath).Trim() with
        | "horizontal" -> Horizontal
        | "vertical" -> Vertical
        | _ -> failwithf "%s does not contain a valid direction." directionPath
    let source = {
        Url = indexUrl
        Direction = direction
        Provider = Provider.tryFromTable indexUrl |> Option.get
    }
    {
        Title = title
        Chapters = chapters
        Bookmark = Bookmark.tryReadBookmark title
        Source = source
    }

let getStoredManga () =
    Directory.GetDirectories(mangaData)
    |> Seq.map fromDir
    |> Seq.sortBy (fun m -> m.Title)
    |> Seq.toList

let firstPage (manga: StoredManga) =
    let chapter = manga.Chapters.Head
    sprintf "/manga/%s/%s" manga.Title chapter.Title

let tryPreviousChapter (manga: StoredManga) (chapter: Chapter) =
    let i = List.findIndex ((=) chapter) manga.Chapters
    List.tryItem (i - 1) manga.Chapters

let tryNextChapter (manga: StoredManga) (chapter: Chapter) =
    let i = List.findIndex ((=) chapter) manga.Chapters
    List.tryItem (i + 1) manga.Chapters

let tryFromTitle (title: string) =
    getStoredManga ()
    |> List.tryFind (fun m -> m.Title = title)

let tryLast () =
    let lastMangaPath = Path.Combine(mangaData, "last-manga")
    if File.Exists(lastMangaPath) then
        tryFromTitle (File.ReadAllText(lastMangaPath).Trim())
    else None
