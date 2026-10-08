interface JwtPayload {
  exp?: number
  role?: string | string[]
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"?:
    | string
    | string[]
}

function decodeBase64Url(value: string): string {
  const base64 = value.replace(/-/g, "+").replace(/_/g, "/")
  const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, "=")

  return atob(padded)
}

export function getRolesFromAccessToken(
  accessToken: string | null,
): string[] {
  if (!accessToken) {
    return []
  }

  try {
    const parts = accessToken.split(".")

    if (parts.length !== 3) {
      return []
    }

    const payload = JSON.parse(
      decodeBase64Url(parts[1]),
    ) as JwtPayload

    const roleClaim =
      payload.role ??
      payload[
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
      ]

    if (!roleClaim) {
      return []
    }

    return Array.isArray(roleClaim)
      ? roleClaim
      : [roleClaim]
  } catch {
    return []
  }
}
export function isAccessTokenExpired(
  accessToken: string | null,
): boolean {
  if (!accessToken) {
    return true
  }

  try {
    const parts = accessToken.split(".")

    if (parts.length !== 3) {
      return true
    }

    const payload = JSON.parse(
      decodeBase64Url(parts[1]),
    ) as JwtPayload

    if (payload.exp === undefined) {
      return true
    }

    return payload.exp * 1000 <= Date.now()
  } catch {
    return true
  }
}