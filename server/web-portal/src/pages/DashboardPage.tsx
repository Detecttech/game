import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { listClasses, type ClassRoster } from "../api/classes";
import { listQuestionBanks, type QuestionBank } from "../api/questionBanks";
import { listMatches, type MatchWithClassName } from "../api/matches";
import { apiPost, getToken } from "../api/client";
import { useAuth } from "../state/AuthContext";
import { HomeArena } from "./HomeArena";
import "./home.css";

export function DashboardPage() {
  const { teacher, signOut } = useAuth();
  const [classes, setClasses] = useState<ClassRoster[] | null>(null);
  const [banks, setBanks] = useState<QuestionBank[] | null>(null);
  const [matches, setMatches] = useState<MatchWithClassName[] | null>(null);
  const [loadErrors, setLoadErrors] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [refresh, setRefresh] = useState(0);
  const [busy, setBusy] = useState<"export" | "import" | null>(null);
  const [backupMessage, setBackupMessage] = useState("");
  const [backupError, setBackupError] = useState("");
  const backupBusy = useRef(false);
  const fileInput = useRef<HTMLInputElement>(null);

  useEffect(() => {
    let cancelled = false;
    Promise.allSettled([listClasses(), listQuestionBanks(), listMatches()]).then(([classResult, bankResult, matchResult]) => {
      if (cancelled) return;
      setClasses(classResult.status === "fulfilled" ? classResult.value : null);
      setBanks(bankResult.status === "fulfilled" ? bankResult.value : null);
      setMatches(matchResult.status === "fulfilled" ? matchResult.value : null);
      setLoadErrors([
        ...(classResult.status === "rejected" ? ["classes"] : []),
        ...(bankResult.status === "rejected" ? ["question banks"] : []),
        ...(matchResult.status === "rejected" ? ["recent matches"] : []),
      ]);
      setLoading(false);
    });
    return () => { cancelled = true; };
  }, [refresh]);

  async function exportBackup() {
    if (backupBusy.current) return;
    backupBusy.current = true;
    setBusy("export");
    setBackupError("");
    setBackupMessage("");
    try {
      const token = getToken();
      const res = await fetch("/api/backup/export", {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });
      if (res.status === 401) signOut();
      if (!res.ok) throw new Error(`Export failed (${res.status}). Please try again.`);
      const url = URL.createObjectURL(await res.blob());
      const link = document.createElement("a");
      link.href = url;
      link.download = `quizbattle-backup-${new Date().toISOString().slice(0, 10)}.json`;
      document.body.append(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      setBackupMessage("Backup download started. Keep the file somewhere safe.");
    } catch (error) {
      setBackupError(error instanceof Error ? error.message : "Could not export your backup.");
    } finally {
      backupBusy.current = false;
      setBusy(null);
    }
  }

  async function restoreBackup(file: File) {
    if (backupBusy.current) return;
    backupBusy.current = true;
    setBusy("import");
    setBackupError("");
    setBackupMessage("");
    try {
      const text = await file.text();
      let data;
      try {
        data = JSON.parse(text);
      } catch {
        setBackupError("This file is not valid JSON. Choose a QuizBattle backup file.");
        return;
      }
      const result = await apiPost<{ importedBanks?: number; importedQuestions?: number; importedClasses?: number }>("/backup/import", data);
      setBackupMessage(`Restored ${result.importedBanks ?? 0} banks, ${result.importedQuestions ?? 0} questions, and ${result.importedClasses ?? 0} classes.`);
      setLoading(true);
      setRefresh((value) => value + 1);
    } catch (error) {
      setBackupError(error instanceof SyntaxError ? "The server returned an invalid response. Could not confirm the restore. Check your data before trying again." : error instanceof Error ? error.message : "Could not restore your backup.");
    } finally {
      backupBusy.current = false;
      setBusy(null);
    }
  }

  return (
    <div className="page home-page">
      <div className="home-masthead">
        <p className="eyebrow">Teacher home / The next round starts here</p>
        <p>Welcome{teacher ? `, ${teacher.displayName}` : " back"}.</p>
      </div>

      <section className="home-hero" aria-labelledby="home-title">
        <div className="home-hero-copy">
          <span className="home-kicker">A little rivalry. A lot of learning.</span>
          <h1 id="home-title">Turn a lesson into a <em>showdown.</em></h1>
          <p>Your questions. Your class. One arena. Bring the room together with a live classroom tournament.</p>
          <div className="home-actions">
            <Link className="btn btn-primary" to="/match-setup">Set up a match <span aria-hidden="true">&rarr;</span></Link>
            <Link className="home-text-link" to="/question-banks">Explore question banks</Link>
          </div>
        </div>
        <div className="home-hero-art">
          <HomeArena />
          <span className="home-art-caption">Knowledge is your competitive edge.</span>
        </div>
      </section>

      {loadErrors.length > 0 && (
        <div className="home-notice" role="alert">
          <span>Could not load {loadErrors.join(", ")}.</span>
          <button className="btn" disabled={loading} onClick={() => { setLoading(true); setRefresh((value) => value + 1); }}>
            {loading ? "Retrying..." : "Try again"}
          </button>
        </div>
      )}

      <section className="home-prep" aria-labelledby="home-prep-title">
        <div className="home-section-heading">
          <div><p className="eyebrow">Before the whistle</p><h2 id="home-prep-title">Build your next great round.</h2></div>
          <span className="home-section-note">A little prep goes a long way.</span>
        </div>
        <div className="home-prep-grid">
          <Link className="card home-prep-card" to="/rosters">
            <div className="home-prep-top"><span className="home-step">01 / The players</span><span aria-hidden="true">&#8599;</span></div>
            <div className="home-count" aria-live="polite"><strong>{loading ? "..." : classes?.length ?? "Unavailable"}</strong><span>{!loading && classes?.length === 1 ? "class" : "classes"}</span></div>
            <h3>Get your class together</h3>
            <p>Add students and keep your rosters ready for game day.</p>
            <span className="home-card-action">Manage classes <span aria-hidden="true">&rarr;</span></span>
          </Link>
          <Link className="card home-prep-card home-prep-bank" to="/question-banks">
            <div className="home-prep-top"><span className="home-step">02 / The questions</span><span aria-hidden="true">&#8599;</span></div>
            <div className="home-count" aria-live="polite"><strong>{loading ? "..." : banks?.length ?? "Unavailable"}</strong><span>{!loading && banks?.length === 1 ? "question bank" : "question banks"}</span></div>
            <h3>Make the lesson your own</h3>
            <p>Build a bank of questions worth competing over.</p>
            <span className="home-card-action">Manage question banks <span aria-hidden="true">&rarr;</span></span>
          </Link>
          <div className="card home-game-plan">
            <span className="home-step">03 / The showdown</span>
            <h3>Ready. Set. <br />Think fast.</h3>
            <p>Choose a class and question bank, then share the join code with your students.</p>
            <Link className="btn" to="/match-setup">Prepare a match <span aria-hidden="true">&rarr;</span></Link>
          </div>
        </div>
      </section>

      <section className="card home-matches" aria-labelledby="home-matches-title" aria-busy={loading}>
        <div className="home-section-heading">
          <div><p className="eyebrow">From your arena</p><h2 id="home-matches-title">Recent matches</h2></div>
          <Link className="home-text-link" to="/match-setup">All matches <span aria-hidden="true">&rarr;</span></Link>
        </div>
        {loading ? <p role="status">Loading your matches...</p> : matches === null ? (
          <p>Match history is unavailable. Use Try again above to reload it.</p>
        ) : matches.length === 0 ? (
          <div className="empty-state home-match-empty"><strong>Your first showdown is still ahead.</strong><p>Once you create a match, it will appear here. Let's give your class something to talk about.</p><Link className="home-text-link" to="/match-setup">Set up your first match <span aria-hidden="true">&rarr;</span></Link></div>
        ) : (
          <ul className="home-match-list">
            {[...matches].sort((a, b) => b.created_at - a.created_at || b.id - a.id).slice(0, 4).map((match) => (
              <li key={match.id}>
                <span className="home-match-number" aria-label={`Match ${match.id}`}>#{match.id}</span>
                <div className="home-match-name"><strong>{match.class_name}</strong><span>{match.mode === "teams" ? "Team battle" : "Free for all"} / {new Date(match.created_at).toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" })}</span></div>
                <span className={`home-match-status home-match-status-${match.status}`}>{match.status === "active" ? "In play" : match.status === "lobby" ? "In lobby" : "Completed"}</span>
                <Link className="home-text-link" to={`/live-match?matchId=${match.id}`} aria-label={`View match ${match.id} for ${match.class_name}`}>View <span aria-hidden="true">&rarr;</span></Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      <details className="home-backup">
        <summary>Keep your teaching kit safe <span>Backup &amp; restore</span></summary>
        <div className="home-backup-body">
          <p>Download your questions, question banks, and class rosters as a JSON backup, or import a previously saved file.</p>
          <div className="home-actions" aria-busy={busy !== null}>
            <button className="btn" onClick={exportBackup} disabled={busy !== null}>{busy === "export" ? "Exporting..." : "Export backup (.json)"}</button>
            <button className="btn" onClick={() => fileInput.current?.click()} disabled={busy !== null}>{busy === "import" ? "Restoring..." : "Restore from backup"}</button>
            <input ref={fileInput} type="file" accept=".json,application/json" hidden aria-label="Choose a backup file" disabled={busy !== null} onChange={(event) => { const file = event.target.files?.[0]; event.target.value = ""; if (file) void restoreBackup(file); }} />
          </div>
          <p className="home-backup-feedback" role="status">{backupMessage}</p>
          {backupError && <p className="home-error" role="alert">{backupError}</p>}
        </div>
      </details>
    </div>
  );
}
