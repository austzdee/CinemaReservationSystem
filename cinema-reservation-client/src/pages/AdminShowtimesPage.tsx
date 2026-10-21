import { type FormEvent, useEffect, useState } from "react"

import { useAuth } from "../hooks/useAuth"
import { getAuditoriums } from "../services/auditoriumService"
import { getMovies } from "../services/movieService"
import {
  cancelShowtime,
  createShowtime,
  getShowtimes,
  ShowtimeApiError,
  updateShowtime,
} from "../services/showtimeService"
import type { AuditoriumSummary } from "../types/auditorium"
import type { MovieSummary } from "../types/movie"
import type { Showtime } from "../types/showtime"

function toDateTimeLocalValue(value: string): string {
  const date = new Date(value)
  const localDate = new Date(
    date.getTime() - date.getTimezoneOffset() * 60_000,
  )

  return localDate.toISOString().slice(0, 16)
}

export default function AdminShowtimesPage() {
  const { accessToken, logout } = useAuth()

  const [showtimes, setShowtimes] = useState<Showtime[]>([])
  const [movies, setMovies] = useState<MovieSummary[]>([])
  const [auditoriums, setAuditoriums] = useState<AuditoriumSummary[]>([])

  const [movieId, setMovieId] = useState("")
  const [auditoriumId, setAuditoriumId] = useState("")
  const [startsAt, setStartsAt] = useState("")
  const [ticketPrice, setTicketPrice] = useState("")

  const [editingShowtimeId, setEditingShowtimeId] =
    useState<number | null>(null)

  const [isLoading, setIsLoading] = useState(true)
  const [isCreating, setIsCreating] = useState(false)
  const [isUpdating, setIsUpdating] = useState(false)
  const [cancellingShowtimeId, setCancellingShowtimeId] =
    useState<number | null>(null)

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

  function resetForm() {
    setMovieId("")
    setAuditoriumId("")
    setStartsAt("")
    setTicketPrice("")
    setEditingShowtimeId(null)
  }

  function handleStartEdit(showtime: Showtime) {
    setEditingShowtimeId(showtime.id)
    setMovieId(showtime.movieId.toString())
    setAuditoriumId(showtime.auditoriumId.toString())
    setStartsAt(toDateTimeLocalValue(showtime.startsAt))
    setTicketPrice(showtime.ticketPrice.toString())
    setActionError(null)
    setSuccessMessage(null)
  }

  function handleCancelEdit() {
    resetForm()
    setActionError(null)
    setSuccessMessage(null)
  }

  async function handleSubmitShowtime(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (!accessToken) {
      setActionError("Your admin session is no longer available.")
      return
    }

    const selectedMovieId = Number(movieId)
    const selectedAuditoriumId = Number(auditoriumId)
    const price = Number(ticketPrice)

    const selectedStart = new Date(startsAt)

    if (
      !startsAt ||
      Number.isNaN(selectedStart.getTime()) ||
      selectedStart <= new Date()
    ) {
      setActionError("Showtime must start in the future.")
      return
    }

    if (
      !selectedMovieId ||
      !selectedAuditoriumId ||
      !Number.isFinite(price) ||
      price <= 0
    ) {
      setActionError("Complete all showtime fields with valid values.")
      return
    }

    const request = {
      movieId: selectedMovieId,
      auditoriumId: selectedAuditoriumId,
      startsAt: new Date(startsAt).toISOString(),
      ticketPrice: price,
    }

    setActionError(null)
    setSuccessMessage(null)

    if (editingShowtimeId !== null) {
      setIsUpdating(true)

      try {
        const updatedShowtime = await updateShowtime(
          editingShowtimeId,
          request,
          accessToken,
        )

        setShowtimes((currentShowtimes) =>
          currentShowtimes
            .map((showtime) =>
              showtime.id === updatedShowtime.id
                ? updatedShowtime
                : showtime,
            )
            .sort(
              (a, b) =>
                new Date(a.startsAt).getTime() -
                new Date(b.startsAt).getTime(),
            ),
        )

        resetForm()
        setSuccessMessage("Showtime updated successfully.")
      } catch (error) {
        if (error instanceof ShowtimeApiError && error.status === 401) {
          logout()
          return
        }

        setActionError(
          error instanceof Error
            ? error.message
            : "Unable to update showtime.",
        )
      } finally {
        setIsUpdating(false)
      }

      return
    }

    setIsCreating(true)

    try {
      const createdShowtime = await createShowtime(
        request,
        accessToken,
      )

      setShowtimes((currentShowtimes) =>
        [...currentShowtimes, createdShowtime].sort(
          (a, b) =>
            new Date(a.startsAt).getTime() -
            new Date(b.startsAt).getTime(),
        ),
      )

      resetForm()
      setSuccessMessage("Showtime created successfully.")
    } catch (error) {
      if (error instanceof ShowtimeApiError && error.status === 401) {
        logout()
        return
      }

      setActionError(
        error instanceof Error
          ? error.message
          : "Unable to create showtime.",
      )
    } finally {
      setIsCreating(false)
    }
  }

  async function handleCancelShowtime(showtime: Showtime) {
    if (!accessToken) {
      setActionError("Your admin session is no longer available.")
      return
    }

    const confirmed = window.confirm(
      `Cancel the ${showtime.movieTitle} showtime?`,
    )

    if (!confirmed) {
      return
    }

    setCancellingShowtimeId(showtime.id)
    setActionError(null)
    setSuccessMessage(null)

    try {
      await cancelShowtime(showtime.id, accessToken)

      setShowtimes((currentShowtimes) =>
        currentShowtimes.filter(
          (currentShowtime) =>
            currentShowtime.id !== showtime.id,
        ),
      )

      if (editingShowtimeId === showtime.id) {
        resetForm()
      }

      setSuccessMessage("Showtime cancelled successfully.")
    } catch (error) {
      if (error instanceof ShowtimeApiError && error.status === 401) {
        logout()
        return
      }

      setActionError(
        error instanceof Error
          ? error.message
          : "Unable to cancel showtime.",
      )
    } finally {
      setCancellingShowtimeId(null)
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
              {editingShowtimeId !== null
                ? "Edit showtime"
                : "Create showtime"}
            </h2>

            <form
              onSubmit={handleSubmitShowtime}
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
                  min={toDateTimeLocalValue(new Date().toISOString())}
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

                <div className="flex gap-3">
                  <button
                    type="submit"
                    disabled={
                      isCreating ||
                      isUpdating ||
                      movies.length === 0 ||
                      auditoriums.length === 0
                    }
                    className="rounded-md bg-sky-500 px-4 py-2 text-sm font-medium text-slate-950 transition hover:bg-sky-400 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    {isUpdating
                      ? "Saving..."
                      : isCreating
                        ? "Creating..."
                        : editingShowtimeId !== null
                          ? "Save changes"
                          : "Create showtime"}
                  </button>

                  {editingShowtimeId !== null && (
                    <button
                      type="button"
                      onClick={handleCancelEdit}
                      disabled={isUpdating}
                      className="rounded-md border border-slate-700 px-4 py-2 text-sm font-medium text-slate-300 transition hover:border-slate-600 hover:text-white disabled:opacity-50"
                    >
                      Cancel edit
                    </button>
                  )}
                </div>
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
                    <th className="px-4 py-3 font-medium">Actions</th>
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

                      <td className="px-4 py-3">
                        <div className="flex gap-3">
                          <button
                            type="button"
                            onClick={() => handleStartEdit(showtime)}
                            className="text-sky-400 transition hover:text-sky-300"
                          >
                            Edit
                          </button>

                          <button
                            type="button"
                            onClick={() =>
                              void handleCancelShowtime(showtime)
                            }
                            disabled={
                              cancellingShowtimeId === showtime.id
                            }
                            className="text-red-400 transition hover:text-red-300 disabled:opacity-50"
                          >
                            {cancellingShowtimeId === showtime.id
                              ? "Cancelling..."
                              : "Cancel"}
                          </button>
                        </div>
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
