import type { MovieDetails } from "../types/movie";
import type { TmdbMovieSearchResult } from "../types/tmdb";

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5084";

export async function searchTmdbMovies(
  query: string,
  accessToken: string,
  signal?: AbortSignal,
): Promise<TmdbMovieSearchResult[]> {
  const response = await fetch(
    `${API_BASE_URL}/api/admin/tmdb/movies/search?query=${encodeURIComponent(query)}`,
    {
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
      signal,
    },
  );

  if (!response.ok) {
    throw new Error(
      `Unable to search TMDB movies. Status: ${response.status}.`,
    );
  }

  return (await response.json()) as TmdbMovieSearchResult[];
}

export async function importTmdbMovie(
  tmdbId: number,
  accessToken: string,
): Promise<MovieDetails> {
  const response = await fetch(
    `${API_BASE_URL}/api/admin/tmdb/movies/${tmdbId}/import`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    },
  );

  if (!response.ok) {
    const message = await response.text();

    throw new Error(
      message ||
        `Unable to import TMDB movie. Status: ${response.status}.`,
    );
  }

  return (await response.json()) as MovieDetails;
}