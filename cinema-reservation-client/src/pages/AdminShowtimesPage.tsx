import { useEffect, useState } from "react"

import { getAuditoriums } from "../services/auditoriumService"
import { getMovies } from "../services/movieService"
import { getShowtimes } from "../services/showtimeService"
import type { AuditoriumSummary } from "../types/auditorium"
import type { MovieSummary } from "../types/movie"
import type { Showtime } from "../types/showtime"

export default function AdminShowtimesPage() {
  const [showtimes, setShowtimes] = useState<Showtime[]>([])
  const [movies, setMovies] = useState<MovieSummary[]>([])
  const [auditoriums, setAuditoriums] = useState<AuditoriumSummary[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function loadData() {
      try {
        const [showtimeResult, movieResult, auditoriumResult] =
          await Promise.all([
            getShowtimes({
              signal: controller.signal,
            }),
            getMovies({
              pageSize: 100,
              signal: controller.signal,
            }),
            getAuditoriums(controller.signal),
          ])

        setShowtimes(showtimeResult)
        setMovies(movieResult)
        setAuditoriums(auditoriumResult)
      } catch (error) {
        if (controller.signal.aborted) {
          return
        }

        setError(
          error instanceof Error
            ? error.message
            : "Unable to load showtime administration data.",
        )
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false)
        }
      }
    }

    void loadData()

    return () => {
      controller.abort()
    }
  }, [])

  return (
    <main className="mx-auto max-w-6xl px-6 py-10">
      <div className="mb-8">
        <p className="text-sm font-medium uppercase tracking-wider text-sky-400">
          Administration
        </p>

        <h1 className="mt-2 text-3xl font-semibold">
          Showtimes
        </h1>

        <p className="mt-2 text-sm text-slate-400">
          Manage movie schedules, auditoriums, and ticket prices.
        </p>
      </div>

      {isLoading && (
        <p className="text-sm text-slate-400">
          Loading showtimes...
        </p>
      )}

      {error && (
        <div className="rounded-lg border border-red-900 bg-red-950/40 p-4 text-sm text-red-300">
          {error}
        </div>
      )}

      {!isLoading && !error && (
        <>
          <div className="mb-6 grid gap-4 sm:grid-cols-3">
            <div className="rounded-lg border border-slate-800 bg-slate-900 p-4">
              <p className="text-sm text-slate-400">
                Scheduled showtimes
              </p>
              <p className="mt-1 text-2xl font-semibold">
                {showtimes.length}
              </p>
            </div>

            <div className="rounded-lg border border-slate-800 bg-slate-900 p-4">
              <p className="text-sm text-slate-400">
                Available movies
              </p>
              <p className="mt-1 text-2xl font-semibold">
                {movies.length}
              </p>
            </div>

            <div className="rounded-lg border border-slate-800 bg-slate-900 p-4">
              <p className="text-sm text-slate-400">
                Auditoriums
              </p>
              <p className="mt-1 text-2xl font-semibold">
                {auditoriums.length}
              </p>
            </div>
          </div>

          {showtimes.length === 0 ? (
            <div className="rounded-lg border border-slate-800 bg-slate-900 p-6 text-sm text-slate-400">
              No scheduled showtimes are currently available.
            </div>
          ) : (
            <div className="overflow-x-auto rounded-lg border border-slate-800">
              <table className="min-w-full divide-y divide-slate-800 text-sm">
                <thead className="bg-slate-900 text-left text-slate-400">
                  <tr>
                    <th className="px-4 py-3 font-medium">
                      Movie
                    </th>
                    <th className="px-4 py-3 font-medium">
                      Auditorium
                    </th>
                    <th className="px-4 py-3 font-medium">
                      Starts
                    </th>
                    <th className="px-4 py-3 font-medium">
                      Ends
                    </th>
                    <th className="px-4 py-3 font-medium">
                      Price
                    </th>
                  </tr>
                </thead>

                <tbody className="divide-y divide-slate-800 bg-slate-950">
                  {showtimes.map((showtime) => (
                    <tr key={showtime.id}>
                      <td className="px-4 py-3 font-medium text-white">
                        {showtime.movieTitle}
                      </td>

                      <td className="px-4 py-3 text-slate-300">
                        {showtime.auditoriumName}
                      </td>

                      <td className="px-4 py-3 text-slate-300">
                        {new Date(showtime.startsAt).toLocaleString()}
                      </td>

                      <td className="px-4 py-3 text-slate-300">
                        {new Date(showtime.endsAt).toLocaleString()}
                      </td>

                      <td className="px-4 py-3 text-slate-300">
                        £{showtime.ticketPrice.toFixed(2)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </main>
  )
}
