import type { MovieDetails,
            MovieSummary } from '../types/movie'


const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5084'

export interface GetMoviesOptions {
  genreId?: number
  page?: number
  pageSize?: number
  signal?: AbortSignal
}



export async function getMovies(
  options: GetMoviesOptions = {},
): Promise<MovieSummary[]> {
  const searchParams = new URLSearchParams()

  if (options.genreId !== undefined) {
    searchParams.set('genreId', options.genreId.toString())
  }

  if (options.page !== undefined) {
    searchParams.set('page', options.page.toString())
  }

  if (options.pageSize !== undefined) {
    searchParams.set('pageSize', options.pageSize.toString())
  }

  const queryString = searchParams.toString()

  const url = `${API_BASE_URL}/api/movies${
    queryString ? `?${queryString}` : ''
  }`

  const response = await fetch(url, {
    signal: options.signal,
  })

  if (!response.ok) {
    throw new Error(
      `Unable to load movies. Status: ${response.status}.`,
    )
  }

  return (await response.json()) as MovieSummary[]
}

export async function getMovieById(
  id: number,
  signal?: AbortSignal,
): Promise<MovieDetails> {
  const response = await fetch(`${API_BASE_URL}/api/movies/${id}`, {
    signal,
  });

  if (!response.ok) {
    throw new Error(
      `Unable to load movie details. Status: ${response.status}.`,
    );
  }

  return (await response.json()) as MovieDetails;
}