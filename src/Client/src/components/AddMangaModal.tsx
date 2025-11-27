import {
    Button,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    FormControl,
    InputLabel,
    MenuItem,
    Select,
    TextField
} from '@mui/material';
import { useSnackbar } from 'notistack';
import React, { useState } from 'react';

import { Direction, useAddManga } from '../Api';

interface AddMangaModalProps {
    isOpen: boolean;
    onClose: () => void;
}

export const AddMangaModal: React.FC<AddMangaModalProps> = ({ isOpen, onClose }) => {
    const [url, setUrl] = useState('');
    const [direction, setDirection] = useState<Direction>('Horizontal');
    const { trigger, isMutating } = useAddManga();
    const { enqueueSnackbar } = useSnackbar();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await trigger({ Url: url, Direction: direction });
            setUrl('');
            onClose();
            enqueueSnackbar('Manga added', { variant: 'success' });
        }
        catch (error) {
            console.error(error);
            enqueueSnackbar('Failed to add manga', { variant: 'error' });
        }
    };

    return (
        <Dialog open={isOpen} onClose={onClose} fullWidth maxWidth="sm">
            <form onSubmit={handleSubmit}>
                <DialogTitle>Add Manga</DialogTitle>
                <DialogContent>
                    <TextField
                        autoFocus
                        margin="dense"
                        label="URL"
                        type="url"
                        fullWidth
                        variant="outlined"
                        value={url}
                        onChange={e => setUrl(e.target.value)}
                        required
                    />
                    <FormControl fullWidth margin="dense">
                        <InputLabel>Direction</InputLabel>
                        <Select
                            value={direction}
                            label="Direction"
                            onChange={e => setDirection(e.target.value as Direction)}
                        >
                            <MenuItem value="Horizontal">Horizontal</MenuItem>
                            <MenuItem value="Vertical">Vertical</MenuItem>
                        </Select>
                    </FormControl>
                </DialogContent>
                <DialogActions>
                    <Button onClick={onClose}>Cancel</Button>
                    <Button type="submit" disabled={isMutating} variant="contained">
                        {isMutating ? 'Adding...' : 'Add'}
                    </Button>
                </DialogActions>
            </form>
        </Dialog>
    );
};
