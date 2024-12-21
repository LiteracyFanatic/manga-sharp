namespace MangaSharp.Extractors

open System
open FsToolkit.ErrorHandling
open MangaSharp.Database
open MangaSharp.Extractors.Util

type IMangaExtractor =
    abstract IsMatch: url: string -> bool
    abstract DownloadAsync: url: string * direction: Direction -> TaskResult<unit, IMangaSharpError>
    abstract UpdateAsync: mangaId: Guid -> TaskResult<bool, IMangaSharpError>
