import { NavLink, Outlet, useNavigate } from "react-router";

import { useAuth } from "../hooks/useAuth";

export default function AppLayout() {
  const navigate = useNavigate();
  const { isAuthenticated, isAdmin, logout } = useAuth();

  function handleLogout() {
    logout();
    navigate("/");
  }

  return (
    <div className="min-h-screen bg-slate-950 text-white">
      <header className="border-b border-slate-800">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <NavLink to="/" className="text-lg font-semibold tracking-tight">
            Cinema Reservation
          </NavLink>

          <nav className="flex items-center gap-6 text-sm text-slate-300">
            <NavLink
              to="/"
              className={({ isActive }) =>
                isActive ? "text-white" : "transition hover:text-white"
              }
            >
              Home
            </NavLink>

            {isAuthenticated ? (
              <>
                {isAdmin && (
                  <>
                    <NavLink
                      to="/admin/movies"
                      className={({ isActive }) =>
                        isActive ? "text-white" : "transition hover:text-white"
                      }
                    >
                      Movies
                    </NavLink>

                    <NavLink
                      to="/admin/showtimes"
                      className={({ isActive }) =>
                        isActive ? "text-white" : "transition hover:text-white"
                      }
                    >
                      Showtimes
                    </NavLink>
                  </>
                )}

                <NavLink
                  to="/reservations"
                  className={({ isActive }) =>
                    isActive ? "text-white" : "transition hover:text-white"
                  }
                >
                  My Reservations
                </NavLink>

                <button
                  type="button"
                  onClick={handleLogout}
                  className="transition hover:text-white"
                >
                  Logout
                </button>
              </>
            ) : (
              <>
                <NavLink
                  to="/register"
                  className={({ isActive }) =>
                    isActive ? "text-white" : "transition hover:text-white"
                  }
                >
                  Register
                </NavLink>

                <NavLink
                  to="/login"
                  className={({ isActive }) =>
                    isActive ? "text-white" : "transition hover:text-white"
                  }
                >
                  Sign in
                </NavLink>
              </>
            )}
          </nav>
        </div>
      </header>

      <Outlet />
    </div>
  );
}
