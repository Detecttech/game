import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { createClass, deleteClass, listClasses, type ClassRoster } from "../api/classes";
import { ApiError } from "../api/client";
import "./resources.css";

export function RosterListPage() {
  const [classes, setClasses] = useState<ClassRoster[]>([]);
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
    listClasses()
      .then((data) => { if (!cancelled) setClasses(data); })
      .catch((e) => { if (!cancelled) setLoadError(e instanceof ApiError ? e.message : String(e)); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [loadAttempt]);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    if (!newName.trim() || pending) return;
    setError(null);
    setPending(true);
    try {
      await createClass(newName.trim());
      setNewName("");
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    } finally {
      setPending(false);
    }
  }

  async function onDelete(id: number) {
    if (!confirm("Delete this class and its roster? This cannot be undone.")) return;
    setError(null);
    setPending(true);
    try {
      await deleteClass(id);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="page resource-page">
      <header className="page-heading resource-heading">
        <div>
          <p className="eyebrow">Resources / Class rosters</p>
          <h1>Make room for everyone.</h1>
          <p className="resource-intro">Keep your classes organized, manage student access, and follow their earned XP.</p>
        </div>
        <Link className="btn" to="/match-setup">Prepare a match &rarr;</Link>
      </header>
      <form onSubmit={onCreate} className="card resource-create">
        <div className="field">
          <label htmlFor="class-name">New class name</label>
          <input id="class-name" placeholder="e.g. Period 3 Science" required value={newName} onChange={(e) => setNewName(e.target.value)} disabled={pending} />
        </div>
        <button className="btn btn-primary" type="submit" disabled={pending || !newName.trim()}>Create class</button>
      </form>
      {error && <p className="resource-error" role="alert">{error}</p>}
      <div className="section-heading resource-section-heading">
        <h2>Your classes</h2>
        {!loading && !loadError && <span className="resource-meta">{classes.length} {classes.length === 1 ? "class" : "classes"}</span>}
      </div>
      {loading ? (
        <p className="card resource-status" role="status">Loading classes...</p>
      ) : loadError ? (
        <div className="resource-error" role="alert"><p>Could not load classes: {loadError}</p><button className="btn" onClick={refresh}>Retry loading</button></div>
      ) : classes.length === 0 ? (
        <div className="empty-state"><h3>A class is the starting line.</h3><p>Create your first class above, then add the students who will be playing.</p></div>
      ) : (
        <div className="resource-grid">
          {classes.map((c) => (
            <article className="card resource-item resource-item-teal" key={c.id}>
              <p className="resource-meta">Class roster</p>
              <h3>{c.name}</h3>
              <p className="resource-detail">Class code <span className="badge resource-code">{c.class_code}</span></p>
              <div className="resource-item-actions">
                <Link className="btn" to={`/rosters/${c.id}`} aria-label={`Open roster: ${c.name}`}>Open roster &rarr;</Link>
                <button className="btn btn-danger" onClick={() => onDelete(c.id)} disabled={pending} aria-label={`Delete ${c.name}`}>Delete</button>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
