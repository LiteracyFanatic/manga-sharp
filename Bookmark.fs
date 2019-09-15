module MangaSharp.Bookmark

open MangaSharp
open System.IO

let tryReadBookmark (mangaTitle: string) =
    let bookmarkPath = Path.Combine(mangaData, mangaTitle, "bookmark")
    if File.Exists(bookmarkPath) then
        let bookmark = File.ReadAllText(bookmarkPath).Trim()
        match bookmark.Split("/") with
        | [|c; p|] ->
            let chapter = Chapter.fromTitle mangaTitle c
            let page = Page.fromTitle mangaTitle c p
            Some (HorizontalBookmark (chapter, page))
        | [|c|] ->
            let chapter = Chapter.fromTitle mangaTitle c
            Some (VerticalBookmark chapter)
        | _ -> 
            failwithf "The contents of %s is not a valid bookmark." bookmarkPath
    else
        None

let getChapter (bookmark: Bookmark) =
    match bookmark with
    | HorizontalBookmark (chapter, _) -> chapter
    | VerticalBookmark chapter -> chapter

let tryGetPage (bookmark: Bookmark) =
    match bookmark with
    | HorizontalBookmark (_, page) -> Some page
    | VerticalBookmark _ -> None

let toUrl (mangaTitle: string) (bookmark: Bookmark) =
    match bookmark with
    | HorizontalBookmark (chapter, page) ->
        sprintf "/manga/%s/%s#%s" mangaTitle chapter.Title page.Name
    | VerticalBookmark chapter ->
        sprintf "/manga/%s/%s" mangaTitle chapter.Title
