import { createBrowserRouter } from "react-router";

import AdminRoute from "../components/AdminRoute";
import ProtectedRoute from "../components/ProtectedRoute";
import AppLayout from "../layouts/AppLayout";
import AdminMoviesPage from "../pages/AdminMoviesPage";
import HomePage from "../pages/HomePage";
import MovieShowtimesPage from "../pages/MovieShowtimesPage";
import LoginPage from "../pages/LoginPage";
import MyReservationsPage from "../pages/MyReservationsPage";
import RegisterPage from "../pages/RegisterPage";
import AdminShowtimesPage from "../pages/AdminShowtimesPage";

export const router = createBrowserRouter([
  {
    path: "/",
    element: <AppLayout />,
    children: [
      {
        index: true,
        element: <HomePage />,
      },
      {
        path: "movies/:movieId/showtimes",
        element: <MovieShowtimesPage />,
      },
      {
        path: "login",
        element: <LoginPage />,
      },
      {
        path: "register",
        element: <RegisterPage />,
      },
      {
        element: <ProtectedRoute />,
        children: [
          {
            path: "reservations",
            element: <MyReservationsPage />,
          },
        ],
      },
      {
        element: <AdminRoute />,
        children: [
          {
            path: "admin/movies",
            element: <AdminMoviesPage />,
          },
          {
            path: "admin/showtimes",
            element: <AdminShowtimesPage />,
          },
        ],
      },
    ],
  },
]);
