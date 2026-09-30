import { useState, type ReactNode } from 'react'

import { login as loginRequest } from '../services/authService'
import {
  clearAccessToken,
  getAccessToken,
  setAccessToken,
} from '../services/authStorage'
import type { LoginRequest } from '../types/auth'
import { AuthContext } from './AuthContext'

interface AuthProviderProps {
  children: ReactNode
}

export default function AuthProvider({ children }: AuthProviderProps) {
  const [accessToken, setCurrentAccessToken] = useState<string | null>(
    () => getAccessToken(),
  )

  async function login(request: LoginRequest): Promise<void> {
    const response = await loginRequest(request)

    setAccessToken(response.accessToken)
    setCurrentAccessToken(response.accessToken)
  }

  function logout(): void {
    clearAccessToken()
    setCurrentAccessToken(null)
  }

  return (
    <AuthContext.Provider
      value={{
        accessToken,
        isAuthenticated: accessToken !== null,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}