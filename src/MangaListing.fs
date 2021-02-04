module MangaSharp.MangaListing

open System.IO
open System.Web

let firstPage (manga: MangaListing) =
    let chapter =
        manga.Title
        |> ChapterStatus.get
        |> List.find (fun c -> c.DownloadStatus = Downloaded)
    $"/manga/%s{HttpUtility.UrlEncode manga.Title}/%s{chapter.Title.Value}"

let private fromDir (dir: string) =
    try
        let title = Path.GetFileName(dir)
        let indexUrl = File.ReadAllText (Path.Combine(dir, "source"))
        let numberOfChapters =
            title
            |> ChapterStatus.get
            |> List.filter (fun c -> c.DownloadStatus = Downloaded)
            |> List.length
        let directionText = File.ReadAllText(Path.Combine(dir, "direction")).Trim()
        let direction = Direction.parse directionText
        let provider = Provider.fromTable indexUrl
        let bookmark = Bookmark.tryReadBookmark title
        let chapterIndex =
            match bookmark with
            | Some b -> Bookmark.getChapterIndex title b
            | None -> 0
        let source = {
            Url = indexUrl
            Direction = direction
            Provider = provider
        }
        let manga = {
            Title = title
            Bookmark = bookmark
            Source = source
            ChapterIndex = chapterIndex
            NumberOfChapters = numberOfChapters
        }
        manga
    with
    | e ->
        failwithf "Could not process %s." dir

let tryFromTitle (title: string) =
    let dir = Path.Combine(mangaData, title)
    if Directory.Exists dir then
        Some (fromDir dir)
    else
        None

let getAll () =
    Directory.GetDirectories(mangaData)
    |> Seq.map fromDir
    |> Seq.sortBy (fun m -> m.Title)
    |> Seq.toList

let getRecent () =
    let recentMangaPath = Path.Combine(mangaData, "recent-manga")
    if File.Exists(recentMangaPath) then
        File.ReadAllLines(recentMangaPath)
        |> Seq.toList
        |> List.choose tryFromTitle
        // Is this necessary?
        |> List.truncate 5
    else
        printfn "Could not read %s." recentMangaPath
        []
