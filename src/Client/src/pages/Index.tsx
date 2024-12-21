import { CircularProgress, Typography } from "@mui/material";
import { Container } from "@mui/system";
import fuzzysort from "fuzzysort";

import { MangaGetResponse, useManga } from "../Api";
import IndexAppBar, { SortByKey, SortDirection } from "../components/IndexAppBar";
import MangaList from "./MangaList";
import { StringParam, useQueryParam, createEnumParam, withDefault, QueryParamConfig } from "use-query-params";

function compare(sortBy: SortByKey, a: MangaGetResponse, b: MangaGetResponse) {
    switch (sortBy) {
        case "Title":
            return a.Title.localeCompare(b.Title);
        case "Updated":
            return a.Updated.getTime() - b.Updated.getTime();
        default:
            throw new Error("Invalid sort by value.");
    }
}

const SortByKeyParam = withDefault(createEnumParam<SortByKey>(["Title", "Updated"]), "Title") as QueryParamConfig<SortByKey>;

const SortDirectionParam = withDefault(createEnumParam<SortDirection>(["asc", "desc"]), "asc") as QueryParamConfig<SortDirection>;

export default function Index() {
    const manga = useManga();
    const [search, setSearch] = useQueryParam("search", StringParam);
    const [sortBy, setSortBy] = useQueryParam("sort_by", SortByKeyParam);
    const [sortDirection, setSortDirection] = useQueryParam("sort_direction", SortDirectionParam);

    function getContent() {
        if (manga.data) {
            const filteredManga = fuzzysort.go(search || "", manga.data, {
                all: true,
                keys: ["Title"],
                threshold: 0.5
            });
            const sortDirectionMultiplier = sortDirection === "asc" ? 1 : -1;
            const sortedManga = filteredManga.toSorted((a, b) => sortDirectionMultiplier * compare(sortBy, a.obj, b.obj));
            return (
                <MangaList
                    manga={sortedManga}
                    sx={{
                        width: "100%"
                    }}
                />
            );
        } else if (manga.isValidating) {
            return <CircularProgress />;
        } else {
            return <Typography>Manga not found.</Typography>;
        }
    }

    return (
        <>
            <IndexAppBar
                defaultSearchValue={search || ""}
                onSearchValueChange={v => setSearch(v || null)}
                defaultSortBy={{ key: sortBy, direction: sortDirection }}
                onSortByChange={v => {
                    setSortBy(v.key);
                    setSortDirection(v.direction);
                }}
            />
            <Container
                sx={{
                    display: "flex",
                    justifyContent: "center",
                    marginY: 2
                }}
                maxWidth="md"
            >
                {getContent()}
            </Container>
        </>
    );
}
