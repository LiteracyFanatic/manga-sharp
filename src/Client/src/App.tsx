import "@fontsource/roboto/latin-300.css";
import "@fontsource/roboto/latin-400.css";
import "@fontsource/roboto/latin-500.css";
import "@fontsource/roboto/latin-700.css";

import CssBaseLine from "@mui/material/CssBaseline";
import { createTheme, ThemeProvider } from "@mui/material/styles";
import { BrowserRouter, Route, Routes } from "react-router-dom";

import Index from "./pages/Index";
import ReadingPage from "./pages/ReadingPage";

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

export default function App() {
    return (
        <ThemeProvider theme={theme}>
            <CssBaseLine />
            <BrowserRouter>
                <AppRoutes />
            </BrowserRouter>
        </ThemeProvider>
    );
}
