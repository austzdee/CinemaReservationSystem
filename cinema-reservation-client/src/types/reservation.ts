export const ReservationStatus = {
  Confirmed: 1,
  Cancelled: 2,
} as const

export type ReservationStatus =
  (typeof ReservationStatus)[keyof typeof ReservationStatus]

export interface ReservationSeat {
  seatId: number
  row: string
  number: number
  unitPrice: number
}

export interface Reservation {
  id: number
  showtimeId: number
  status: ReservationStatus
  createdAt: string
  cancelledAt: string | null
  totalPrice: number
  seats: ReservationSeat[]
}