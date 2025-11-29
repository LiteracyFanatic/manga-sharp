import { Add } from '@mui/icons-material';
import { CircularProgress, Container, Fab, Toolbar, Typography } from '@mui/material';
import fuzzysort from 'fuzzysort';
import { useState } from 'react';
import {
    QueryParamConfig,
    StringParam,
    createEnumParam,
    useQueryParam,
    withDefault
} from 'use-query-params';

import { MangaGetResponse, useManga } from '../Api';
import { AddMangaModal } from '../components/AddMangaModal';
import IndexAppBar, { SortByKey, SortDirection } from '../components/IndexAppBar';

import MangaList from './MangaList';

function compare(sortBy: SortByKey, a: MangaGetResponse, b: MangaGetResponse) {
    switch (sortBy) {
        case 'Title':
            return a.Title.localeCompare(b.Title);
        case 'Updated':
            return a.Updated.getTime() - b.Updated.getTime();
        default:
            throw new Error('Invalid sort by value.');
    }
}

const sortByKeyParam = withDefault(createEnumParam<SortByKey>(['Title', 'Updated']), 'Title') as QueryParamConfig<SortByKey>;
const sortDirectionParam = withDefault(createEnumParam<SortDirection>(['asc', 'desc']), 'asc') as QueryParamConfig<SortDirection>;

export default function Index() {
    const [isAddModalOpen, setIsAddModalOpen] = useState(false);
    const manga = useManga();
    const [search, setSearch] = useQueryParam('search', StringParam);
    const [sortBy, setSortBy] = useQueryParam('sort_by', sortByKeyParam);
    const [sortDirection, setSortDirection] = useQueryParam('sort_direction', sortDirectionParam);

    function getContent() {
        if (manga.data) {
            const filteredManga = fuzzysort.go(search || '', manga.data, {
                all: true,
                keys: ['Title'],
                threshold: 0.5
            });
            const sortDirectionMultiplier = sortDirection === 'asc' ? 1 : -1;
            const sortedManga = filteredManga.toSorted((a, b) => sortDirectionMultiplier * compare(sortBy, a.obj, b.obj));
            return (
                <MangaList
                    manga={sortedManga}
                    groupByDate={sortBy === 'Updated'}
                    sx={{
                        width: '100%'
                    }}
                />
            );
        }
        else if (manga.isValidating) {
            return <CircularProgress />;
        }
        else {
            return <Typography>Manga not found.</Typography>;
        }
    }

    return (
        <>
            <IndexAppBar
                defaultSearchValue={search || ''}
                onSearchValueChange={v => setSearch(v || null)}
                defaultSortBy={{ key: sortBy, direction: sortDirection }}
                onSortByChange={(v) => {
                    setSortBy(v.key);
                    setSortDirection(v.direction);
                }}
            />
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
                {getContent()}
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
