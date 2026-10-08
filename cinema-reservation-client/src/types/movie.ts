export interface Genre {
  id: number
  name: string
}

export interface MovieSummary {
  id: number
  title: string
  posterUrl: string | null
  durationMinutes: number
  isFeatured: boolean
  genres: Genre[]
}

export interface MovieDetails extends MovieSummary {
  description: string
  isActive: boolean
  createdAt: string
  updatedAt: string | null
}
export interface UpdateMovieRequest {
  title: string
  description: string
  posterUrl: string | null
  durationMinutes: number
  genreIds: number[]
}