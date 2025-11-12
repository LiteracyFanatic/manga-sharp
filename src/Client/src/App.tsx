import "@fontsource/roboto/latin-300.css";
import "@fontsource/roboto/latin-400.css";
import "@fontsource/roboto/latin-500.css";
import "@fontsource/roboto/latin-700.css";

import CssBaseLine from "@mui/material/CssBaseline";
import { ConfirmProvider } from "material-ui-confirm";
import { createTheme, ThemeProvider } from "@mui/material/styles";
import { BrowserRouter, Route, Routes } from "react-router-dom";

import Index from "./pages/Index";
import ReadingPage from "./pages/ReadingPage";
import { QueryParamProvider } from "use-query-params";
import { ReactRouter6Adapter } from 'use-query-params/adapters/react-router-6';

const AppRoutes = () => {
    return (
        <Routes>
            <Route path="" element={<Index />} />
            <Route path="/chapters/:chapterId/*" element={<ReadingPage />} />
        </Routes>
    );
};

const theme = createTheme({
    palette: {
        mode: "dark",
        primary: {
            main: "#8844bb"
        }
    }
});

function App() {
    return (
        <ConfirmProvider>
            <BrowserRouter>
                <QueryParamProvider adapter={ReactRouter6Adapter}>
                    <AppRoutes />
                </QueryParamProvider>
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
