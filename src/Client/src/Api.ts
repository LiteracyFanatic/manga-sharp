import { useAsyncFn } from 'react-use';
import useSWR, { SWRConfiguration, SWRResponse, mutate, useSWRConfig } from 'swr';
import useSWRMutation from 'swr/mutation';
import * as z from 'zod';

type UnwrapSWR<T> = T extends SWRResponse<infer Data> ? Data : never;
type UnwrapSWRHook<T> = T extends (...args: infer _A) => infer R ? UnwrapSWR<R> : never;

const directionSchema = z.enum(['Horizontal', 'Vertical']);
export type Direction = z.infer<typeof directionSchema>;

const mangaGetResponseSchema = z.object({
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
    Direction: directionSchema,
    ChapterId: z.string(),
    ChapterTitle: z.string().nullable(),
    PreviousChapterUrl: z.string().nullable(),
    NextChapterUrl: z.string().nullable(),
    OtherChapters: z.object({
        Id: z.string(),
        Title: z.string(),
        Url: z.string(),
        DownloadStatus: downloadStatusSchema
    }).array(),
    DownloadStatus: downloadStatusSchema,
    Pages: z.object({
        Id: z.string(),
        Name: z.string(),
        Width: z.number(),
        Height: z.number()
    }).array()
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
                console.log('optimisticData', currentData);
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
