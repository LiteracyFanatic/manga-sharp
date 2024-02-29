namespace MangaSharp.Extractors.MangaDex

open System
open System.Net.Http
open System.Net.Http.Headers
open System.Net.Http.Json
open FsToolkit.ErrorHandling
open MangaSharp

[<CLIMutable>]
type MangaDexAtHomeResponse = {
    baseUrl: string
    chapter: {|
        hash: string
        data: string[]
    |}
}

[<CLIMutable>]
type MangaDexMangaResponse = {
    data: {|
        attributes:
            {|
                title:
                    {|
                        en: string option
                        ja: string option
                    |}
                altTitles:
                    {|
                        en: string option
                        ja: string option
                    |}[]
                chapterNumbersResetOnNewVolume: bool
            |}
    |}
}

[<CLIMutable>]
type MangaDexChapterResponse = {
    data:
        {|
            id: string
            attributes:
                {|
                    translatedLanguage: string
                    publishAt: DateTime
                    volume: string option
                    chapter: string option
                    title: string option
                |}
        |}[]
}

[<CLIMutable>]
type MangaDexHealthReportRequest = {
    url: string
    success: bool
    cached: bool
    bytes: int64
    duration: int64
}

type MangaDexApi(httpFactory: IHttpClientFactory, versionInfo: VersionInfo) =

    let hc = httpFactory.CreateClient()
    do hc.Timeout <- TimeSpan.FromSeconds(20.)

    let userAgent = ProductInfoHeaderValue("MangaSharp", versionInfo.Version)

    do hc.DefaultRequestHeaders.UserAgent.Add(userAgent)

    member this.GetAtHomeAsync(chapterId: string) =
        let apiUrl = $"https://api.mangadex.org/at-home/server/%s{chapterId}"
        hc.GetFromJsonAsync<MangaDexAtHomeResponse>(apiUrl)

    member this.GetMangaAsync(mangaId: string) =
        let apiUrl = $"https://api.mangadex.org/manga/%s{mangaId}"
        hc.GetFromJsonAsync<MangaDexMangaResponse>(apiUrl)

    member this.GetChaptersAsync(mangaId: string) =
        let rec loop offset acc =
            taskResult {
                let apiUrl =
                    $"https://api.mangadex.org/chapter?manga=%s{mangaId}&translatedLanguage%%5b%%5d=en&includeFutureUpdates=0&limit=100&offset=%i{offset}&order%%5bchapter%%5d=asc&contentRating%%5b%%5d=safe&contentRating%%5b%%5d=suggestive&contentRating%%5b%%5d=erotica&contentRating%%5b%%5d=pornographic"

                let! res = hc.GetFromJsonAsync<MangaDexChapterResponse>(apiUrl)

                if Seq.isEmpty res.data then
                    return acc
                else
                    return! loop (offset + 100) (Seq.append acc res.data)
            }

        loop 0 Seq.empty

    member this.PostHealthReportAsync(request: MangaDexHealthReportRequest) =
        task {
            let apiUrl = "https://api.mangadex.network/report"
            use! res = hc.PostAsJsonAsync(apiUrl, request)
            return ()
        }
