import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { listClasses, type ClassRoster } from "../api/classes";
import { listQuestionBanks, listQuestions, type QuestionBank } from "../api/questionBanks";
import { createMatch, listMatches, type MatchWithClassName } from "../api/matches";
import { ApiError } from "../api/client";

function formatDate(ms: number): string {
  return new Date(ms).toLocaleString();
}

export function MatchSetupPage() {
  const navigate = useNavigate();
  const [classes, setClasses] = useState<ClassRoster[]>([]);
  const [banks, setBanks] = useState<QuestionBank[]>([]);
  const [classId, setClassId] = useState<number | "">("");
  const [bankId, setBankId] = useState<number | "">("");
  const [mode, setMode] = useState<"ffa" | "teams">("ffa");
  const [timerSeconds, setTimerSeconds] = useState<number>(30);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loadAttempt, setLoadAttempt] = useState(0);
  const [submitting, setSubmitting] = useState(false);
  const submittingRef = useRef(false);
  const [recentMatches, setRecentMatches] = useState<MatchWithClassName[]>([]);
  const [matchesError, setMatchesError] = useState<string | null>(null);
  const [matchesLoading, setMatchesLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  function refreshMatches() {
    setMatchesLoading(true);
    setMatchesError(null);
    listMatches().then(setRecentMatches)
      .catch((err) => setMatchesError(`Could not load recent matches: ${err instanceof Error ? err.message : String(err)}`))
      .finally(() => setMatchesLoading(false));
  }

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setLoadError(null);
    Promise.all([listClasses(), listQuestionBanks()]).then(([loadedClasses, loadedBanks]) => {
      if (cancelled) return;
      setClasses(loadedClasses);
      setBanks(loadedBanks);
      if (loadedClasses.length === 1) setClassId(loadedClasses[0].id);
      if (loadedBanks.length === 1) setBankId(loadedBanks[0].id);
    }).catch((err) => {
      if (!cancelled) setLoadError(`Could not load classes and question banks: ${err instanceof Error ? err.message : String(err)}`);
    }).finally(() => {
      if (!cancelled) setLoading(false);
    });
    return () => { cancelled = true; };
  }, [loadAttempt]);

  useEffect(() => {
    refreshMatches();
    return () => { submittingRef.current = false; };
  }, []);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (submittingRef.current || loading || loadError) return;
    setError(null);
    if (!classId || !bankId) {
      setError("Pick a class and a question bank first.");
      return;
    }
    submittingRef.current = true;
    setSubmitting(true);
    try {
      const questions = await listQuestions(Number(bankId));
      if (!submittingRef.current) return;
      if (questions.length === 0) {
        setError("This question bank is empty. Add at least one question before creating a match.");
        return;
      }
      const match = await createMatch(Number(classId), Number(bankId), mode, timerSeconds);
      if (!submittingRef.current) return;
      navigate(`/live-match?matchId=${match.id}`);
    } catch (err) {
      if (submittingRef.current) setError(err instanceof ApiError ? err.message : String(err));
    } finally {
      if (submittingRef.current) {
        submittingRef.current = false;
        setSubmitting(false);
      }
    }
  }

  return (
    <div className="page" style={{ maxWidth: 700 }}>
      <h1>Match Setup</h1>

      <p className="muted">Create a lobby, invite your students, then start the match when they are ready.</p>
      {loading && <p role="status">Loading classes and question banks...</p>}
      {loadError && <div role="alert"><p className="error-text">{loadError}</p><button className="btn" onClick={() => setLoadAttempt((n) => n + 1)}>Retry loading</button></div>}
      {!loading && !loadError && classes.length === 0 && (
        <p className="muted">You need at least one <Link to="/rosters">class</Link> before starting a match.</p>
      )}
      {!loading && !loadError && banks.length === 0 && (
        <p className="muted">You need at least one <Link to="/question-banks">question bank</Link> before starting a match.</p>
      )}

      <form onSubmit={onSubmit} className="card">
        <fieldset disabled={loading || !!loadError || submitting} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
        <div className="field">
          <label htmlFor="class">Class</label>
          <select id="class" value={classId} onChange={(e) => setClassId(e.target.value ? Number(e.target.value) : "")}>
            <option value="">Select a class…</option>
            {classes.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="bank">Question bank</label>
          <select id="bank" value={bankId} onChange={(e) => setBankId(e.target.value ? Number(e.target.value) : "")}>
            <option value="">Select a question bank…</option>
            {banks.map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>Mode</label>
          <div className="row">
            <label className="row"><input type="radio" checked={mode === "ffa"} onChange={() => setMode("ffa")} /> Free-for-all</label>
            <label className="row"><input type="radio" checked={mode === "teams"} onChange={() => setMode("teams")} /> Teams</label>
          </div>
          {mode === "teams" && (
            <p className="muted">Students pick Team A or Team B themselves in the app's lobby before the match starts — no roster assignment needed here.</p>
          )}
        </div>
        <div className="field">
          <label htmlFor="timer">Finish Countdown Timer (for 3+ players)</label>
          <select id="timer" value={timerSeconds} onChange={(e) => setTimerSeconds(Number(e.target.value))}>
            <option value={15}>15 seconds (Speed blitz)</option>
            <option value={30}>30 seconds (Standard)</option>
            <option value={45}>45 seconds (Relaxed)</option>
            <option value={60}>60 seconds (1 minute)</option>
            <option value={90}>90 seconds (1.5 minutes)</option>
          </select>
          <p className="muted" style={{ fontSize: "0.85em", marginTop: 4 }}>
            In 3+ player games, when 1st place finishes, this countdown timer gives remaining players time to race for 2nd and 3rd place. (2-player games end immediately upon 1st finish).
          </p>
        </div>
        {error && <p className="error-text" role="alert">{error}</p>}
        <button className="btn btn-primary" type="submit" disabled={!classId || !bankId}>
          {submitting ? "Creating lobby..." : "Create & open lobby"}
        </button>
        </fieldset>
      </form>

      <div className="card">
        <div className="row" style={{ justifyContent: "space-between" }}>
          <h2>Recent matches</h2>
          <button className="btn" onClick={refreshMatches} disabled={matchesLoading}>Refresh</button>
        </div>
        {matchesError && <p className="error-text" role="alert">{matchesError}</p>}
        {matchesLoading ? <p role="status">Loading recent matches...</p> : matchesError ? null : recentMatches.length === 0 ? (
          <p className="muted">No matches yet — create one above.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Match ID</th>
                <th>Class</th>
                <th>Mode</th>
                <th>Status</th>
                <th>Join code</th>
                <th>Created</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {recentMatches.map((m) => (
                <tr key={m.id}>
                  <td>{m.id}</td>
                  <td>{m.class_name}</td>
                  <td>{m.mode}</td>
                  <td>{m.status}</td>
                  <td><span className="badge">{m.join_code}</span></td>
                  <td>{formatDate(m.created_at)}</td>
                  <td><Link className="btn" to={`/live-match?matchId=${m.id}`}>Live Monitor</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
