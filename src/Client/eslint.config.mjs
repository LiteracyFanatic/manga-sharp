import baseConfig from '@literacyfanatic/eslint-config';
import { defineConfig, globalIgnores } from 'eslint/config';
import eslintPluginZodX from 'eslint-plugin-zod-x';

export default defineConfig([
    globalIgnores(['dist']),
    ...baseConfig,
    eslintPluginZodX.configs.recommended
])
