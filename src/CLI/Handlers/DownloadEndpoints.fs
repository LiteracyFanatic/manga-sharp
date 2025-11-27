namespace MangaSharp.CLI.Server

open System
open Giraffe
open Giraffe.EndpointRouting
open MangaSharp.CLI

module DownloadEndpoints =

    let private getDownloadsHandler : HttpHandler =
        fun next ctx ->
            task {
                let manager = ctx.GetService<DownloadManager>()
                let! jobs = manager.GetJobs()
                return! Successful.OK jobs next ctx
            }

    let private clearCompletedDownloadsHandler : HttpHandler =
        fun next ctx ->
            task {
                let manager = ctx.GetService<DownloadManager>()
                do! manager.ClearCompleted()
                return! Successful.NO_CONTENT next ctx
            }

    let private moveJobTopHandler (jobId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let manager = ctx.GetService<DownloadManager>()
                do! manager.MoveToTop(jobId)
                return! Successful.NO_CONTENT next ctx
            }

    let private moveJobBottomHandler (jobId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let manager = ctx.GetService<DownloadManager>()
                do! manager.MoveToBottom(jobId)
                return! Successful.NO_CONTENT next ctx
            }

    let private cancelJobHandler (jobId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let manager = ctx.GetService<DownloadManager>()
                do! manager.CancelJob(jobId)
                return! Successful.NO_CONTENT next ctx
            }

    let routes : Endpoint list =
        [
            GET [ route "/downloads" getDownloadsHandler ]
            POST [
                routef "/jobs/%O/move-top" moveJobTopHandler
                routef "/jobs/%O/move-bottom" moveJobBottomHandler
                routef "/jobs/%O/cancel" cancelJobHandler
            ]
            DELETE [ route "/downloads" clearCompletedDownloadsHandler ]
        ]
