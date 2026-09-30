import type { Reservation } from '../types/reservation'

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5084'

export async function getMyReservations(
  accessToken: string,
  signal?: AbortSignal,
): Promise<Reservation[]> {
  const response = await fetch(`${API_BASE_URL}/api/reservations`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
    signal,
  })

  if (!response.ok) {
    throw new Error(
      `Unable to load reservations. Status: ${response.status}.`,
    )
  }

  return (await response.json()) as Reservation[]
}