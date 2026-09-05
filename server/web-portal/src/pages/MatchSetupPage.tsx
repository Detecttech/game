import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { listClasses, type ClassRoster } from "../api/classes";
import { listQuestionBanks, listQuestions, type QuestionBank } from "../api/questionBanks";
import { createMatch, listMatches, type MatchWithClassName } from "../api/matches";
import { ApiError } from "../api/client";
import "./resources.css";

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
    <div className="page resource-page">
      <header className="page-heading resource-heading">
        <div>
          <p className="eyebrow">Prepare / Match setup</p>
          <h1>Set the stage.</h1>
          <p className="resource-intro">Choose the class, bring the questions, and make it a tournament. You'll start the race when everyone is ready.</p>
        </div>
      </header>
      {loading && <p className="resource-status" role="status">Loading classes and question banks...</p>}
      {loadError && <div className="resource-error" role="alert"><p>{loadError}</p><button className="btn" onClick={() => setLoadAttempt((n) => n + 1)}>Retry loading</button></div>}
      {!loading && !loadError && classes.length === 0 && (
        <p className="resource-note">First, <Link to="/rosters">create a class</Link> for your players. Then return here to open a lobby.</p>
      )}
      {!loading && !loadError && banks.length === 0 && (
        <p className="resource-note">You'll need a <Link to="/question-banks">question bank</Link> with at least one question before creating a match.</p>
      )}

      <form onSubmit={onSubmit} className="card resource-setup">
        <fieldset disabled={loading || !!loadError || submitting} className="resource-fieldset">
          <legend className="sr-only">Match settings</legend>
          <section className="resource-setup-section" aria-labelledby="setup-resources">
            <div className="resource-step-heading">
              <span className="resource-step-number" aria-hidden="true">01</span>
              <h2 id="setup-resources">Class &amp; questions</h2>
              <p className="resource-detail">Pick who plays and what they'll learn.</p>
            </div>
            <div className="resource-setup-fields">
              <div className="field">
                <label htmlFor="class">Class</label>
                <select id="class" value={classId} onChange={(e) => setClassId(e.target.value ? Number(e.target.value) : "")}>
                  <option value="">Select a class...</option>
                  {classes.map((c) => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label htmlFor="bank">Question bank</label>
                <select id="bank" aria-describedby="bank-help" value={bankId} onChange={(e) => setBankId(e.target.value ? Number(e.target.value) : "")}>
                  <option value="">Select a question bank...</option>
                  {banks.map((b) => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </select>
                <p className="resource-detail" id="bank-help">We'll check that this bank has questions before opening the lobby.</p>
              </div>
            </div>
          </section>
          <section className="resource-setup-section" aria-labelledby="setup-mode">
            <div className="resource-step-heading">
              <span className="resource-step-number" aria-hidden="true">02</span>
              <h2 id="setup-mode">Choose the format</h2>
              <p className="resource-detail">Individual competition or a team effort.</p>
            </div>
            <fieldset className="resource-fieldset">
              <legend className="resource-field-label">Match mode</legend>
              <div className="resource-mode-options">
                <label className={`resource-mode-option${mode === "ffa" ? " is-selected" : ""}`}>
                  <input type="radio" name="match-mode" checked={mode === "ffa"} onChange={() => setMode("ffa")} />
                  <span><strong>Free-for-all</strong><span>Every player races for their own finish.</span></span>
                </label>
                <label className={`resource-mode-option${mode === "teams" ? " is-selected" : ""}`}>
                  <input type="radio" name="match-mode" checked={mode === "teams"} onChange={() => setMode("teams")} />
                  <span><strong>Teams</strong><span>Players choose Team A or Team B in the lobby.</span></span>
                </label>
              </div>
              {mode === "teams" && <p className="resource-detail">Students choose their team before the match starts. No roster assignment needed here.</p>}
            </fieldset>
          </section>
          <section className="resource-setup-section" aria-labelledby="setup-timer">
            <div className="resource-step-heading">
              <span className="resource-step-number" aria-hidden="true">03</span>
              <h2 id="setup-timer">The final stretch</h2>
              <p className="resource-detail">Give the rest of the field time to finish.</p>
            </div>
            <div className="field">
              <label htmlFor="timer">Finish countdown (for 3+ players)</label>
              <select id="timer" aria-describedby="timer-help" value={timerSeconds} onChange={(e) => setTimerSeconds(Number(e.target.value))}>
                <option value={15}>15 seconds (Speed blitz)</option>
                <option value={30}>30 seconds (Standard)</option>
                <option value={45}>45 seconds (Relaxed)</option>
                <option value={60}>60 seconds (1 minute)</option>
                <option value={90}>90 seconds (1.5 minutes)</option>
              </select>
              <p className="resource-detail" id="timer-help">In games with 3+ players, this countdown starts when the first player finishes, giving others time to race for second and third. Two-player games end at the first finish.</p>
            </div>
          </section>
          {error && <p className="resource-error" role="alert">{error}</p>}
          <div className="resource-setup-footer">
            <div><strong>Your lobby opens next.</strong><p className="resource-detail">Invite students, check they're ready, then start the match.</p></div>
            <button className="btn btn-primary" type="submit" disabled={!classId || !bankId}>
              {submitting ? "Creating lobby..." : "Create & open lobby"}
            </button>
          </div>
        </fieldset>
      </form>

      <section className="resource-recent" aria-labelledby="recent-matches">
        <div className="section-heading resource-section-heading">
          <div><p className="resource-meta">Match register</p><h2 id="recent-matches">Recent matches</h2></div>
          <button className="btn" onClick={refreshMatches} disabled={matchesLoading}>Refresh</button>
        </div>
        {matchesError && <p className="resource-error" role="alert">{matchesError}</p>}
        {matchesLoading ? <p className="card resource-status" role="status">Loading recent matches...</p> : matchesError ? null : recentMatches.length === 0 ? (
          <div className="empty-state"><h3>The first race is yours to start.</h3><p>Create a lobby above. Your matches will be listed here.</p></div>
        ) : (
          <div className="table-scroll resource-table" role="region" aria-label="Recent matches table" tabIndex={0}>
            <table>
              <thead>
                <tr>
                  <th scope="col">Match</th>
                  <th scope="col">Class</th>
                  <th scope="col">Mode</th>
                  <th scope="col">Status</th>
                  <th scope="col">Join code</th>
                  <th scope="col">Created</th>
                  <th scope="col"><span className="sr-only">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                {recentMatches.map((m) => (
                  <tr key={m.id}>
                    <th scope="row" className="resource-numeric">#{m.id}</th>
                    <td>{m.class_name}</td>
                    <td>{m.mode === "ffa" ? "Free-for-all" : "Teams"}</td>
                    <td><span className="badge">{m.status}</span></td>
                    <td><span className="resource-code">{m.join_code}</span></td>
                    <td>{formatDate(m.created_at)}</td>
                    <td><Link className="btn" to={`/live-match?matchId=${m.id}`}>Live Monitor</Link></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
