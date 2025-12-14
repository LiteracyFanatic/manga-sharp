import { useAsyncFn } from 'react-use';
import useSWR, { SWRConfiguration, SWRResponse, mutate, useSWRConfig } from 'swr';
import useSWRMutation from 'swr/mutation';
import * as z from 'zod';

type UnwrapSWR<T> = T extends SWRResponse<infer Data> ? Data : never;
type UnwrapSWRHook<T> = T extends (...args: infer _A) => infer R ? UnwrapSWR<R> : never;

const directionSchema = z.enum(['Horizontal', 'Vertical']);
export type Direction = z.infer<typeof directionSchema>;

export const mangaGetResponseSchema = z.object({
    Id: z.string(),
    Title: z.string(),
    BookmarkUrl: z.string(),
    NumberOfChapters: z.object({
        NotDownloaded: z.number(),
        Downloaded: z.number(),
        Archived: z.number(),
        Ignored: z.number(),
        Total: z.number()
    }),
    ChapterIndex: z.number(),
    Direction: directionSchema,
    SourceUrl: z.string(),
    Updated: z.coerce.date()
});

export type MangaGetResponse = z.infer<typeof mangaGetResponseSchema>;

const downloadStatusSchema = z.enum(['NotDownloaded', 'Downloaded', 'Archived', 'Ignored']);
export type DownloadStatus = z.infer<typeof downloadStatusSchema>;

const chapterGetResponseSchema = z.object({
    MangaId: z.string(),
    MangaTitle: z.string(),
    Direction: directionSchema,
    ChapterId: z.string(),
    ChapterTitle: z.string().nullable(),
    PreviousChapterUrl: z.string().nullable(),
    NextChapterUrl: z.string().nullable(),
    OtherChapters: z.array(z.object({
        Id: z.string(),
        Title: z.string(),
        Url: z.string(),
        DownloadStatus: downloadStatusSchema
    })),
    DownloadStatus: downloadStatusSchema,
    Pages: z.array(z.object({
        Id: z.string(),
        Name: z.string(),
        Width: z.number(),
        Height: z.number()
    }))
});

export type ChapterGetResponse = z.infer<typeof chapterGetResponseSchema>;

export interface PutBookmarkRequest {
    MangaId: string;
    ChapterId: string;
    PageId: string | null;
}

export interface SetMangaDirectionRequest {
    MangaId: string;
    Direction: Direction;
}

export const postMangaRequestSchema = z.object({
    Url: z.string(),
    Direction: directionSchema
});
export type PostMangaRequest = z.infer<typeof postMangaRequestSchema>;

const jobTypeSchema = z.enum(['AddManga', 'UpdateManga']);
export type JobType = z.infer<typeof jobTypeSchema>;

const jobStatusSchema = z.enum(['Pending', 'Downloading', 'Completed', 'Canceled', 'Failed']);
export type JobStatus = z.infer<typeof jobStatusSchema>;

const downloadJobSchema = z.object({
    Id: z.string(),
    Type: jobTypeSchema,
    Status: jobStatusSchema,
    Title: z.string().nullable().optional(),
    Url: z.string().optional(),
    Error: z.string().nullable().optional(),
    ProgressChapterTitle: z.string().nullable().optional(),
    ProgressChapterIndex: z.number().nullable().optional(),
    ProgressTotalChapters: z.number().nullable().optional(),
    ProgressPageIndex: z.number().nullable().optional(),
    ProgressTotalPages: z.number().nullable().optional(),
    CreatedAt: z.string()
});
export type DownloadJob = z.infer<typeof downloadJobSchema>;

export async function deleteManga(mangaId: string) {
    await fetch(`/api/manga/${mangaId}`, { method: 'DELETE' });
}

export async function archiveManga(mangaId: string) {
    await fetch(`/api/manga/${mangaId}/archive`, { method: 'POST' });
}

export async function unarchiveManga(mangaId: string) {
    await fetch(`/api/manga/${mangaId}/unarchive`, { method: 'POST' });
}

export async function setMangaDirection({ MangaId, ...request }: SetMangaDirectionRequest) {
    await fetch(`/api/manga/${MangaId}/direction`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
    });
}

async function getManga() {
    const res = await fetch('/api/manga');
    try {
        return mangaGetResponseSchema.array().parse(await res.json());
    }
    catch (e) {
        console.error(e);
        return [];
    }
}

async function getChapter(chapterId: string) {
    const res = await fetch(`/api/chapters/${chapterId}`);
    return chapterGetResponseSchema.parse(await res.json());
}

export async function setBookmark({ MangaId, ...request }: PutBookmarkRequest) {
    await fetch(`/api/manga/${MangaId}/bookmark`, {
        method: 'PUT',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(request)
    });
}

async function postManga(request: PostMangaRequest) {
    const res = await fetch('/api/manga', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
    });
    if (!res.ok) {
        throw new Error('Failed to add manga');
    }
    const json: unknown = await res.json();
    return z.string().parse(json);
}

async function checkUpdate(mangaId: string) {
    const res = await fetch(`/api/manga/${mangaId}/check-update`, { method: 'POST' });
    if (!res.ok) {
        throw new Error('Failed to check for updates');
    }
    return z.object({ Count: z.number() }).parse(await res.json());
}

