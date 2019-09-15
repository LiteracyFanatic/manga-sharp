module MangaSharp.Manga

open System
open System.IO
open System.Net
open FSharp.Data
open ImageMagick
open MangaSharp

let dataHome =
    Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData,
        Environment.SpecialFolderOption.Create
    )
let mangaData = Path.Combine(dataHome, "manga")
Directory.CreateDirectory(mangaData) |> ignore

let normalizeWidth (images: MagickImage list) =
    let minWidth =
        images
        |> List.map (fun image -> image.Width)
        |> List.min
    images
    |> List.filter (fun image -> image.Width > minWidth)
    |> List.iter (fun image -> image.Resize(minWidth, 0))
    images

let split (targetHeight: int) (image: MagickImage) =
    let top = new MagickImage(image.Clone())
    top.Crop(top.Width, targetHeight)
    top.RePage()
    let bottom = new MagickImage(image.Clone())
    bottom.Crop(MagickGeometry(0, targetHeight, bottom.Width, bottom.Height - targetHeight))
    bottom.RePage()
    top, bottom

let append (top: MagickImage) (bottom: MagickImage) =
    use parts = new MagickImageCollection()
    parts.Add(top)
    parts.Add(bottom)
    new MagickImage(parts.AppendVertically())
    
let extend (targetHeight) (image: MagickImage) =
    let newImage = new MagickImage(image.Clone())
    newImage.Extent(image.Width, targetHeight, Gravity.North, MagickColors.Black)
    newImage

let rec normalizeHeight (targetHeight: int) (images: MagickImage list) =
    match images with
    | [] -> []
    | [h] ->
        if h.Height = targetHeight then
            [h]
        else if h.Height < targetHeight then
            [extend targetHeight h]
        else
            let top, bottom = split targetHeight h
            top :: normalizeHeight targetHeight [bottom]
    | h :: t ->
        if h.Height = targetHeight then
            h :: normalizeHeight targetHeight t
        else if h.Height < targetHeight then
            normalizeHeight targetHeight (append h t.Head :: t.Tail)
        else
            let top, bottom = split targetHeight h
            top :: normalizeHeight targetHeight (bottom :: t)

let resizeImages images =
    printfn "Resizing images..."
    images
    |> normalizeWidth
    |> normalizeHeight 5000

let toPdf (path: string) (images: MagickImage list) =
    printfn "Converting to PDF..."
    use imageCollection = new MagickImageCollection()
    for image in images do imageCollection.Add(image)
    imageCollection.Write(path)
    for image in images do image.Dispose()

let downloadImage (dir: string) (url: string) =
    let name = Path.GetFileName(Uri(url).LocalPath)
    use wc = new WebClient()
    wc.DownloadFile(url, Path.Combine(dir, name))

let downloadChapter (dir: string) (title: string) (manga: MangaSource) (chapterCount: int) (n: int) (url: string) =
    let html = HtmlDocument.Load(url)
    let chapterTitle = manga.Provider.ChapterTitleExtractor html
    printfn "Downloading %s Chapter %s (%i/%i)..." title chapterTitle n chapterCount
    match manga.Direction with
    | Horizontal ->
        let folder = Path.Combine(dir, chapterTitle)
        Directory.CreateDirectory(folder) |> ignore
        html
        |> manga.Provider.ImageExtractor
        |> Seq.iter (downloadImage folder)
    | Vertical ->
        html
        |> manga.Provider.ImageExtractor
        |> Seq.map (fun url ->
            let stream = Http.RequestStream(url).ResponseStream
            let image = new MagickImage(stream)
            image
        )
        |> Seq.toList
        |> resizeImages
        |> toPdf (Path.Combine(dir, sprintf "%s.pdf" chapterTitle))

let download (manga: MangaSource): unit =
    let index = HtmlDocument.Load(manga.Url)
    let title = manga.Provider.TitleExtractor index
    let chapterUrls = manga.Provider.ChapterUrlsExtractor index
    let dir = Path.Combine(mangaData, title)
    Directory.CreateDirectory(dir) |> ignore
    File.WriteAllText(Path.Combine(dir, "direction"), manga.Direction.ToString())
    File.WriteAllText(Path.Combine(dir, "source"), manga.Url)
    let existingChapters =
        if File.Exists(Path.Combine(dir, "chapters")) then
            File.ReadAllLines (Path.Combine(dir, "chapters"))
            |> Set.ofArray
        else
            Set.empty
    if not (Set.isSubset existingChapters (Set.ofSeq chapterUrls)) then
        printfn "WARNING: There are previously downloaded chapters from URLs not listed by the current source. This may indicate a change in page structure."
    let newChapters = Set.difference (Set.ofSeq chapterUrls) existingChapters
    if Set.isEmpty newChapters then
        printfn "No new chapters."
    else
        chapterUrls
        |> Seq.filter newChapters.Contains
        |> Seq.iteri (fun i u ->
            downloadChapter dir title manga newChapters.Count (i + 1) u
            File.AppendAllText(Path.Combine(dir, "chapters"), sprintf "%s\n" u)
        )
        printfn "Finished downloading %s." title

let storedManga =
    Directory.EnumerateDirectories(mangaData)
    |> Seq.map (fun d -> 
        let indexUrl = File.ReadAllText(Path.Combine(d, "source"))
        let direction =
            match File.ReadAllText(Path.Combine(d, "direction")) with
            | "horizontal" -> Horizontal
            | "vertical" -> Vertical
        let bookmarkPath = Path.Combine(d, "bookmark")
        let bookmark =
            if File.Exists(bookmarkPath) then
                File.ReadAllText(bookmarkPath) |> Some
            else
                None
        let source = {
            Url = indexUrl
            Direction = direction
            Provider = Provider.tryFromTable indexUrl |> Option.get
        }
        {
            Title = Path.GetFileName(d)
            NumberOfChapters =
                match direction with
                | Horizontal ->
                    Directory.GetDirectories(d).Length
                | Vertical ->
                    Directory.GetFiles(d)
                    |> Array.filter(fun f -> FileInfo(f).Extension = "pdf")
                    |> Array.length
            Bookmark = bookmark                
            Source = source
        }
    )
    |> Seq.sortBy (fun m -> m.Title)
