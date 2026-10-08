import { createBrowserRouter } from "react-router";

import AdminRoute from "../components/AdminRoute";
import ProtectedRoute from "../components/ProtectedRoute";
import AppLayout from "../layouts/AppLayout";
import AdminMoviesPage from "../pages/AdminMoviesPage";
import HomePage from "../pages/HomePage";
import LoginPage from "../pages/LoginPage";
import MyReservationsPage from "../pages/MyReservationsPage";
import RegisterPage from "../pages/RegisterPage";

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
        ],
      },
    ],
  },
]);