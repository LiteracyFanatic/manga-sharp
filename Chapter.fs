module MangaSharp.Chapter

open MangaSharp
open MangaSharp.Util
open System.IO

let fromDir (dir: string) =
    let title = DirectoryInfo(dir).Name
    let pages = Page.fromDir dir
    { Title = title; Pages = pages }

let fromTitle (mangaTitle: string) (chapterTitle: string) =
    fromDir (Path.Combine(mangaData, mangaTitle, chapterTitle))

let tryPreviousPage (chapter: Chapter) (page: Page) =
    let i = NonEmptyList.findIndex ((=) page) chapter.Pages
    NonEmptyList.tryItem (i - 1) chapter.Pages

let tryNextPage (chapter: Chapter) (page: Page) =
    let i = NonEmptyList.findIndex ((=) page) chapter.Pages
    NonEmptyList.tryItem (i + 1) chapter.Pages
