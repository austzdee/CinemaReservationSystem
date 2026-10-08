import type { Genre } from "../types/movie";

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5084";

export async function getGenres(
  signal?: AbortSignal,
): Promise<Genre[]> {
  const response = await fetch(
    `${API_BASE_URL}/api/genres`,
    {
      signal,
    },
  );

  if (!response.ok) {
    throw new Error(
      `Unable to load genres. Status: ${response.status}.`,
    );
  }

  return (await response.json()) as Genre[];
}