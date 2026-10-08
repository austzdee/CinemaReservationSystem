import { useEffect, useState } from "react";

import { useAuth } from "../hooks/useAuth";
import {
  archiveMovie,
  getMovieById,
  getMovies,
  setFeaturedMovie,
  updateMovie,
} from "../services/movieService";
import { getGenres } from "../services/genreService";
import type {
  Genre,
  MovieDetails,
  MovieSummary,
  UpdateMovieRequest,
} from "../types/movie";
import { importTmdbMovie, searchTmdbMovies } from "../services/tmdbService";
import type { TmdbMovieSearchResult } from "../types/tmdb";

export default function AdminMoviesPage() {
  const [movies, setMovies] = useState<MovieSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [archivingMovieId, setArchivingMovieId] = useState<number | null>(null);
  const { accessToken } = useAuth();
  const [genres, setGenres] = useState<Genre[]>([]);
  const [editingMovie, setEditingMovie] = useState<MovieDetails | null>(null);
  const [loadingEditMovieId, setLoadingEditMovieId] = useState<number | null>(
    null,
  );
  const [isSavingEdit, setIsSavingEdit] = useState(false);

  const [updatingMovieId, setUpdatingMovieId] = useState<number | null>(null);
  const [editRequest, setEditRequest] = useState<UpdateMovieRequest | null>(
    null,
  );
  const [actionError, setActionError] = useState<string | null>(null);
  const [tmdbQuery, setTmdbQuery] = useState("");
  const [tmdbResults, setTmdbResults] = useState<TmdbMovieSearchResult[]>([]);
  const [isSearchingTmdb, setIsSearchingTmdb] = useState(false);
  const [importingTmdbId, setImportingTmdbId] = useState<number | null>(null);
  const [tmdbError, setTmdbError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    async function loadData() {
      try {
        const [movieResult, genreResult] = await Promise.all([
          getMovies({
            pageSize: 100,
            signal: controller.signal,
          }),
          getGenres(controller.signal),
        ]);

        setMovies(movieResult);
        setGenres(genreResult);
      } catch (error) {
        if (controller.signal.aborted) {
          return;
        }

        setError(
          error instanceof Error
            ? error.message
            : "Unable to load movie administration data.",
        );
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      }
    }

    void loadData();

    return () => {
      controller.abort();
    };
  }, []);

  async function handleEditMovie(movieId: number) {
    setLoadingEditMovieId(movieId);
    setActionError(null);

    try {
      const movie = await getMovieById(movieId);

      setEditingMovie(movie);
      setEditRequest({
        title: movie.title,
        description: movie.description,
        posterUrl: movie.posterUrl,
        durationMinutes: movie.durationMinutes,
        genreIds: movie.genres.map((genre) => genre.id),
      });
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : "Unable to load movie details.",
      );
    } finally {
      setLoadingEditMovieId(null);
    }
  }

  function handleEditFieldChange(
    field: keyof Omit<UpdateMovieRequest, "genreIds">,
    value: string,
  ) {
    setEditRequest((currentRequest) => {
      if (!currentRequest) {
        return currentRequest;
      }

      if (field === "durationMinutes") {
        return {
          ...currentRequest,
          durationMinutes: Number(value),
        };
      }

      return {
        ...currentRequest,
        [field]: value,
      };
    });
  }

  function handleGenreToggle(genreId: number) {
    setEditRequest((currentRequest) => {
      if (!currentRequest) {
        return currentRequest;
      }

      const isSelected = currentRequest.genreIds.includes(genreId);

      return {
        ...currentRequest,
        genreIds: isSelected
          ? currentRequest.genreIds.filter((id) => id !== genreId)
          : [...currentRequest.genreIds, genreId],
      };
    });
  }

  function handleCancelEdit() {
    setEditingMovie(null);
    setEditRequest(null);
    setActionError(null);
  }

  async function handleSaveEdit() {
    if (!accessToken || !editingMovie || !editRequest) {
      setActionError("The movie edit session is no longer available.");
      return;
    }

    if (editRequest.genreIds.length === 0) {
      setActionError("Select at least one genre.");
      return;
    }

    setIsSavingEdit(true);
    setActionError(null);

    try {
      const updatedMovie = await updateMovie(
        editingMovie.id,
        editRequest,
        accessToken,
      );

      setMovies((currentMovies) =>
        currentMovies.map((movie) =>
          movie.id === updatedMovie.id
            ? {
                id: updatedMovie.id,
                title: updatedMovie.title,
                posterUrl: updatedMovie.posterUrl,
                durationMinutes: updatedMovie.durationMinutes,
                isFeatured: updatedMovie.isFeatured,
                genres: updatedMovie.genres,
              }
            : movie,
        ),
      );

      setEditingMovie(null);
      setEditRequest(null);
    } catch (error) {
      setActionError(
        error instanceof Error ? error.message : "Unable to update movie.",
      );
    } finally {
      setIsSavingEdit(false);
    }
  }

  async function handleTmdbSearch() {
    if (!accessToken) {
      setTmdbError("Your session is no longer available.");
      return;
    }

    const query = tmdbQuery.trim();

    if (!query) {
      setTmdbError("Enter a movie title to search.");
      return;
    }

    setIsSearchingTmdb(true);
    setTmdbError(null);

    try {
      const results = await searchTmdbMovies(query, accessToken);

      setTmdbResults(results);
    } catch (error) {
      setTmdbError(
        error instanceof Error ? error.message : "Unable to search TMDB.",
      );
    } finally {
      setIsSearchingTmdb(false);
    }
  }

  async function handleTmdbImport(tmdbId: number) {
    if (!accessToken) {
      setTmdbError("Your session is no longer available.");
      return;
    }

    setImportingTmdbId(tmdbId);
    setTmdbError(null);

    try {
      const importedMovie = await importTmdbMovie(tmdbId, accessToken);

      setMovies((currentMovies) =>
        [
          ...currentMovies,
          {
            id: importedMovie.id,
            title: importedMovie.title,
            posterUrl: importedMovie.posterUrl,
            durationMinutes: importedMovie.durationMinutes,
            isFeatured: importedMovie.isFeatured,
            genres: importedMovie.genres,
          },
        ].sort((a, b) => a.title.localeCompare(b.title)),
      );

      setTmdbResults((currentResults) =>
        currentResults.filter((result) => result.tmdbId !== tmdbId),
      );
    } catch (error) {
      setTmdbError(
        error instanceof Error
          ? error.message
          : "Unable to import movie from TMDB.",
      );
    } finally {
      setImportingTmdbId(null);
    }
  }

  async function handleSetFeatured(movieId: number) {
    if (!accessToken) {
      setActionError("Your session is no longer available.");
      return;
    }

    setUpdatingMovieId(movieId);
    setActionError(null);

    try {
      const featuredMovie = await setFeaturedMovie(movieId, accessToken);

      // Keep the client catalogue consistent with the backend's
      // single-featured-movie rule without reloading the full list.
      setMovies((currentMovies) =>
        currentMovies.map((movie) => ({
          ...movie,
          isFeatured: movie.id === featuredMovie.id,
        })),
      );
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : "Unable to update the featured movie.",
      );
    } finally {
      setUpdatingMovieId(null);
    }
  }
  async function handleArchiveMovie(movieId: number, movieTitle: string) {
    if (!accessToken) {
      setActionError("Your session is no longer available.");
      return;
    }

    const confirmed = window.confirm(`Archive "${movieTitle}"?`);

    if (!confirmed) {
      return;
    }

    setArchivingMovieId(movieId);
    setActionError(null);

    try {
      await archiveMovie(movieId, accessToken);

      // Archived movies are removed from the active catalogue returned
      // by the public movie endpoint, so remove the row locally as well.
      setMovies((currentMovies) =>
        currentMovies.filter((movie) => movie.id !== movieId),
      );
    } catch (error) {
      setActionError(
        error instanceof Error ? error.message : "Unable to archive movie.",
      );
    } finally {
      setArchivingMovieId(null);
    }
  }

  return (
    <main className="mx-auto max-w-6xl px-6 py-10">
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-sm font-medium uppercase tracking-[0.2em] text-slate-400">
            Administration
          </p>

          <h1 className="mt-2 text-3xl font-semibold tracking-tight text-white">
            Movie management
          </h1>

          <p className="mt-2 max-w-2xl text-sm text-slate-400">
            Manage catalogue movies, featured content, and movie metadata.
          </p>
        </div>
      </div>

      <section className="mt-8 rounded-xl border border-slate-800 bg-slate-900/50 p-6">
        <div>
          <p className="text-sm font-medium uppercase tracking-[0.2em] text-slate-400">
            TMDB import
          </p>

          <h2 className="mt-2 text-2xl font-semibold text-white">
            Add movie from TMDB
          </h2>

          <p className="mt-2 text-sm text-slate-400">
            Search TMDB and import movie metadata into the cinema catalogue.
          </p>
        </div>

        <div className="mt-6 flex flex-col gap-3 sm:flex-row">
          <input
            type="search"
            value={tmdbQuery}
            onChange={(event) => setTmdbQuery(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter") {
                void handleTmdbSearch();
              }
            }}
            placeholder="Search for a movie..."
            className="min-w-0 flex-1 rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white outline-none placeholder:text-slate-500 focus:border-slate-500"
          />

          <button
            type="button"
            onClick={() => void handleTmdbSearch()}
            disabled={isSearchingTmdb}
            className="rounded-md bg-white px-4 py-2 text-sm font-medium text-slate-950 transition hover:bg-slate-200 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isSearchingTmdb ? "Searching..." : "Search"}
          </button>
        </div>

        {tmdbError && <p className="mt-4 text-sm text-red-400">{tmdbError}</p>}

        {tmdbResults.length > 0 && (
          <div className="mt-6 grid gap-4 md:grid-cols-2">
            {tmdbResults.map((movie) => (
              <article
                key={movie.tmdbId}
                className="flex gap-4 rounded-lg border border-slate-800 bg-slate-950/60 p-4"
              >
                {movie.posterUrl ? (
                  <img
                    src={movie.posterUrl}
                    alt=""
                    className="h-36 w-24 shrink-0 rounded-md object-cover"
                  />
                ) : (
                  <div className="flex h-36 w-24 shrink-0 items-center justify-center rounded-md bg-slate-800 text-xs text-slate-500">
                    No poster
                  </div>
                )}

                <div className="min-w-0 flex-1">
                  <h3 className="font-medium text-white">{movie.title}</h3>

                  <p className="mt-1 text-xs text-slate-500">
                    {movie.releaseDate ?? "Release date unavailable"}
                  </p>

                  <p className="mt-3 line-clamp-3 text-sm text-slate-400">
                    {movie.overview || "No overview available."}
                  </p>

                  <button
                    type="button"
                    onClick={() => void handleTmdbImport(movie.tmdbId)}
                    disabled={importingTmdbId !== null}
                    className="mt-4 rounded-md border border-slate-700 px-3 py-1.5 text-xs font-medium text-slate-200 transition hover:border-slate-500 hover:text-white disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    {importingTmdbId === movie.tmdbId
                      ? "Importing..."
                      : "Import"}
                  </button>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>

      {editingMovie && editRequest && (
        <section className="mt-8 rounded-xl border border-slate-800 bg-slate-900/50 p-6">
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="text-sm font-medium uppercase tracking-[0.2em] text-slate-400">
                Edit movie
              </p>

              <h2 className="mt-2 text-2xl font-semibold text-white">
                {editingMovie.title}
              </h2>
            </div>

            <button
              type="button"
              onClick={handleCancelEdit}
              disabled={isSavingEdit}
              className="text-sm text-slate-400 transition hover:text-white disabled:opacity-50"
            >
              Cancel
            </button>
          </div>

          <div className="mt-6 grid gap-6 md:grid-cols-2">
            <label className="grid gap-2 text-sm text-slate-300">
              Title
              <input
                type="text"
                value={editRequest.title}
                onChange={(event) =>
                  handleEditFieldChange("title", event.target.value)
                }
                className="rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white outline-none focus:border-slate-500"
              />
            </label>

            <label className="grid gap-2 text-sm text-slate-300">
              Duration
              <input
                type="number"
                min="1"
                value={editRequest.durationMinutes}
                onChange={(event) =>
                  handleEditFieldChange("durationMinutes", event.target.value)
                }
                className="rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white outline-none focus:border-slate-500"
              />
            </label>

            <label className="grid gap-2 text-sm text-slate-300 md:col-span-2">
              Description
              <textarea
                rows={5}
                value={editRequest.description}
                onChange={(event) =>
                  handleEditFieldChange("description", event.target.value)
                }
                className="rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white outline-none focus:border-slate-500"
              />
            </label>

            <label className="grid gap-2 text-sm text-slate-300 md:col-span-2">
              Poster URL
              <input
                type="url"
                value={editRequest.posterUrl ?? ""}
                onChange={(event) =>
                  handleEditFieldChange("posterUrl", event.target.value)
                }
                className="rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white outline-none focus:border-slate-500"
              />
            </label>
          </div>

          <fieldset className="mt-6">
            <legend className="text-sm font-medium text-slate-300">
              Genres
            </legend>

            <div className="mt-3 flex flex-wrap gap-3">
              {genres.map((genre) => (
                <label
                  key={genre.id}
                  className="flex items-center gap-2 rounded-md border border-slate-700 px-3 py-2 text-sm text-slate-300"
                >
                  <input
                    type="checkbox"
                    checked={editRequest.genreIds.includes(genre.id)}
                    onChange={() => handleGenreToggle(genre.id)}
                  />
                  {genre.name}
                </label>
              ))}
            </div>
          </fieldset>

          <div className="mt-6 flex gap-3">
            <button
              type="button"
              onClick={() => void handleSaveEdit()}
              disabled={isSavingEdit}
              className="rounded-md bg-white px-4 py-2 text-sm font-medium text-slate-950 transition hover:bg-slate-200 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {isSavingEdit ? "Saving..." : "Save changes"}
            </button>

            <button
              type="button"
              onClick={handleCancelEdit}
              disabled={isSavingEdit}
              className="rounded-md border border-slate-700 px-4 py-2 text-sm font-medium text-slate-200 transition hover:border-slate-500 hover:text-white disabled:opacity-50"
            >
              Cancel
            </button>
          </div>
        </section>
      )}

      {actionError && (
        <p className="mt-6 text-sm text-red-400">{actionError}</p>
      )}

      <section className="mt-8 overflow-hidden rounded-xl border border-slate-800 bg-slate-900/50">
        {isLoading && (
          <p className="p-6 text-sm text-slate-400">Loading movies...</p>
        )}

        {!isLoading && error && (
          <p className="p-6 text-sm text-red-400">{error}</p>
        )}

        {!isLoading && !error && movies.length === 0 && (
          <p className="p-6 text-sm text-slate-400">No movies are available.</p>
        )}

        {!isLoading && !error && movies.length > 0 && (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-800 text-left text-sm">
              <thead className="bg-slate-900">
                <tr className="text-slate-400">
                  <th className="px-6 py-3 font-medium">Movie</th>
                  <th className="px-6 py-3 font-medium">Duration</th>
                  <th className="px-6 py-3 font-medium">Genres</th>
                  <th className="px-6 py-3 font-medium">Featured</th>
                  <th className="px-6 py-3 font-medium">Actions</th>
                </tr>
              </thead>

              <tbody className="divide-y divide-slate-800">
                {movies.map((movie) => (
                  <tr key={movie.id}>
                    <td className="px-6 py-4 font-medium text-white">
                      {movie.title}
                    </td>

                    <td className="px-6 py-4 text-slate-300">
                      {movie.durationMinutes} min
                    </td>

                    <td className="px-6 py-4 text-slate-300">
                      {movie.genres.length > 0
                        ? movie.genres.map((genre) => genre.name).join(", ")
                        : "—"}
                    </td>

                    <td className="px-6 py-4">
                      {movie.isFeatured ? (
                        <span className="rounded-full bg-amber-400/10 px-2.5 py-1 text-xs font-medium text-amber-300">
                          Featured
                        </span>
                      ) : (
                        <span className="text-slate-500">—</span>
                      )}
                    </td>
                    <td className="px-6 py-4">
                      <div className="flex flex-wrap gap-2">
                        {movie.isFeatured ? (
                          <span className="self-center text-xs text-slate-500">
                            Current feature
                          </span>
                        ) : (
                          <button
                            type="button"
                            onClick={() => void handleSetFeatured(movie.id)}
                            disabled={
                              loadingEditMovieId !== null ||
                              updatingMovieId !== null ||
                              archivingMovieId !== null
                            }
                            className="rounded-md border border-slate-700 px-3 py-1.5 text-xs font-medium text-slate-200 transition hover:border-slate-500 hover:text-white disabled:cursor-not-allowed disabled:opacity-50"
                          >
                            {updatingMovieId === movie.id
                              ? "Updating..."
                              : "Set featured"}
                          </button>
                        )}
                        <button
                          type="button"
                          onClick={() => void handleEditMovie(movie.id)}
                          disabled={
                            loadingEditMovieId !== null ||
                            updatingMovieId !== null ||
                            archivingMovieId !== null
                          }
                          className="rounded-md border border-slate-700 px-3 py-1.5 text-xs font-medium text-slate-200 transition hover:border-slate-500 hover:text-white disabled:cursor-not-allowed disabled:opacity-50"
                        >
                          {loadingEditMovieId === movie.id
                            ? "Loading..."
                            : "Edit"}
                        </button>
                        <button
                          type="button"
                          onClick={() =>
                            void handleArchiveMovie(movie.id, movie.title)
                          }
                          disabled={
                            loadingEditMovieId !== null ||
                            updatingMovieId !== null ||
                            archivingMovieId !== null
                          }
                          className="rounded-md border border-red-900/70 px-3 py-1.5 text-xs font-medium text-red-300 transition hover:border-red-700 hover:text-red-200 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                          {archivingMovieId === movie.id
                            ? "Archiving..."
                            : "Archive"}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  );
}
