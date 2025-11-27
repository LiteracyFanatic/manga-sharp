import { Add } from '@mui/icons-material';
import { Container, Fab, Toolbar } from '@mui/material';
import { useState } from 'react';

import { AddMangaModal } from '../components/AddMangaModal';
import { DownloadList } from '../components/DownloadList';
import DownloadsAppBar from '../components/DownloadsAppBar';

export default function DownloadsPage() {
    const [isAddModalOpen, setIsAddModalOpen] = useState(false);
    return (
        <>
            <DownloadsAppBar />
            <Toolbar />
            <Container
                sx={{
                    display: 'flex',
                    justifyContent: 'center',
                    marginY: 2,
                    padding: 3
                }}
                maxWidth="md"
            >
                <DownloadList />
            </Container>
            <Fab
                color="primary"
                aria-label="add"
                sx={{ position: 'fixed', bottom: 16, right: 16 }}
                onClick={() => setIsAddModalOpen(true)}
            >
                <Add />
            </Fab>
            <AddMangaModal isOpen={isAddModalOpen} onClose={() => setIsAddModalOpen(false)} />
        </>
    );
}