async function checkAllUpdates() {
    const res = await fetch('/api/manga/check-updates', { method: 'POST' });
    if (!res.ok) {
        throw new Error('Failed to check for updates');
    }
}

async function clearCompletedDownloads() {
    const res = await fetch('/api/downloads', { method: 'DELETE' });
    if (!res.ok) {
        throw new Error('Failed to clear downloads');
    }
}

async function getDownloads() {
    const res = await fetch('/api/downloads');
    if (!res.ok) {
        throw new Error('Failed to get downloads');
    }
    return downloadJobSchema.array().parse(await res.json());
}

async function moveJobTop(jobId: string) {
    const res = await fetch(`/api/jobs/${jobId}/move-top`, { method: 'POST' });
    if (!res.ok) {
        throw new Error('Failed to move job to top');
    }
}

async function moveJobBottom(jobId: string) {
    const res = await fetch(`/api/jobs/${jobId}/move-bottom`, { method: 'POST' });
    if (!res.ok) {
        throw new Error('Failed to move job to bottom');
    }
}

async function cancelJob(jobId: string) {
    const res = await fetch(`/api/jobs/${jobId}/cancel`, { method: 'POST' });
    if (!res.ok) {
        throw new Error('Failed to cancel job');
    }
}

export function useManga(config?: SWRConfiguration): SWRResponse<MangaGetResponse[], Error> {
    return useSWR(['useManga'], getManga, config);
}

export function useChapter(chapterId: string, config?: SWRConfiguration): SWRResponse<ChapterGetResponse, Error> {
    return useSWR(['useChapter', chapterId], () => getChapter(chapterId), config);
}

export function useDeleteManga() {
    const { mutate } = useSWRConfig();

    const [state, trigger] = useAsyncFn(async (mangaId: string) => {
        await mutate<UnwrapSWRHook<typeof useManga>>(['useManga'], async (current) => {
            await deleteManga(mangaId);
            return (current ?? []).filter(manga => manga.Id !== mangaId);
        }, {
            revalidate: false,
            optimisticData: currentData => (currentData ?? []).filter(manga => manga.Id !== mangaId)
        });
    });

    return { ...state, trigger };
}

export function useArchiveManga() {
    const { mutate } = useSWRConfig();

    const [state, trigger] = useAsyncFn(async (mangaId: string) => {
        await mutate(['useManga'], archiveManga(mangaId));
    });

    return { ...state, trigger };
}

export function useUnarchiveManga() {
    const { mutate } = useSWRConfig();

    const [state, trigger] = useAsyncFn(async (mangaId: string) => {
        await mutate(['useManga'], unarchiveManga(mangaId));
    });

    return { ...state, trigger };
}

export function useSetMangaDirection() {
    const { mutate } = useSWRConfig();

    return useSWRMutation(['useSetMangaDirection'], async (_, { arg }: { arg: SetMangaDirectionRequest }) => {
        await mutate<UnwrapSWRHook<typeof useManga>>(['useManga'], async (currentData) => {
            await setMangaDirection(arg);
            return (currentData ?? []).map((manga) => {
                if (manga.Id === arg.MangaId) {
                    return { ...manga, Direction: arg.Direction };
                }
                return manga;
            });
        }, {
            optimisticData: (currentData) => {
                return (currentData ?? []).map((manga) => {
                    if (manga.Id === arg.MangaId) {
                        return { ...manga, Direction: arg.Direction };
                    }
                    return manga;
                });
            }
        });
    });
}

export function useSetBookmark() {
    return useSWRMutation(['useSetBookmark'], async (_, { arg }: { arg: PutBookmarkRequest }) => {
        await setBookmark(arg);
        await mutate(['useManga'], undefined, { revalidate: true });
    });
}

export function useAddManga() {
    return useSWRMutation(['useAddManga'], async (_, { arg }: { arg: PostMangaRequest }) => {
        const id = await postManga(arg);
        await mutate(['useDownloads']);
        return id;
    });
}

export function useCheckUpdate() {
    return useSWRMutation(['useCheckUpdate'], async (_, { arg }: { arg: string }) => {
        const res = await checkUpdate(arg);
        await mutate(['useDownloads']);
        return res;
    });
}

export function useCheckAllUpdates() {
    return useSWRMutation(['useCheckAllUpdates'], async () => {
        await checkAllUpdates();
        await mutate(['useDownloads']);
    });
}

export function useClearCompletedDownloads() {
    return useSWRMutation(['useClearCompletedDownloads'], async () => {
        await clearCompletedDownloads();
        await mutate(['useDownloads']);
    });
}

export function useDownloads() {
    return useSWR(['useDownloads'], getDownloads, { refreshInterval: 1000 });
}

export function useMoveJobTop() {
    return useSWRMutation(['useMoveJobTop'], async (_, { arg }: { arg: string }) => {
        await moveJobTop(arg);
        await mutate(['useDownloads']);
    });
}

export function useMoveJobBottom() {
    return useSWRMutation(['useMoveJobBottom'], async (_, { arg }: { arg: string }) => {
        await moveJobBottom(arg);
        await mutate(['useDownloads']);
    });
}

export function useCancelJob() {
    return useSWRMutation(['useCancelJob'], async (_, { arg }: { arg: string }) => {
        await cancelJob(arg);
        await mutate(['useDownloads']);
    });
}
