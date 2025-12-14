import '@fontsource/roboto/latin-300.css';
import '@fontsource/roboto/latin-400.css';
import '@fontsource/roboto/latin-500.css';
import '@fontsource/roboto/latin-700.css';

import { Box } from '@mui/material';
import CssBaseLine from '@mui/material/CssBaseline';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { ConfirmProvider } from 'material-ui-confirm';
import { SnackbarProvider } from 'notistack';
import { NuqsAdapter } from 'nuqs/adapters/react-router';
import { BrowserRouter, Route, Routes } from 'react-router-dom';

import AppDrawer from './components/AppDrawer';
import { DrawerProvider } from './hooks/useDrawer';
import DownloadsPage from './pages/DownloadsPage';
import Index from './pages/Index';
import ReadingPage from './pages/ReadingPage';

const AppRoutes = () => {
    return (
        <Routes>
            <Route path="" element={<Index />} />
            <Route path="/downloads" element={<DownloadsPage />} />
            <Route path="/chapters/:chapterId">
                <Route path="*" element={<ReadingPage />} />
            </Route>
        </Routes>
    );
};

const theme = createTheme({
    palette: {
        mode: 'dark',
        primary: {
            main: '#8844bb'
        }
    }
});

function App() {
    return (
        <ConfirmProvider>
            <BrowserRouter>
                <NuqsAdapter>
                    <DrawerProvider>
                        <SnackbarProvider
                            maxSnack={3}
                            anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
                            preventDuplicate
                        >
                            <Box sx={{ display: 'flex' }}>
                                <AppDrawer />
                                <Box component="main" sx={{ flexGrow: 1 }}>
                                    <AppRoutes />
                                </Box>
                            </Box>
                        </SnackbarProvider>
                    </DrawerProvider>
                </NuqsAdapter>
            </BrowserRouter>
        </ConfirmProvider>
    );
}

export default function AppWithAuth() {
    return (
        <ThemeProvider theme={theme}>
            <CssBaseLine />
            <App />
        </ThemeProvider>
    );
}
