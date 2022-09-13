import { useSearchParams } from "react-router-dom";

export function useSearchParam(paramName: string): [string | null, (value: string | null) => void];

export function useSearchParam(paramName: string, defaultValue: string): [string , (value: string) => void];

export function useSearchParam(paramName: string, defaultValue?: string) {
    const [searchParams, setSearchParams] = useSearchParams();
    const searchParam = searchParams.get(paramName) || defaultValue || null;

    if (defaultValue) {
        const setSearchParam = (value: string) => {
            setSearchParams({
                ...Object.fromEntries(searchParams.entries()),
                [paramName]: value
            });
        };

        return [searchParam, setSearchParam];
    } else {
        const setSearchParam = (value: string | null) => {
            if (value) {
                setSearchParams({
                    ...Object.fromEntries(searchParams.entries()),
                    [paramName]: value
                });
            } else {
                setSearchParams(
                    Array.from(searchParams.entries()).filter(([k, v]) => k !== paramName)
                );
            }
        };

        return [searchParam, setSearchParam];
    }
}
