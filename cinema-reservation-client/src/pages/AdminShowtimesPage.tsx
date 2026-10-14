import { type FormEvent, useEffect, useState } from "react"

import { useAuth } from "../hooks/useAuth"
import { getAuditoriums } from "../services/auditoriumService"
import { getMovies } from "../services/movieService"
import {
  createShowtime,
  getShowtimes,
} from "../services/showtimeService"
import type { AuditoriumSummary } from "../types/auditorium"
import type { MovieSummary } from "../types/movie"
import type { Showtime } from "../types/showtime"

export default function AdminShowtimesPage() {
  const { accessToken } = useAuth()

  const [showtimes, setShowtimes] = useState<Showtime[]>([])
  const [movies, setMovies] = useState<MovieSummary[]>([])
  const [auditoriums, setAuditoriums] = useState<AuditoriumSummary[]>([])

  const [movieId, setMovieId] = useState("")
  const [auditoriumId, setAuditoriumId] = useState("")
  const [startsAt, setStartsAt] = useState("")
  const [ticketPrice, setTicketPrice] = useState("")

  const [isLoading, setIsLoading] = useState(true)
  const [isCreating, setIsCreating] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

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

  async function handleCreateShowtime(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!accessToken) {
      setActionError("Your admin session is no longer available.")
      return
    }

    const selectedMovieId = Number(movieId)
    const selectedAuditoriumId = Number(auditoriumId)
    const price = Number(ticketPrice)

    if (
      !selectedMovieId ||
      !selectedAuditoriumId ||
      !startsAt ||
      !Number.isFinite(price) ||
      price <= 0
    ) {
      setActionError("Complete all showtime fields with valid values.")
      return
    }

    setIsCreating(true)
    setActionError(null)
    setSuccessMessage(null)

    try {
      const createdShowtime = await createShowtime(
        {
          movieId: selectedMovieId,
          auditoriumId: selectedAuditoriumId,
          startsAt: new Date(startsAt).toISOString(),
          ticketPrice: price,
        },
        accessToken,
      )

      setShowtimes((currentShowtimes) =>
        [...currentShowtimes, createdShowtime].sort(
          (a, b) =>
            new Date(a.startsAt).getTime() -
            new Date(b.startsAt).getTime(),
        ),
      )

      setMovieId("")
      setAuditoriumId("")
      setStartsAt("")
      setTicketPrice("")
      setSuccessMessage("Showtime created successfully.")
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : "Unable to create showtime.",
      )
    } finally {
      setIsCreating(false)
    }
  }

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

          <section className="mb-8 rounded-lg border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-lg font-semibold">
              Create showtime
            </h2>

            <form
              onSubmit={handleCreateShowtime}
              className="mt-5 grid gap-4 md:grid-cols-2"
            >
              <label className="text-sm text-slate-300">
                Movie
                <select
                  value={movieId}
                  onChange={(event) => setMovieId(event.target.value)}
                  required
                  className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                >
                  <option value="">Select movie</option>

                  {movies.map((movie) => (
                    <option key={movie.id} value={movie.id}>
                      {movie.title}
                    </option>
                  ))}
                </select>
              </label>

              <label className="text-sm text-slate-300">
                Auditorium
                <select
                  value={auditoriumId}
                  onChange={(event) =>
                    setAuditoriumId(event.target.value)
                  }
                  required
                  className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                >
                  <option value="">Select auditorium</option>

                  {auditoriums.map((auditorium) => (
                    <option key={auditorium.id} value={auditorium.id}>
                      {auditorium.name}
                    </option>
                  ))}
                </select>
              </label>

              <label className="text-sm text-slate-300">
                Start date and time
                <input
                  type="datetime-local"
                  value={startsAt}
                  onChange={(event) => setStartsAt(event.target.value)}
                  required
                  className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                />
              </label>

              <label className="text-sm text-slate-300">
                Ticket price (£)
                <input
                  type="number"
                  min="0.01"
                  step="0.01"
                  value={ticketPrice}
                  onChange={(event) =>
                    setTicketPrice(event.target.value)
                  }
                  required
                  className="mt-2 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                />
              </label>

              <div className="md:col-span-2">
                {actionError && (
                  <p className="mb-3 text-sm text-red-300">
                    {actionError}
                  </p>
                )}

                {successMessage && (
                  <p className="mb-3 text-sm text-emerald-300">
                    {successMessage}
                  </p>
                )}

                <button
                  type="submit"
                  disabled={
                    isCreating ||
                    movies.length === 0 ||
                    auditoriums.length === 0
                  }
                  className="rounded-md bg-sky-500 px-4 py-2 text-sm font-medium text-slate-950 transition hover:bg-sky-400 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {isCreating
                    ? "Creating..."
                    : "Create showtime"}
                </button>
              </div>
            </form>
          </section>

          {showtimes.length === 0 ? (
            <div className="rounded-lg border border-slate-800 bg-slate-900 p-6 text-sm text-slate-400">
              No scheduled showtimes are currently available.
            </div>
          ) : (
            <div className="overflow-x-auto rounded-lg border border-slate-800">
              <table className="min-w-full divide-y divide-slate-800 text-sm">
                <thead className="bg-slate-900 text-left text-slate-400">
                  <tr>
                    <th className="px-4 py-3 font-medium">Movie</th>
                    <th className="px-4 py-3 font-medium">Auditorium</th>
                    <th className="px-4 py-3 font-medium">Starts</th>
                    <th className="px-4 py-3 font-medium">Ends</th>
                    <th className="px-4 py-3 font-medium">Price</th>
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
