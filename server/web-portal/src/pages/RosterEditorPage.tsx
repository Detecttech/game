import { useEffect, useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { getClass, type ClassRoster } from "../api/classes";
import { addStudent, listRoster, removeStudent, resetStudentPin, type StudentSummary } from "../api/roster";
import { ApiError } from "../api/client";
import "./resources.css";

export function RosterEditorPage() {
  const { id } = useParams();
  const classId = Number(id);
  const [cls, setCls] = useState<ClassRoster | null>(null);
  const [students, setStudents] = useState<StudentSummary[]>([]);
  const [newName, setNewName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loadAttempt, setLoadAttempt] = useState(0);
  const [pending, setPending] = useState(false);

  function refresh() {
    setLoadAttempt((n) => n + 1);
  }

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setLoadError(null);
    Promise.all([getClass(classId), listRoster(classId)])
      .then(([roster, data]) => {
        if (cancelled) return;
        setCls(roster);
        setStudents(data);
      })
      .catch((e) => { if (!cancelled) setLoadError(e instanceof ApiError ? e.message : String(e)); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [classId, loadAttempt]);

  useEffect(() => {
    setCls(null);
    setNewName("");
    setError(null);
  }, [classId]);

  async function onAdd(e: FormEvent) {
    e.preventDefault();
    if (!newName.trim() || pending || loading || loadError) return;
    setError(null);
    setPending(true);
    try {
      await addStudent(classId, newName.trim());
      setNewName("");
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    } finally {
      setPending(false);
    }
  }

  async function onRemove(studentId: number) {
    if (!confirm("Remove this student from the class?")) return;
    setError(null);
    setPending(true);
    try {
      await removeStudent(studentId);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    } finally {
      setPending(false);
    }
  }

  async function onResetPin(studentId: number) {
    setError(null);
    setPending(true);
    try {
      await resetStudentPin(studentId);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="page resource-page">
      <p className="resource-back"><Link to="/rosters">&larr; All classes</Link></p>
      <header className="page-heading resource-heading">
        <div>
          <p className="eyebrow">Resources / Class roster</p>
          <h1>{cls?.name ?? "Class roster"}</h1>
          <p className="resource-intro">Your players, their progress, and everything they need to join.</p>
        </div>
        {cls && <div className="resource-join-code"><span className="resource-meta">Class code</span><strong>{cls.class_code}</strong></div>}
      </header>
      <div className="resource-note">
        <strong>Joining this class</strong>
        <p>Students use the class code, their name from this roster, and a PIN they set on first login. Reset a forgotten PIN so they can choose a new one.</p>
      </div>
      <form onSubmit={onAdd} className="card resource-create">
        <div className="field">
          <label htmlFor="student-name">Student name</label>
          <input id="student-name" placeholder="Name students will use to sign in" required value={newName} onChange={(e) => setNewName(e.target.value)} disabled={pending || loading || !!loadError} />
        </div>
        <button className="btn btn-primary" type="submit" disabled={pending || loading || !!loadError || !newName.trim()}>{pending ? "Working..." : "Add student"}</button>
      </form>
      {error && <p className="resource-error" role="alert">{error}</p>}

      <div className="section-heading resource-section-heading">
        <h2>Student roster</h2>
        {!loading && !loadError && <span className="resource-meta">{students.length} {students.length === 1 ? "student" : "students"}</span>}
      </div>
      {loading ? (
        <p className="card resource-status" role="status">Loading class roster...</p>
      ) : loadError ? (
        <div className="resource-error" role="alert"><p>Could not load class roster: {loadError}</p><button className="btn" onClick={refresh}>Retry loading</button></div>
      ) : students.length === 0 ? (
        <div className="empty-state"><h3>Who's joining the race?</h3><p>Add your first student above. Their XP and unlocked characters will appear here.</p></div>
      ) : (
        <div className="table-scroll resource-table" role="region" aria-label="Student roster" tabIndex={0}>
          <table>
            <thead>
              <tr>
                <th scope="col">Student</th>
                <th scope="col">Total XP</th>
                <th scope="col">Unlocked characters</th>
                <th scope="col">PIN status</th>
                <th scope="col"><span className="sr-only">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              {students.map((s) => (
                <tr key={s.id}>
                  <th scope="row" className="resource-text-cell">{s.name}</th>
                  <td className="resource-numeric">{s.xpTotal.toLocaleString()}</td>
                  <td>{s.unlockedCharacters.length > 0 ? s.unlockedCharacters.join(", ") : <span className="resource-detail">Default only</span>}</td>
                  <td><span className={`resource-pin${s.pinSet ? " is-set" : ""}`}>{s.pinSet ? "Set" : "Not set yet"}</span></td>
                  <td><div className="resource-table-actions">
                    <button className="btn" onClick={() => onResetPin(s.id)} disabled={!s.pinSet || pending} aria-label={`Reset PIN for ${s.name}`}>Reset PIN</button>
                    <button className="btn btn-danger" onClick={() => onRemove(s.id)} disabled={pending} aria-label={`Remove ${s.name}`}>Remove</button>
                  </div></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
