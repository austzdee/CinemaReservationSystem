import { createContext } from 'react'

import type { LoginRequest } from '../types/auth'

export interface AuthContextValue {
  accessToken: string | null
  isAuthenticated: boolean
  login: (request: LoginRequest) => Promise<void>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | undefined>(
  undefined,
)