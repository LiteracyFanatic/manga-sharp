module MangaSharp.ChapterStatus

open System.IO
open Newtonsoft.Json
open Microsoft.FSharpLu.Json

let private opts = JsonSerializerSettings()
opts.Formatting <- Formatting.Indented
opts.Converters.Add(CompactUnionJsonConverter())

let get (mangaTitle: string) =
    let chaptersPath = Path.Combine(mangaData, mangaTitle, "chapters")
    if File.Exists(chaptersPath) then
        let json = File.ReadAllText(chaptersPath)
        JsonConvert.DeserializeObject<ChapterStatus list>(json, opts)
    else
        []

let merge (mangaInfo: MangaInfo) (urls: string list) (chapters: ChapterStatus list) =
    let existingChaptersSet = set (List.map (fun c -> c.Url) chapters)
    let chapterUrlsSet = set urls
    if not (existingChaptersSet.IsSubsetOf chapterUrlsSet) then
        printfn "WARNING: There are previously downloaded chapters for %s from URLs not listed by the current source. This may indicate a change in page structure."
            mangaInfo.Title
    urls
    |> List.map (fun url ->
        let title, downloadStatus =
            match List.tryFind (fun chapter -> chapter.Url = url) chapters with
            | Some chapter -> chapter.Title, chapter.DownloadStatus
            | None -> None, NotDownloaded
        {
            Url = url
            Title = title
            DownloadStatus = downloadStatus
        }
    )

let save (path: string) (chapters: ChapterStatus list) =
    File.WriteAllText(path, JsonConvert.SerializeObject(chapters, opts))
