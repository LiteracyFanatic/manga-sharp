import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";
import { VitePWA } from "vite-plugin-pwa";

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [
        react(),
        VitePWA({
            includeAssets: ["favicon.ico", "apple-touch-icon.png", "mask-icon.svg"],
            manifest: {
                name: "MangaSharp",
                short_name: "MangaSharp",
                description: "CLI manga downloader and reader with lightweight web interface",
                icons: [
                    {
                        src: "192x192.png",
                        sizes: "192x192",
                        type: "image/png"
                    },
                    {
                        src: "512x512.png",
                        sizes: "512x512",
                        type: "image/png"
                    },
                    {
                        src: "mask-192x192.png",
                        sizes: "192x192",
                        type: "image/png",
                        purpose: "maskable"
                    },
                    {
                        src: "mask-512x512.png",
                        sizes: "512x512",
                        type: "image/png",
                        purpose: "maskable"
                    }
                ]
            },
            injectRegister: "script"
        })
    ],
    server: {
        proxy: {
            "/api": "http://localhost:8080/",
            "/pages": "http://localhost:8080/"
        }
    }
});
