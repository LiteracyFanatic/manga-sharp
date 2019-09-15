module MangaSharp.Page

open MangaSharp
open System.IO

let fromChapterTitle (mangaTitle: string) (chapterTitle: string) =
    Directory.GetFiles(Path.Combine(mangaData, mangaTitle, chapterTitle))
    |> Array.map (fun f ->
        {
            Name = Path.GetFileNameWithoutExtension(FileInfo(f).Name)
            File = f
        }
    )
    |> Array.toList

let fromTitle (mangaTitle: string) (chapterTitle: string) (pageTitle: string) =
    fromChapterTitle mangaTitle chapterTitle
    |> List.find (fun p -> p.Name = pageTitle)
