import baseConfig from '@literacyfanatic/eslint-config'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
    globalIgnores(['dist']),
    ...baseConfig,
])
