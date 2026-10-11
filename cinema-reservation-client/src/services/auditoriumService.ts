import type { AuditoriumSummary } from "../types/auditorium"

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5084"

export async function getAuditoriums(
  signal?: AbortSignal,
): Promise<AuditoriumSummary[]> {
  const response = await fetch(
    `${API_BASE_URL}/api/auditoriums`,
    {
      signal,
    },
  )

  if (!response.ok) {
    throw new Error(
      `Unable to load auditoriums. Status: ${response.status}.`,
    )
  }

  return (await response.json()) as AuditoriumSummary[]
}
