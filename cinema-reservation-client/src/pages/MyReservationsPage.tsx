import { useEffect, useState } from 'react'

import { useAuth } from '../hooks/useAuth'
import { getMyReservations } from '../services/reservationService'
import {
  ReservationStatus,
  type Reservation,
} from '../types/reservation'

function getStatusLabel(status: number): string {
  switch (status) {
    case ReservationStatus.Confirmed:
      return 'Confirmed'

    case ReservationStatus.Cancelled:
      return 'Cancelled'

    default:
      return 'Unknown'
  }
}

export default function MyReservationsPage() {
  const { accessToken } = useAuth()

  const [reservations, setReservations] = useState<Reservation[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!accessToken) {
      return
    }
    const token = accessToken

    const controller = new AbortController()

    async function loadReservations() {
      try {
        setError(null)

        const result = await getMyReservations(
          token,
          controller.signal,
        )

        setReservations(result)
      } catch (error) {
        if (controller.signal.aborted) {
          return
        }

        setError(
          error instanceof Error
            ? error.message
            : 'Unable to load reservations.',
        )
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false)
        }
      }
    }

    void loadReservations()

    return () => {
      controller.abort()
    }
  }, [accessToken])

  return (
    <main className="mx-auto max-w-6xl px-6 py-16">
      <h1 className="text-3xl font-bold">
        My Reservations
      </h1>

      <p className="mt-2 text-slate-400">
        View your cinema reservation history.
      </p>

      {isLoading && (
        <p className="mt-8 text-slate-400">
          Loading reservations...
        </p>
      )}

      {error && (
        <p
          role="alert"
          className="mt-8 text-red-400"
        >
          {error}
        </p>
      )}

      {!isLoading && !error && reservations.length === 0 && (
        <p className="mt-8 text-slate-400">
          You do not have any reservations yet.
        </p>
      )}

      {!isLoading && !error && reservations.length > 0 && (
        <div className="mt-8 space-y-4">
          {reservations.map((reservation) => (
            <article
              key={reservation.id}
              className="rounded-lg border border-slate-800 bg-slate-900 p-6"
            >
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                  <h2 className="font-semibold">
                    Reservation #{reservation.id}
                  </h2>

                  <p className="mt-1 text-sm text-slate-400">
                    Showtime #{reservation.showtimeId}
                  </p>
                </div>

                <span className="rounded-full border border-slate-700 px-3 py-1 text-sm">
                  {getStatusLabel(reservation.status)}
                </span>
              </div>

              <dl className="mt-6 grid gap-4 sm:grid-cols-3">
                <div>
                  <dt className="text-sm text-slate-400">
                    Seats
                  </dt>

                  <dd className="mt-1">
                    {reservation.seats
                      .map((seat) => `${seat.row}${seat.number}`)
                      .join(', ')}
                  </dd>
                </div>

                <div>
                  <dt className="text-sm text-slate-400">
                    Total
                  </dt>

                  <dd className="mt-1">
                    {reservation.totalPrice.toFixed(2)}
                  </dd>
                </div>

                <div>
                  <dt className="text-sm text-slate-400">
                    Reserved
                  </dt>

                  <dd className="mt-1">
                    {new Date(
                      reservation.createdAt,
                    ).toLocaleString()}
                  </dd>
                </div>
              </dl>
            </article>
          ))}
        </div>
      )}
    </main>
  )
}