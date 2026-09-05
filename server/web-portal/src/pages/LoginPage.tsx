import { useRef, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { login, register } from "../api/auth";
import { ApiError } from "../api/client";
import { useAuth } from "../state/AuthContext";
import { HomeArena } from "./HomeArena";
import "./home.css";

export function LoginPage() {
  const [mode, setMode] = useState<"login" | "register">("login");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const { signIn } = useAuth();
  const navigate = useNavigate();

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (submitting.current) return;
    submitting.current = true;
    setError(null);
    setBusy(true);
    try {
      const teacher = mode === "login" ? await login(username, password) : await register(username, password, displayName);
      signIn(teacher);
      navigate("/");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not connect. Please check your connection and try again.");
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  }

  return (
    <div className="page home-login-page">
      <div className="home-login-layout">
        <section className="home-login-story" aria-label="QuizBattle for teachers">
          <p className="eyebrow">QuizBattle / Classroom tournaments</p>
          <p className="home-login-headline">Big classroom <br />energy. <br /><em>Real learning.</em></p>
          <p className="home-login-intro">Give your next lesson a competitive streak. Bring your questions, gather your class, and let the learning play out.</p>
          <HomeArena />
          <div className="home-login-story-footer"><span>Your lesson. Their arena.</span><span aria-hidden="true">PLAY TO LEARN</span></div>
        </section>

        <section className="home-login-panel" aria-labelledby="login-title">
          <div className="home-login-form-wrap">
            <span className="home-kicker">The teacher's corner</span>
            <h1 id="login-title">{mode === "login" ? "Welcome back." : "Make room for play."}</h1>
            <p className="home-login-description">{mode === "login" ? "Your next great classroom moment starts here." : "Create your teacher account and get your first round ready."}</p>
            <form onSubmit={onSubmit} aria-busy={busy}>
              <div className="field">
                <label htmlFor="username">Username</label>
                <input id="username" name="username" autoComplete="username" autoCapitalize="none" spellCheck={false} value={username} onChange={(e) => setUsername(e.target.value)} required disabled={busy} />
              </div>
              {mode === "register" && (
                <div className="field">
                  <label htmlFor="displayName">Display name</label>
                  <input id="displayName" name="displayName" autoComplete="name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} required disabled={busy} />
                </div>
              )}
              <div className="field">
                <label htmlFor="password">Password</label>
                <input id="password" name="password" type="password" autoComplete={mode === "login" ? "current-password" : "new-password"} value={password} onChange={(e) => setPassword(e.target.value)} required minLength={6} disabled={busy} aria-describedby={mode === "register" ? "password-help" : undefined} />
                {mode === "register" && <span className="home-field-help" id="password-help">Use at least 6 characters.</span>}
              </div>
              {error && <p className="home-error" role="alert">{error}</p>}
              <button className="btn btn-primary home-login-submit" type="submit" disabled={busy}>
                {busy ? "Please wait..." : mode === "login" ? "Log in" : "Create account"}<span aria-hidden="true">&rarr;</span>
              </button>
              <span className="sr-only" role="status">{busy ? mode === "login" ? "Logging in." : "Creating your account." : ""}</span>
            </form>
            <p className="home-login-switch">
              {mode === "login" ? "New to QuizBattle?" : "Already have an account?"}{" "}
              <button type="button" className="home-text-link" disabled={busy} onClick={() => { setMode(mode === "login" ? "register" : "login"); setError(null); setPassword(""); }}>
                {mode === "login" ? "Create an account" : "Log in"}
              </button>
            </p>
            <div className="home-student-entry"><span>Here to play?</span><a className="home-text-link" href="/play/">Students join here <span aria-hidden="true">&rarr;</span></a><p>Use the class and match codes from your teacher.</p></div>
          </div>
        </section>
      </div>
    </div>
  );
}
