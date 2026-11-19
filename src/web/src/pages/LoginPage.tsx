import { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { api } from "../api";
import { useAuth } from "../auth";

export function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  async function submit(event: React.FormEvent) {
    event.preventDefault(); setError("");
    try { signIn(await api.login(email, password)); navigate((location.state as { from?: { pathname: string } })?.from?.pathname ?? "/"); }
    catch (err) { setError(err instanceof Error ? err.message : "Unable to sign in"); }
  }
  return <AuthCard title="Welcome back" subtitle="Sign in to manage your shipments.">
    <form onSubmit={submit}>{error && <p className="error">{error}</p>}
      <label>Email<input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} /></label>
      <label>Password<input type="password" required value={password} onChange={(e) => setPassword(e.target.value)} /></label>
      <button className="primary full" type="submit">Sign in</button>
    </form>
    <p className="muted center">New to ShipTrack? <Link to="/register">Create an account</Link></p>
  </AuthCard>;
}

export function AuthCard({ title, subtitle, children }: { title: string; subtitle: string; children: React.ReactNode }) {
  return <section className="auth-card"><div className="eyebrow">SHIPTRACK</div><h1>{title}</h1><p className="muted">{subtitle}</p>{children}</section>;
}
