import { CircularProgress, Typography } from "@mui/material";
import { Container } from "@mui/system";

import { useManga } from "../Api";
import MangaList from "./MangaList";

export default function Index() {
    const manga = useManga();

    function getContent() {
        if (manga.data) {
            return <MangaList manga={manga.data} />;
        } else if (manga.isValidating) {
            return <CircularProgress />;
        } else {
            return <Typography>Manga not found.</Typography>;
        }
    }

    return (
        <Container
            sx={{
                display: "flex",
                justifyContent: "center",
                alignItems: "center",
                minHeight: "100vh"
            }}
            maxWidth="md"
        >
            {getContent()}
        </Container>
    );
}

