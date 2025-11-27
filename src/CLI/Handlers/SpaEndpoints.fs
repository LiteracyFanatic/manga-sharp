namespace MangaSharp.CLI.Server

open Microsoft.Extensions.FileProviders
open Giraffe
open Giraffe.EndpointRouting

module SpaEndpoints =

    let private serveSpa : HttpHandler =
        fun next ctx ->
            task {
                let provider = ctx.GetService<IFileProvider>()
                use stream = provider.GetFileInfo("index.html").CreateReadStream()
                return! streamData true stream None None next ctx
            }

    let routes : Endpoint list =
        [
            GET [ route "{*rest}" serveSpa ]
        ]
