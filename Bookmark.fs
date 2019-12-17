module MangaSharp.Bookmark

open MangaSharp
open MangaSharp.Util
open System.IO
open System.Web
open Giraffe.ComputationExpressions

let private tryParse (mangaTitle: string) (bookmark: string) =
    match bookmark.Split("/") with
    | [|c; p|] ->
        let chapter = Chapter.tryFromTitle mangaTitle c
        let page = Page.tryFromTitle mangaTitle c p
        match chapter, page with
        | Some chapter, Some page ->
            Some (HorizontalBookmark (chapter, page))
        | _ ->
            printfn "The bookmarked location does not exist."
            None
    | [|c|] ->
        let chapter = Chapter.tryFromTitle mangaTitle c
        match chapter with
        | Some chapter ->
            Some (VerticalBookmark chapter)
        | None ->
            printfn "The bookmarked location does not exist."
            None
    | _ ->
        printfn "%s is not a valid bookmark." bookmark
        None

let tryReadBookmark (mangaTitle: string) =
    opt {
        let bookmarkPath = Path.Combine(mangaData, mangaTitle, "bookmark")
        if File.Exists(bookmarkPath) then
            let! bookmark = File.tryReadAllText bookmarkPath
            match tryParse mangaTitle bookmark with
            | Some bookmark -> return bookmark
            | None -> printfn "Error parsing %s." bookmarkPath
    }

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
        sprintf "/manga/%s/%s#%s" (HttpUtility.UrlEncode mangaTitle) chapter.Title page.Name
    | VerticalBookmark chapter ->
        sprintf "/manga/%s/%s" (HttpUtility.UrlEncode mangaTitle) chapter.Title
