import { useEffect, useState } from "react";
import { listClasses, type ClassRoster } from "../api/classes";
import { fetchLeaderboard, type LeaderboardEntry } from "../api/leaderboard";
import { ApiError } from "../api/client";
import "./resources.css";

export function LeaderboardPage() {
  const [classes, setClasses] = useState<ClassRoster[]>([]);
  const [scope, setScope] = useState<"global" | number>("global");
  const [entries, setEntries] = useState<LeaderboardEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [classesLoading, setClassesLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [classesError, setClassesError] = useState<string | null>(null);
  const [loadAttempt, setLoadAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setClassesLoading(true);
    setClassesError(null);
    listClasses()
      .then((data) => { if (!cancelled) setClasses(data); })
      .catch((e) => { if (!cancelled) setClassesError(e instanceof ApiError ? e.message : String(e)); })
      .finally(() => { if (!cancelled) setClassesLoading(false); });
    return () => { cancelled = true; };
  }, [loadAttempt]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    const scopeParam = scope === "global" ? "global" : (`class:${scope}` as const);
    fetchLeaderboard(scopeParam)
      .then((data) => { if (!cancelled) setEntries(data); })
      .catch((e) => { if (!cancelled) setError(e instanceof ApiError ? e.message : String(e)); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [scope, loadAttempt]);

  const scopeName = scope === "global" ? "All classes" : classes.find((c) => c.id === scope)?.name ?? "Selected class";

  return (
    <div className="page resource-page">
      <header className="page-heading resource-heading">
        <div>
          <p className="eyebrow">Results / Earned XP</p>
          <h1>Every point tells a story.</h1>
          <p className="resource-intro">Follow student progress across your classes, or focus on one roster at a time.</p>
        </div>
      </header>
      <div className="card resource-toolbar">
        <div className="field">
          <label htmlFor="scope">Leaderboard scope</label>
          <select
            id="scope"
            value={scope}
            disabled={classesLoading}
            onChange={(e) => setScope(e.target.value === "global" ? "global" : Number(e.target.value))}
          >
            <option value="global">All classes</option>
            {classes.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </div>
        <a
          href={`/api/leaderboard/export${scope === "global" ? "" : `?classId=${scope}`}`}
          download
          className="btn btn-primary"
        >
          Download XP leaderboard (CSV)
        </a>
      </div>
      {classesLoading && <p className="resource-detail" role="status">Loading class filters...</p>}
      {classesError && <div className="resource-error" role="alert"><p>Could not load class filters: {classesError}</p><button className="btn" onClick={() => setLoadAttempt((n) => n + 1)}>Retry loading</button></div>}
      <div className="section-heading resource-section-heading">
        <div><p className="resource-meta">XP leaderboard</p><h2>{scopeName}</h2></div>
        {!loading && !error && <span className="resource-meta">{entries.length} {entries.length === 1 ? "student" : "students"}</span>}
      </div>
      {loading ? (
        <p className="card resource-status" role="status">Loading XP leaderboard...</p>
      ) : error ? (
        <div className="resource-error" role="alert"><p>Could not load leaderboard: {error}</p><button className="btn" onClick={() => setLoadAttempt((n) => n + 1)}>Retry loading</button></div>
      ) : entries.length === 0 ? (
        <div className="empty-state"><h3>No XP recorded yet.</h3><p>When students earn XP, their progress for {scopeName} will appear here.</p></div>
      ) : (
        <div className="table-scroll resource-table resource-leaderboard" role="region" aria-label={`XP leaderboard: ${scopeName}`} tabIndex={0}>
          <table>
            <thead><tr><th scope="col">Rank</th><th scope="col">Student</th><th scope="col" className="resource-xp">Total XP</th></tr></thead>
            <tbody>
              {entries.map((e, i) => (
                <tr key={e.student_profile_id}>
                  <td className="resource-rank">{String(i + 1).padStart(2, "0")}</td>
                  <th scope="row" className="resource-text-cell">{e.name}</th>
                  <td className="resource-numeric resource-xp">{e.xp_total.toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
