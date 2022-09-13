import { useSearchParams } from "react-router-dom";

export function useSearchParam(paramName: string): [string | null, (value: string) => void];

export function useSearchParam(paramName: string, defaultValue: string): [string , (value: string) => void];

export function useSearchParam(paramName: string, defaultValue?: string) {
    const [searchParams, setSearchParams] = useSearchParams();
    const searchParam = searchParams.get(paramName) || defaultValue || null;

    function setSearchParam (value: string) {
        setSearchParams({
            ...Object.fromEntries(searchParams.entries()),
            [paramName]: value
        });
    }

    return [searchParam, setSearchParam];
}
