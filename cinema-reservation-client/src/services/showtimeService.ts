import type {
  CreateShowtimeRequest,
  Showtime,
  UpdateShowtimeRequest,
} from "../types/showtime"

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5084"

export interface GetShowtimesOptions {
  startsFrom?: string
  startsTo?: string
  movieId?: number
  auditoriumId?: number
  signal?: AbortSignal
}

async function getApiErrorMessage(
  response: Response,
  fallbackMessage: string,
): Promise<string> {
  try {
    const body = (await response.json()) as {
      message?: string
      title?: string
    }

    return body.message ?? body.title ?? fallbackMessage
  } catch {
    return fallbackMessage
  }
}

export async function getShowtimes(
  options: GetShowtimesOptions = {},
): Promise<Showtime[]> {
  const searchParams = new URLSearchParams()

  if (options.startsFrom) {
    searchParams.set("startsFrom", options.startsFrom)
  }

  if (options.startsTo) {
    searchParams.set("startsTo", options.startsTo)
  }

  if (options.movieId !== undefined) {
    searchParams.set("movieId", options.movieId.toString())
  }

  if (options.auditoriumId !== undefined) {
    searchParams.set(
      "auditoriumId",
      options.auditoriumId.toString(),
    )
  }

  const queryString = searchParams.toString()

  const url = `${API_BASE_URL}/api/showtimes${
    queryString ? `?${queryString}` : ""
  }`

  const response = await fetch(url, {
    signal: options.signal,
  })

  if (!response.ok) {
    throw new Error(
      await getApiErrorMessage(
        response,
        `Unable to load showtimes. Status: ${response.status}.`,
      ),
    )
  }

  return (await response.json()) as Showtime[]
}

export async function getShowtimeById(
  id: number,
  signal?: AbortSignal,
): Promise<Showtime> {
  const response = await fetch(
    `${API_BASE_URL}/api/showtimes/${id}`,
    {
      signal,
    },
  )

  if (!response.ok) {
    throw new Error(
      await getApiErrorMessage(
        response,
        `Unable to load showtime. Status: ${response.status}.`,
      ),
    )
  }

  return (await response.json()) as Showtime
}

export async function createShowtime(
  request: CreateShowtimeRequest,
  accessToken: string,
): Promise<Showtime> {
  const response = await fetch(
    `${API_BASE_URL}/api/showtimes`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${accessToken}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
    },
  )

  if (!response.ok) {
    throw new Error(
      await getApiErrorMessage(
        response,
        `Unable to create showtime. Status: ${response.status}.`,
      ),
    )
  }

  return (await response.json()) as Showtime
}

export async function updateShowtime(
  id: number,
  request: UpdateShowtimeRequest,
  accessToken: string,
): Promise<Showtime> {
  const response = await fetch(
    `${API_BASE_URL}/api/showtimes/${id}`,
    {
      method: "PUT",
      headers: {
        Authorization: `Bearer ${accessToken}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
    },
  )

  if (!response.ok) {
    throw new Error(
      await getApiErrorMessage(
        response,
        `Unable to update showtime. Status: ${response.status}.`,
      ),
    )
  }

  return (await response.json()) as Showtime
}

export async function cancelShowtime(
  id: number,
  accessToken: string,
): Promise<void> {
  const response = await fetch(
    `${API_BASE_URL}/api/showtimes/${id}`,
    {
      method: "DELETE",
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    },
  )

  if (!response.ok) {
    throw new Error(
      await getApiErrorMessage(
        response,
        `Unable to cancel showtime. Status: ${response.status}.`,
      ),
    )
  }
}
