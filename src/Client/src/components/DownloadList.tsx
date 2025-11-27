import { ArrowDownward, ArrowUpward, Cancel } from '@mui/icons-material';
import {
    Box,
    Button,
    IconButton,
    LinearProgress,
    List,
    ListItem,
    ListItemText,
    Stack,
    Tab,
    Tabs,
    Typography
} from '@mui/material';
import React, { useState } from 'react';

import {
    DownloadJob,
    JobStatus,
    useCancelJob,
    useClearCompletedDownloads,
    useDownloads,
    useMoveJobBottom,
    useMoveJobTop
} from '../Api';

export const DownloadList: React.FC = () => {
    const { data: downloads } = useDownloads();
    const { trigger: clearCompleted, isMutating: isClearing } = useClearCompletedDownloads();
    const { trigger: moveTop } = useMoveJobTop();
    const { trigger: moveBottom } = useMoveJobBottom();
    const { trigger: cancel } = useCancelJob();

    const [tab, setTab] = useState(0);

    if (!downloads) {
        return null;
    }

    const activeJobs = downloads.filter(j => j.Status === 'Downloading');
    const pendingJobs = downloads.filter(j => j.Status === 'Pending');
    const completedJobs = downloads.filter(j => ['Completed', 'Failed', 'Canceled'].includes(j.Status));

    const currentJobs = tab === 0 ? activeJobs : tab === 1 ? pendingJobs : completedJobs;

    const getStatusColor = (status: JobStatus) => {
        switch (status) {
            case 'Pending':
                return 'warning.main';
            case 'Downloading':
                return 'info.main';
            case 'Completed':
                return 'success.main';
            case 'Canceled':
            case 'Failed':
            default:
                return 'error.main';
        }
    };

    const formatStatus = (status: JobStatus): string => {
        return status;
    };

    const getJobTitle = (job: DownloadJob): string => {
        if (job.Title) {
            return job.Title;
        }
        switch (job.Type) {
            case 'AddManga':
                return `Adding: ${job.Url ?? ''}`;
            case 'UpdateManga':
                return job.Title ? `Updating: ${job.Title}` : 'Updating manga';
            default:
                return 'Unknown Job';
        }
    };

    const getProgressMessage = (job: DownloadJob): string => {
        if (!job.ProgressChapterTitle || job.ProgressChapterIndex == null || job.ProgressTotalChapters == null) {
            return '';
        }

        const chapterPart = `Downloading Chapter ${job.ProgressChapterTitle} (${(job.ProgressChapterIndex + 1).toString()}/${job.ProgressTotalChapters.toString()})`;

        if (job.ProgressPageIndex != null && job.ProgressTotalPages != null) {
            return `${chapterPart} (Page ${(job.ProgressPageIndex + 1).toString()}/${job.ProgressTotalPages.toString()})`;
        }

        return chapterPart;
    };

    const getProgressPercent = (job: DownloadJob): number => {
        if (job.ProgressChapterIndex == null || job.ProgressTotalChapters == null) {
            return 0;
        }

        if (job.ProgressPageIndex != null && job.ProgressTotalPages != null) {
            const chapterPercent = (job.ProgressPageIndex + 1) / job.ProgressTotalPages;
            return ((job.ProgressChapterIndex + chapterPercent) / job.ProgressTotalChapters) * 100;
        }

        return (job.ProgressChapterIndex / job.ProgressTotalChapters) * 100;
    };

    const renderStatus = (job: DownloadJob) => {
        if (job.Status === 'Downloading' && job.ProgressChapterTitle) {
            const message = getProgressMessage(job);
            const percent = getProgressPercent(job);

            return (
                <Box component="span" sx={{ display: 'block', mt: 0.5 }}>
                    <Typography
                        variant="caption"
                        display="block"
                        noWrap
                        title={message}
                    >
                        {message}
                    </Typography>
                    <LinearProgress variant="determinate" value={percent} />
                </Box>
            );
        }
        return null;
    };

    return (
        <Box sx={{ width: '100%' }}>
            <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
                <Tabs value={tab} onChange={(_, v) => setTab(Number(v))}>
                    <Tab label={`Active (${activeJobs.length.toString()})`} />
                    <Tab label={`Pending (${pendingJobs.length.toString()})`} />
                    <Tab label={`Completed (${completedJobs.length.toString()})`} />
                </Tabs>
            </Box>

            {tab === 2 && completedJobs.length > 0 && (
                <Box sx={{ p: 2, display: 'flex', justifyContent: 'flex-end' }}>
                    <Button onClick={() => clearCompleted()} disabled={isClearing} variant="outlined" color="error">
                        Clear Completed
                    </Button>
                </Box>
            )}

            <List>
                {currentJobs.map(job => (
                    <ListItem
                        key={job.Id}
                        secondaryAction={(
                            <Stack direction="row" spacing={1}>
                                {job.Status === 'Pending' && (
                                    <>
                                        <IconButton onClick={() => moveTop(job.Id)} size="small">
                                            <ArrowUpward />
                                        </IconButton>
                                        <IconButton onClick={() => moveBottom(job.Id)} size="small">
                                            <ArrowDownward />
                                        </IconButton>
                                    </>
                                )}
                                {(job.Status === 'Pending' || job.Status === 'Downloading') && (
                                    <IconButton onClick={() => cancel(job.Id)} color="error" size="small">
                                        <Cancel />
                                    </IconButton>
                                )}
                            </Stack>
                        )}
                    >
                        <ListItemText
                            primary={getJobTitle(job)}
                            secondary={(
                                <>
                                    <Typography component="span" color={getStatusColor(job.Status)}>
                                        {formatStatus(job.Status)}
                                    </Typography>
                                    {renderStatus(job)}
                                    {job.Error && <Typography color="error" display="block">{job.Error}</Typography>}
                                </>
                            )}
                        />
                    </ListItem>
                ))}
                {currentJobs.length === 0 && (
                    <ListItem>
                        <ListItemText primary="No jobs in this category" />
                    </ListItem>
                )}
            </List>
        </Box>
    );
};
