namespace MangaSharp.CLI

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open MangaSharp.Database
open MangaSharp.Extractors

type DownloadManager(logger: ILogger<DownloadManager>, jobRepository: DownloadJobRepository) =
    let activeJobTokens : ConcurrentDictionary<Guid, CancellationTokenSource> = ConcurrentDictionary()

    member _.QueueDownload(url: string, direction: Direction) =
        task {
            let! job = jobRepository.EnqueueAddMangaAsync(url, direction)
            return job.Id
        }

    member _.QueueUpdate(mangaId: Guid, title: string, url: string) =
        task {
            let! job = jobRepository.EnqueueUpdateMangaAsync(mangaId, title, url)
            return job.Id
        }

    member _.GetJobs() = jobRepository.GetJobsAsync()

    member _.GetPendingJobs(limit) = jobRepository.GetPendingJobsAsync(limit)

    member _.UpdateStatus(id: Guid, status: JobStatus, error: string option) =
        jobRepository.UpdateStatusAsync(id, status, error |> Option.toObj)

    member _.UpdateProgress(id: Guid, progress: ExtractorProgress) =
        task {
            let pageIndex = progress.PageIndex |> Option.toNullable
            let totalPages = progress.TotalPages |> Option.toNullable
            do! jobRepository.UpdateProgressAsync(id, progress.ChapterTitle, Nullable progress.ChapterIndex, Nullable progress.TotalChapters, pageIndex, totalPages)
        }

    member _.ClearCompleted() = jobRepository.ClearCompletedAsync()

    member _.MoveToTop(id) = jobRepository.MoveToTopAsync(id)

    member _.MoveToBottom(id) = jobRepository.MoveToBottomAsync(id)

    member _.ResetStuckJobs() = jobRepository.ResetStuckJobsAsync()

    member _.RegisterActiveJob(id, cts) = activeJobTokens.TryAdd(id, cts) |> ignore

    member _.UnregisterActiveJob(id) = 
        let mutable cts = Unchecked.defaultof<CancellationTokenSource>
        activeJobTokens.TryRemove(id, &cts) |> ignore

    member _.CancelJob(id) =
        task {
            match activeJobTokens.TryGetValue(id) with
            | true, cts ->
                cts.Cancel()
            | _ ->
                do! jobRepository.UpdateStatusAsync(id, JobStatus.Canceled, null)
        }

