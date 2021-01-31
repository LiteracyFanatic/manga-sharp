module MangaSharp.Chapter

open System.IO

let fromDir (dir: string) =
    let title = DirectoryInfo(dir).Name
    let pages = Page.fromDir dir
    { Title = title; Pages = pages }

let fromTitle (mangaTitle: string) (chapterTitle: string) =
    fromDir (Path.Combine(mangaData, mangaTitle, chapterTitle))

let tryPreviousPage (chapter: Chapter) (page: Page) =
    let i = List.findIndex ((=) page) chapter.Pages
    List.tryItem (i - 1) chapter.Pages

let tryNextPage (chapter: Chapter) (page: Page) =
    let i = List.findIndex ((=) page) chapter.Pages
    List.tryItem (i + 1) chapter.Pages
