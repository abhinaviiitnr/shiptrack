import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "../api";
import { useAuth } from "../auth";
import { AuthCard } from "./LoginPage";

export function RegisterPage() {
  const [email, setEmail] = useState(""); const [password, setPassword] = useState(""); const [error, setError] = useState("");
  const { signIn } = useAuth(); const navigate = useNavigate();
  async function submit(event: React.FormEvent) {
    event.preventDefault(); setError("");
    try { signIn(await api.register(email, password)); navigate("/"); }
    catch (err) { setError(err instanceof Error ? err.message : "Unable to register"); }
  }
  return <AuthCard title="Create your account" subtitle="Track every order from one place.">
    <form onSubmit={submit}>{error && <p className="error">{error}</p>}
      <label>Email<input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} /></label>
      <label>Password<input type="password" minLength={8} required value={password} onChange={(e) => setPassword(e.target.value)} /></label>
      <button className="primary full" type="submit">Create account</button>
    </form><p className="muted center">Already registered? <Link to="/login">Sign in</Link></p>
  </AuthCard>;
}
