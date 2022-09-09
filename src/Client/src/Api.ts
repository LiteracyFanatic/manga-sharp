import axios, { AxiosError } from "axios";
import useSWR, { SWRConfiguration, SWRResponse } from "swr";

export type Direction =
    | "Horizontal"
    | "Vertical"

export interface MangaGetResponse {
    Id: string
    Title: string
    BookmarkUrl: string
    NumberOfChapters: number
    ChapterIndex: number
    Direction: Direction
    SourceUrl: string
}

async function getManga() {
    const res = await axios.get<MangaGetResponse[]>("/api/manga");
    return res.data;
}

export function useManga(config?: SWRConfiguration): SWRResponse<MangaGetResponse[], AxiosError> {
    return useSWR(["useManga"], getManga, config);
}

type DownloadStatus =
    | "NotDownloaded"
    | "Downloaded"
    | "Archived"
    | "Ignored"

export interface ChapterGetResponse {
    MangaId: string
    Direction: Direction
    ChapterId: string
    ChapterTitle: string | null
    PreviousChapterUrl: string | null
    NextChapterUrl: string | null
    OtherChapters: {
        Id: string
        Title: string
        Url: string
        DownloadStatus: DownloadStatus
    }[]
    DownloadStatus: DownloadStatus
    Pages: {
        Id: string
        Name: string
    }[]
}

async function getChapter(chapterId: string) {
    const res = await axios.get<ChapterGetResponse>(`/api/chapters/${chapterId}`);
    return res.data;
}

export function useChapter(chapterId: string, config?: SWRConfiguration): SWRResponse<ChapterGetResponse, AxiosError> {
    return useSWR(["useChapter", chapterId], () => getChapter(chapterId), config);
}

export interface PutBookmarkRequest {
    ChapterId: string
    PageId: string | null
}

export async function setBookmark(mangaId: string, request: PutBookmarkRequest) {
    const res = await axios.put(`/api/manga/${mangaId}/bookmark`, request);
    return res.data;
}
