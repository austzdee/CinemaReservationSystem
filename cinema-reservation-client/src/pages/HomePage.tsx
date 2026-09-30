import { useEffect, useState } from "react";

import { getMovieById, getMovies } from "../services/movieService";
import type { MovieDetails, MovieSummary } from "../types/movie";

export default function HomePage() {
  const [movies, setMovies] = useState<MovieSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [featuredMovie, setFeaturedMovie] = useState<MovieDetails | null>(null);
  const [featuredIndex, setFeaturedIndex] = useState(0);

  useEffect(() => {
    const controller = new AbortController();

    async function loadMovies() {
      try {
        const result = await getMovies({
          page: 1,
          pageSize: 20,
          signal: controller.signal,
        });

        setMovies(result);

        const initialFeaturedIndex = Math.max(
          result.findIndex((movie) => movie.isFeatured),
          0,
        );

        setFeaturedIndex(initialFeaturedIndex);
      } catch (error) {
        if (controller.signal.aborted) {
          return;
        }

        setError(
          error instanceof Error ? error.message : "Unable to load movies.",
        );
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      }
    }

    void loadMovies();

    return () => {
      controller.abort();
    };
  }, []);

  useEffect(() => {
    if (movies.length <= 1) {
      return;
    }

    const intervalId = window.setInterval(() => {
      setFeaturedIndex((currentIndex) => {
        let nextIndex = currentIndex;

        while (nextIndex === currentIndex) {
          nextIndex = Math.floor(Math.random() * movies.length);
        }

        return nextIndex;
      });
    }, 10_000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [movies]);

  useEffect(() => {
    if (movies.length === 0) {
      return;
    }

    const selectedMovie = movies[featuredIndex];

    if (!selectedMovie) {
      return;
    }

    const controller = new AbortController();

    async function loadFeaturedMovie() {
      try {
        const details = await getMovieById(selectedMovie.id, controller.signal);

        setFeaturedMovie(details);
      } catch (error) {
        if (controller.signal.aborted) {
          return;
        }

        setError(
          error instanceof Error
            ? error.message
            : "Unable to load featured movie.",
        );
      }
    }

    void loadFeaturedMovie();

    return () => {
      controller.abort();
    };
  }, [featuredIndex, movies]);

  return (
    <main>
      {isLoading && (
        <div className="mx-auto max-w-6xl px-6 py-20">
          <p className="text-slate-400">Loading movies...</p>
        </div>
      )}

      {error && (
        <div className="mx-auto max-w-6xl px-6 py-20">
          <p role="alert" className="text-red-400">
            {error}
          </p>
        </div>
      )}

      {!isLoading && !error && movies.length === 0 && (
        <section className="mx-auto max-w-6xl px-6 py-20">
          <h1 className="text-4xl font-bold">Cinema Reservation</h1>

          <p className="mt-4 text-slate-400">
            No movies are currently available.
          </p>
        </section>
      )}

      {!isLoading && !error && featuredMovie && (
        <>
          <section className="relative overflow-hidden border-b border-slate-800">
            {featuredMovie.posterUrl && (
              <div
                aria-hidden="true"
                className="absolute inset-0 bg-cover bg-center opacity-30 blur-sm"
                style={{
                  backgroundImage: `url("${featuredMovie.posterUrl}")`,
                }}
              />
            )}

            <div className="absolute inset-0 bg-linear-to-r from-slate-950 via-slate-950/95 to-slate-950/60" />

            <div className="relative mx-auto grid max-w-6xl gap-12 px-6 py-16 md:grid-cols-[1fr_320px] md:items-center lg:py-24">
              <div>
                <p className="text-sm font-semibold uppercase tracking-[0.25em] text-slate-400">
                  Featured movie
                </p>

                <h1 className="mt-4 text-5xl font-bold tracking-tight sm:text-6xl">
                  {featuredMovie.title}
                </h1>

                <div className="mt-5 flex flex-wrap items-center gap-3 text-sm text-slate-300">
                  <span>{featuredMovie.durationMinutes} minutes</span>

                  {featuredMovie.genres.length > 0 && (
                    <>
                      <span aria-hidden="true">•</span>

                      <span>
                        {featuredMovie.genres
                          .map((genre) => genre.name)
                          .join(" · ")}
                      </span>
                    </>
                  )}
                </div>

                <p className="mt-6 max-w-2xl text-lg leading-8 text-slate-300">
                  {featuredMovie.description}
                </p>

                <a
                  href="#now-showing"
                  className="mt-8 inline-flex rounded-md bg-white px-5 py-3 font-medium text-slate-950 transition hover:bg-slate-200"
                >
                  Browse movies
                </a>
              </div>

              <div className="mx-auto w-full max-w-xs md:max-w-none">
                {featuredMovie.posterUrl ? (
                  <img
                    src={featuredMovie.posterUrl}
                    alt={`${featuredMovie.title} poster`}
                    className="aspect-2/3 w-full rounded-xl object-cover shadow-2xl"
                  />
                ) : (
                  <div className="flex aspect-2/3 items-center justify-center rounded-xl bg-slate-800 px-6 text-center text-slate-400">
                    No poster available
                  </div>
                )}
              </div>
            </div>
          </section>

          <section
            id="now-showing"
            aria-labelledby="now-showing-heading"
            className="mx-auto max-w-6xl px-6 py-16"
          >
            <div>
              <p className="text-sm font-semibold uppercase tracking-[0.2em] text-slate-500">
                Cinema catalogue
              </p>

              <h2 id="now-showing-heading" className="mt-2 text-3xl font-bold">
                Now showing
              </h2>

              <p className="mt-3 text-slate-400">
                Browse available movies and choose what you want to watch.
              </p>
            </div>

            <div className="mt-10 grid gap-8 sm:grid-cols-2 lg:grid-cols-3">
              {movies.map((movie) => (
                <article
                  key={movie.id}
                  className="group overflow-hidden rounded-xl border border-slate-800 bg-slate-900 transition hover:-translate-y-1 hover:border-slate-700"
                >
                  {movie.posterUrl ? (
                    <div className="overflow-hidden">
                      <img
                        src={movie.posterUrl}
                        alt={`${movie.title} poster`}
                        className="aspect-2/3 w-full object-cover transition duration-300 group-hover:scale-105"
                      />
                    </div>
                  ) : (
                    <div className="flex aspect-2/3 items-center justify-center bg-slate-800 px-4 text-center text-sm text-slate-400">
                      No poster available
                    </div>
                  )}

                  <div className="p-5">
                    <h3 className="text-xl font-semibold">{movie.title}</h3>

                    <p className="mt-2 text-sm text-slate-400">
                      {movie.durationMinutes} minutes
                    </p>

                    {movie.genres.length > 0 && (
                      <p className="mt-3 text-sm text-slate-300">
                        {movie.genres.map((genre) => genre.name).join(" · ")}
                      </p>
                    )}
                  </div>
                </article>
              ))}
            </div>
          </section>
        </>
      )}
    </main>
  );
}
