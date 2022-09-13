import { CircularProgress, Typography } from "@mui/material";
import { Container } from "@mui/system";

import { useManga } from "../Api";
import IndexAppBar from "../components/IndexAppBar";
import { useSearchParam } from "../hooks/useSearchParam";
import MangaList from "./MangaList";

export default function Index() {
    const manga = useManga();
    const [search, setSearch] = useSearchParam("search");

    function getContent() {
        if (manga.data) {
            return (
                <MangaList
                    manga={manga.data.filter(manga => search ? manga.Title.toLowerCase().includes(search.toLowerCase()) : true)}
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
                defaultValue={search || ""}
                onChange={v => setSearch(v || null)}
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

