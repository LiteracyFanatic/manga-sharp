namespace MangaSharp.Extractors

open System
open System.Threading.Tasks
open MangaSharp.Database.MangaDomain

type IMangaExtractor =
    abstract IsMatch: url: string -> bool
    abstract DownloadAsync: url: string * direction: Direction -> Task<Result<unit, string>>
    abstract UpdateAsync: mangaId: Guid -> Task<Result<bool, string>>
