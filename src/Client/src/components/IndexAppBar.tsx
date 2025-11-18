import { Close, Search, Sort } from '@mui/icons-material';
import {
    AppBar,
    Box,
    IconButton,
    InputBase,
    MenuItem,
    TextField,
    Toolbar,
    alpha
} from '@mui/material';
import { useEffect, useState } from 'react';
import { useDebounce } from 'react-use';

import { MangaGetResponse } from '../Api';

export type SortByKey = Extract<keyof MangaGetResponse, 'Title' | 'Updated'>;

export type SortDirection = 'asc' | 'desc';

export interface SortBy {
    key: SortByKey;
    direction: SortDirection;
}

interface IndexAppBarProps {
    defaultSearchValue?: string;
    onSearchValueChange?: (value: string) => void;
    defaultSortBy?: SortBy;
    onSortByChange?: (value: SortBy) => void;
}

const sortByOptions = [
    { key: 'Title', direction: 'asc' },
    { key: 'Title', direction: 'desc' },
    { key: 'Updated', direction: 'asc' },
    { key: 'Updated', direction: 'desc' }
] as const;

export default function IndexAppBar(props: IndexAppBarProps) {
    const [searchText, setSearchText] = useState(props.defaultSearchValue || '');
    const [searchTextDebounced, setSearchTextDebounced] = useState(searchText);
    useDebounce(
        () => setSearchTextDebounced(searchText),
        500,
        [searchText]
    );
    const [selectedSortByIndex, setSelectedSortByIndex] = useState(sortByOptions.findIndex(v => v.key === props.defaultSortBy?.key && v.direction === props.defaultSortBy.direction));
    const selectedSortBy = sortByOptions[selectedSortByIndex];

    useEffect(() => {
        if (props.onSearchValueChange) {
            props.onSearchValueChange(searchTextDebounced);
        }
    }, [props, searchTextDebounced]);

    useEffect(() => {
        if (props.onSortByChange) {
            props.onSortByChange(selectedSortBy);
        }
    }, [props, selectedSortBy]);

    function onChangeSearchText(value: string) {
        setSearchText(value);
        if (!value) {
            setSearchTextDebounced(value);
        }
    }

    function onClickClear() {
        setSearchText('');
        setSearchTextDebounced('');
    }

    return (
        <Box>
            <AppBar>
                <Toolbar
                    sx={{
                        justifyContent: 'space-between'
                    }}
                >
                    <Box
                        sx={{
                            display: 'flex',
                            justifyContent: 'center',
                            alignItems: 'center',
                            width: '100%',
                            gap: 1
                        }}
                    >
                        <Box
                            sx={theme => ({
                                'display': 'flex',
                                'borderRadius': 1,
                                'backgroundColor': alpha(theme.palette.common.white, 0.15),
                                '&:hover': {
                                    backgroundColor: alpha(theme.palette.common.white, 0.25)
                                },
                                'width': '100%',
                                [theme.breakpoints.up('sm')]: {
                                    marginLeft: theme.spacing(3),
                                    width: 'auto'
                                }
                            })}
                        >
                            <Box
                                sx={{
                                    display: 'flex',
                                    justifyContent: 'center',
                                    alignItems: 'center',
                                    padding: 1
                                }}
                            >
                                <Search />
                            </Box>
                            <InputBase
                                value={searchText}
                                onChange={e => onChangeSearchText(e.target.value)}
                                sx={{
                                    marginLeft: 1,
                                    flexGrow: 1
                                }}
                                placeholder="Search"
                            />
                            <IconButton
                                onClick={onClickClear}
                                disabled={!searchText}
                            >
                                <Close />
                            </IconButton>
                        </Box>
                        <TextField
                            size="small"
                            select
                            value={selectedSortByIndex}
                            onChange={e => setSelectedSortByIndex(parseInt(e.target.value))}
                            slotProps={{
                                input: {
                                    startAdornment: (
                                        <Sort
                                            sx={{
                                                marginRight: 2
                                            }}
                                        />
                                    )
                                }
                            }}
                            sx={theme => ({
                                width: '100%',
                                [theme.breakpoints.up('sm')]: {
                                    width: 'auto'
                                }
                            })}
                        >
                            {sortByOptions.map((v, i) => (
                                <MenuItem
                                    key={i}
                                    value={i}
                                >
                                    {v.key}
                                    {' '}
                                    (
                                    {v.direction === 'asc' ? 'Ascending' : 'Descending'}
                                    )
                                </MenuItem>
                            ))}
                        </TextField>
                    </Box>
                </Toolbar>
            </AppBar>
            <Toolbar />
        </Box>
    );
}
