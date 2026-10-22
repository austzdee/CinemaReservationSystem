import { useEffect, useState } from "react";
import { Link, useParams } from "react-router";

import { getMovieById } from "../services/movieService";
import { getShowtimes } from "../services/showtimeService";
import type { MovieDetails } from "../types/movie";
import { ShowtimeStatus, type Showtime } from "../types/showtime";

export default function MovieShowtimesPage() {
  const { movieId } = useParams<{ movieId: string }>();
  const selectedMovieId = Number(movieId);
  const isValidMovieId = Number.isSafeInteger(selectedMovieId) && selectedMovieId > 0;

  const [movie, setMovie] = useState<MovieDetails | null>(null);
  const [showtimes, setShowtimes] = useState<Showtime[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isValidMovieId) {
      setError("Invalid movie identifier.");
      setIsLoading(false);
      return;
    }

    const controller = new AbortController();

    async function loadMovieShowtimes() {
      setIsLoading(true);
      setError(null);
      setMovie(null);
      setShowtimes([]);

      try {
        const [movieResult, showtimeResult] = await Promise.all([
          getMovieById(selectedMovieId, controller.signal),
          getShowtimes({
            movieId: selectedMovieId,
            startsFrom: new Date().toISOString(),
            signal: controller.signal,
          }),
        ]);

        if (controller.signal.aborted) return;

        setMovie(movieResult);
        // The service supplies all showtime statuses; only bookable future sessions belong here.
        setShowtimes(
          showtimeResult
            .filter(
              (showtime) =>
                showtime.movieId === selectedMovieId &&
                showtime.status === ShowtimeStatus.Scheduled &&
                new Date(showtime.startsAt).getTime() > Date.now(),
            )
            .sort(
              (a, b) =>
                new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime(),
            ),
        );
      } catch (caughtError) {
        if (controller.signal.aborted) return;
        setError(
          caughtError instanceof Error
            ? caughtError.message
            : "Unable to load movie showtimes.",
        );
      } finally {
        if (!controller.signal.aborted) setIsLoading(false);
      }
    }

    void loadMovieShowtimes();
    return () => controller.abort();
  }, [isValidMovieId, selectedMovieId]);

  return (
    <main className="mx-auto max-w-6xl px-6 py-12">
      <Link to="/" className="text-sm text-sky-400 hover:text-sky-300">
        ← Back to movies
      </Link>

      {isLoading && (
        <p role="status" className="mt-8 text-slate-400">Loading showtimes...</p>
      )}

      {!isLoading && error && (
        <p role="alert" className="mt-8 rounded-lg border border-red-900 bg-red-950/40 p-4 text-red-300">
          {error}
        </p>
      )}

      {!isLoading && !error && movie && (
        <>
          <div className="mt-8 flex flex-col gap-6 sm:flex-row">
            {movie.posterUrl && (
              <img
                src={movie.posterUrl}
                alt={`${movie.title} poster`}
                className="w-36 self-start rounded-lg object-cover"
              />
            )}
            <div>
              <p className="text-sm font-semibold uppercase tracking-wider text-sky-400">
                Movie showtimes
              </p>
              <h1 className="mt-2 text-3xl font-bold">{movie.title}</h1>
              <p className="mt-2 text-sm text-slate-400">
                {movie.durationMinutes} minutes
                {movie.genres.length > 0 &&
                  ` · ${movie.genres.map((genre) => genre.name).join(" · ")}`}
              </p>
              <p className="mt-4 max-w-2xl text-slate-300">{movie.description}</p>
            </div>
          </div>

          <section aria-labelledby="showtimes-heading" className="mt-12">
            <h2 id="showtimes-heading" className="text-2xl font-semibold">
              Upcoming screenings
            </h2>
            {showtimes.length === 0 ? (
              <p className="mt-5 rounded-lg border border-slate-800 bg-slate-900 p-5 text-slate-400">
                There are no upcoming screenings scheduled for this movie.
              </p>
            ) : (
              <ul className="mt-6 grid gap-4 md:grid-cols-2">
                {showtimes.map((showtime) => (
                  <li
                    key={showtime.id}
                    className="rounded-xl border border-slate-800 bg-slate-900 p-5"
                  >
                    <p className="text-lg font-semibold">
                      {new Date(showtime.startsAt).toLocaleString("en-GB", {
                        weekday: "long",
                        day: "numeric",
                        month: "long",
                        year: "numeric",
                        hour: "2-digit",
                        minute: "2-digit",
                      })}
                    </p>
                    <p className="mt-2 text-slate-300">
                      {showtime.auditoriumName}
                    </p>
                    <p className="mt-2 font-medium text-sky-400">
                      £{showtime.ticketPrice.toFixed(2)}
                    </p>
                    <p className="mt-4 text-sm text-slate-400">
                      Seat selection will be available in the next development step.
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}
    </main>
  );
}
