import { Link, Navigate, Outlet, Route, Routes, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "./auth";
import { LoginPage } from "./pages/LoginPage";
import { RegisterPage } from "./pages/RegisterPage";
import { ShopPage } from "./pages/ShopPage";
import { OrdersPage } from "./pages/OrdersPage";
import { OrderDetailsPage } from "./pages/OrderDetailsPage";
import { AdminPage } from "./pages/AdminPage";
import { CheckoutPage } from "./pages/CheckoutPage";

function Layout() {
  const { token, role, signOut } = useAuth();
  const navigate = useNavigate();
  return (
    <div className="app-shell">
      <header className="topbar">
        <Link className="brand" to="/">Ship<span>Track</span></Link>
        {token && <nav>
          <Link to="/">Shop</Link>
          <Link to="/orders">My orders</Link>
          {role === "Admin" && <Link to="/admin">Admin</Link>}
          <button className="link-button" onClick={() => { signOut(); navigate("/login"); }}>Sign out</button>
        </nav>}
      </header>
      <main className="content"><Outlet /></main>
    </div>
  );
}

function Protected({ admin = false }: { admin?: boolean }) {
  const { token, role } = useAuth();
  const location = useLocation();
  if (!token) return <Navigate to="/login" state={{ from: location }} replace />;
  if (admin && role !== "Admin") return <Navigate to="/" replace />;
  return <Outlet />;
}

export function App() {
  return <Routes>
    <Route element={<Layout />}>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route element={<Protected />}><Route index element={<ShopPage />} /><Route path="/checkout" element={<CheckoutPage />} /><Route path="/orders" element={<OrdersPage />} /><Route path="/orders/:id" element={<OrderDetailsPage />} /></Route>
      <Route element={<Protected admin />}><Route path="/admin" element={<AdminPage />} /></Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Route>
  </Routes>;
}
