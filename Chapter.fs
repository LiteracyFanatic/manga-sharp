module MangaSharp.Chapter

open MangaSharp
open MangaSharp.Util
open System.IO

let tryFromDir (dir: string) =
    let title = DirectoryInfo(dir).Name
    match Page.fromDir dir with
    | Some pages ->
        Some { Title = title; Pages = pages }
    | None ->
        None

let tryFromTitle (mangaTitle: string) (chapterTitle: string) =
    tryFromDir (Path.Combine(mangaData, mangaTitle, chapterTitle))

let tryPreviousPage (chapter: Chapter) (page: Page) =
    let i = NonEmptyList.findIndex ((=) page) chapter.Pages
    NonEmptyList.tryItem (i - 1) chapter.Pages

let tryNextPage (chapter: Chapter) (page: Page) =
    let i = NonEmptyList.findIndex ((=) page) chapter.Pages
    NonEmptyList.tryItem (i + 1) chapter.Pages
