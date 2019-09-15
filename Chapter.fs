module MangaSharp.Chapter

open MangaSharp
open System.IO

let fromDir (dir: string) =
    {
        Title = DirectoryInfo(dir).Name
        Pages = 
            Directory.GetFiles(dir)
            |> Array.map (fun f -> FileInfo(f).Name)
            |> Array.sort
            |> Array.map (fun f -> 
                {
                    Name = Path.GetFileNameWithoutExtension(f)
                    File = f
                }
            )
            |> Array.toList
    }

let fromTitle (mangaTitle: string) (chapterTitle: string) =
    fromDir (Path.Combine(mangaData, mangaTitle, chapterTitle))

let tryPreviousPage (chapter: Chapter) (page: Page) =
    let i = List.findIndex ((=) page) chapter.Pages
    List.tryItem (i - 1) chapter.Pages

let tryNextPage (chapter: Chapter) (page: Page) =
    let i = List.findIndex ((=) page) chapter.Pages
    List.tryItem (i + 1) chapter.Pages
