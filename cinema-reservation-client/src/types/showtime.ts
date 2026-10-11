export const ShowtimeStatus = {
  Scheduled: 1,
  Cancelled: 2,
} as const

export type ShowtimeStatus =
  (typeof ShowtimeStatus)[keyof typeof ShowtimeStatus]

export interface Showtime {
  id: number
  movieId: number
  movieTitle: string
  auditoriumId: number
  auditoriumName: string
  startsAt: string
  endsAt: string
  ticketPrice: number
  status: ShowtimeStatus
}

export interface CreateShowtimeRequest {
  movieId: number
  auditoriumId: number
  startsAt: string
  ticketPrice: number
}

export interface UpdateShowtimeRequest {
  movieId: number
  auditoriumId: number
  startsAt: string
  ticketPrice: number
}