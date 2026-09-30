import { createBrowserRouter } from "react-router";

import ProtectedRoute from "../components/ProtectedRoute";
import AppLayout from "../layouts/AppLayout";
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
    ],
  },
]);
