module MangaSharp.Bookmark

open MangaSharp
open System.IO
open System.Web

let private parse (bookmark: string) =
    match bookmark.Split("/") with
    | [|c; p|] -> HorizontalBookmark (c, p)
    | [|c|] -> VerticalBookmark c
    | _ -> failwithf "%s is not a valid bookmark." bookmark

let tryReadBookmark (mangaTitle: string) =
    let bookmarkPath = Path.Combine(mangaData, mangaTitle, "bookmark")
    if File.Exists(bookmarkPath) then
        let bookmark = File.ReadAllText(bookmarkPath).Trim()
        Some (parse bookmark)
    else
        None

let getChapter (bookmark: Bookmark) =
    match bookmark with
    | HorizontalBookmark (chapter, _) -> chapter
    | VerticalBookmark chapter -> chapter

let getChapterIndex (mangaTitle: string) (bookmark: Bookmark) =
    let chapter = getChapter bookmark
    ChapterStatus.get mangaTitle
    |> List.filter (fun c -> c.DownloadStatus = Downloaded)
    |> List.findIndex (fun c -> c.Title.Value = chapter)

let tryGetPage (bookmark: Bookmark) =
    match bookmark with
    | HorizontalBookmark (_, page) -> Some page
    | VerticalBookmark _ -> None

let toUrl (mangaTitle: string) (bookmark: Bookmark) =
    match bookmark with
    | HorizontalBookmark (chapter, page) ->
        sprintf "/manga/%s/%s#%s" (HttpUtility.UrlEncode mangaTitle) chapter page
    | VerticalBookmark chapter ->
        sprintf "/manga/%s/%s" (HttpUtility.UrlEncode mangaTitle) chapter