type DownloadWorker(scopeFactory: IServiceScopeFactory, logger: ILogger<DownloadWorker>) =
    inherit BackgroundService()

    let globalLimit = 5
    let globalSemaphore = new SemaphoreSlim(globalLimit)
    let activeExtractors = ConcurrentDictionary<string, int>()
    let activeMangaIds = ConcurrentDictionary<Guid, byte>()
    let activeUrls = ConcurrentDictionary<string, byte>()

    let extractorLimits =
        dict [
            "MangaDex", 3
            "Manganato", 1
            "Manhwa18", 1
            "Manhwa18CC", 1
            "ManyToon", 1
            "WebToon", 2
        ]

    member private _.ResetStuckJobs() =
        task {
            try
                use scope = scopeFactory.CreateScope()
                let manager = scope.ServiceProvider.GetRequiredService<DownloadManager>()
                do! manager.ResetStuckJobs()
            with ex ->
                logger.LogError(ex, "Failed to reset stuck jobs")
        }

    member private _.GetExtractorsAtCapacity() =
        activeExtractors
        |> Seq.filter (fun kvp ->
            let limit =
                match extractorLimits.TryGetValue(kvp.Key) with
                | true, v -> v
                | false, _ -> 1
            kvp.Value >= limit)
        |> Seq.map (fun kvp -> kvp.Key)
        |> Set.ofSeq

    member private _.IsJobRunnable(job: DownloadJob, downloader: MangaDownloaderService, extractorsAtCapacity: Set<string>) =
        let isExtractorAtCapacity =
            match downloader.GetExtractorName(job.Url) with
            | Some name -> extractorsAtCapacity.Contains(name)
            | None -> false

        // Prevent multiple jobs for the same manga to avoid file locking issues
        let isBlockedManga =
            job.MangaId.HasValue && activeMangaIds.ContainsKey(job.MangaId.Value)

        // Prevent multiple jobs for the same URL (redundancy check)
        let isBlockedUrl =
            activeUrls.ContainsKey(job.Url)

        not isExtractorAtCapacity && not isBlockedManga && not isBlockedUrl

    member private _.ProcessJob(job: DownloadJob, extractorName: string, stoppingToken: CancellationToken) =
        task {
            // Create a new scope for the execution of this specific job.
            // This is crucial because DbContext is not thread-safe and we are running in parallel.
            use scope = scopeFactory.CreateScope()
            let manager = scope.ServiceProvider.GetRequiredService<DownloadManager>()
            let downloader = scope.ServiceProvider.GetRequiredService<MangaDownloaderService>()
            let logger = scope.ServiceProvider.GetRequiredService<ILogger<DownloadWorker>>()

            use _ = logger.BeginScope(dict ["JobId", box job.Id])

            try
                try
                    logger.LogInformation("Processing job {Id}: {Url}", job.Id, job.Url)
                    do! manager.UpdateStatus(job.Id, JobStatus.Downloading, None)

                    // Create a separate scope for progress updates.
                    // Progress.Report runs on a thread pool thread and could conflict with
                    // the main download thread if they shared the same DbContext.
                    let progress = Progress<ExtractorProgress>(fun p ->
                        try
                            use progressScope = scopeFactory.CreateScope()
                            let progressManager = progressScope.ServiceProvider.GetRequiredService<DownloadManager>()
                            progressManager.UpdateProgress(job.Id, p).GetAwaiter().GetResult()
                        with ex ->
                            logger.LogError(ex, "Error updating progress for job {Id}", job.Id))

                    let! result =
                        match job.Type with
                        | JobType.AddManga -> downloader.Download(job.Url, job.Direction.GetValueOrDefault(Direction.Vertical), progress)
                        | JobType.UpdateManga ->
                            let mangaId = job.MangaId.GetValueOrDefault()
                            if mangaId = Guid.Empty then
                                task { return Error "Missing MangaId" }
                            else
                                downloader.Update(mangaId, progress)
                        | _ -> task { return Error $"Unknown job type: {job.Type}" }

                    match result with
                    | Ok _ ->
                        logger.LogInformation("Job {Id} completed", job.Id)
                        do! manager.UpdateStatus(job.Id, JobStatus.Completed, None)
                    | Error e ->
                        logger.LogError("Job {Id} failed: {Error}", job.Id, e)
                        do! manager.UpdateStatus(job.Id, JobStatus.Failed, Some e)

                with
                | :? OperationCanceledException ->
                    logger.LogWarning("Job {Id} was canceled", job.Id)
                    do! manager.UpdateStatus(job.Id, JobStatus.Canceled, None)
                | ex ->
                    logger.LogError(ex, "Job {Id} failed with exception", job.Id)
                    do! manager.UpdateStatus(job.Id, JobStatus.Failed, Some ex.Message)
            finally
                // Cleanup active tracking
                activeExtractors.AddOrUpdate(extractorName, 0, (fun _ c -> c - 1)) |> ignore
                if job.MangaId.HasValue then
                    activeMangaIds.TryRemove(job.MangaId.Value) |> ignore
                activeUrls.TryRemove(job.Url) |> ignore

                manager.UnregisterActiveJob(job.Id)
                globalSemaphore.Release() |> ignore
        }

    override this.ExecuteAsync(stoppingToken: CancellationToken) = task {
        logger.LogInformation("DownloadWorker started")
        do! this.ResetStuckJobs()

        while not stoppingToken.IsCancellationRequested do
            try
                // Wait for a slot in the global concurrency limit
                do! globalSemaphore.WaitAsync(stoppingToken)

                use scope = scopeFactory.CreateScope()
                let manager = scope.ServiceProvider.GetRequiredService<DownloadManager>()
                let downloader = scope.ServiceProvider.GetRequiredService<MangaDownloaderService>()

                let extractorsAtCapacity = this.GetExtractorsAtCapacity()
                let! pendingJobs = manager.GetPendingJobs(20)

                let runnableJob =
                    pendingJobs
                    |> Seq.tryFind (fun job -> this.IsJobRunnable(job, downloader, extractorsAtCapacity))

                match runnableJob with
                | Some job ->
                    let extractorName = downloader.GetExtractorName(job.Url) |> Option.defaultValue "Unknown"

                    // Mark job as active to prevent duplicates and enforce limits
                    activeExtractors.AddOrUpdate(extractorName, 1, (fun _ c -> c + 1)) |> ignore
                    if job.MangaId.HasValue then
                        activeMangaIds.TryAdd(job.MangaId.Value, 0uy) |> ignore
                    activeUrls.TryAdd(job.Url, 0uy) |> ignore

                    let cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken)
                    manager.RegisterActiveJob(job.Id, cts)

                    // Fire and forget the job processing (it manages its own semaphore release)
                    let _ = Task.Run(fun () -> this.ProcessJob(job, extractorName, cts.Token) :> Task)
                    ()

                | None ->
                    // No job found, release the slot and wait before retrying
                    globalSemaphore.Release() |> ignore
                    do! Task.Delay(1000, stoppingToken)

            with ex ->
                logger.LogError(ex, "Error in DownloadWorker loop")
                do! Task.Delay(5000, stoppingToken)
    }

