module MangaSharp.Chapter

open MangaSharp
open MangaSharp.Util
open System.IO
open Giraffe.ComputationExpressions

let tryFromDir (dir: string) =
    opt {
        let title = DirectoryInfo(dir).Name
        let! pages = Page.fromDir dir
        return { Title = title; Pages = pages }
    }

let tryFromTitle (mangaTitle: string) (chapterTitle: string) =
    tryFromDir (Path.Combine(mangaData, mangaTitle, chapterTitle))

let tryPreviousPage (chapter: Chapter) (page: Page) =
    let i = NonEmptyList.findIndex ((=) page) chapter.Pages
    NonEmptyList.tryItem (i - 1) chapter.Pages

let tryNextPage (chapter: Chapter) (page: Page) =
    let i = NonEmptyList.findIndex ((=) page) chapter.Pages
    NonEmptyList.tryItem (i + 1) chapter.Pages
