import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'

import { register } from '../services/authService'

export default function RegisterPage() {
  const navigate = useNavigate()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    setError(null)

    if (password !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }

    setIsSubmitting(true)

    try {
      await register({
        email,
        password,
      })

      navigate('/login', {
        replace: true,
        state: {
          registrationSucceeded: true,
        },
      })
    } catch (error) {
      setError(
        error instanceof Error
          ? error.message
          : 'Unable to create your account.',
      )
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="mx-auto max-w-md px-6 py-16">
      <h1 className="text-3xl font-bold">
        Create account
      </h1>

      <p className="mt-2 text-slate-400">
        Register to reserve cinema seats.
      </p>

      <form
        onSubmit={handleSubmit}
        className="mt-8 space-y-6"
      >
        <div>
          <label
            htmlFor="email"
            className="block text-sm font-medium"
          >
            Email
          </label>

          <input
            id="email"
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className="mt-2 w-full rounded-md border border-slate-700 bg-slate-900 px-3 py-2 outline-none focus:border-slate-500"
          />
        </div>

        <div>
          <label
            htmlFor="password"
            className="block text-sm font-medium"
          >
            Password
          </label>

          <input
            id="password"
            type="password"
            autoComplete="new-password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className="mt-2 w-full rounded-md border border-slate-700 bg-slate-900 px-3 py-2 outline-none focus:border-slate-500"
          />
        </div>

        <div>
          <label
            htmlFor="confirm-password"
            className="block text-sm font-medium"
          >
            Confirm password
          </label>

          <input
            id="confirm-password"
            type="password"
            autoComplete="new-password"
            required
            value={confirmPassword}
            onChange={(event) =>
              setConfirmPassword(event.target.value)
            }
            className="mt-2 w-full rounded-md border border-slate-700 bg-slate-900 px-3 py-2 outline-none focus:border-slate-500"
          />
        </div>

        {error && (
          <p
            role="alert"
            className="text-sm text-red-400"
          >
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={isSubmitting}
          className="w-full rounded-md bg-white px-4 py-2 font-medium text-slate-950 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isSubmitting
            ? 'Creating account...'
            : 'Create account'}
        </button>
      </form>

      <p className="mt-6 text-sm text-slate-400">
        Already have an account?{' '}
        <Link
          to="/login"
          className="text-white underline"
        >
          Sign in
        </Link>
      </p>
    </main>
  )
}