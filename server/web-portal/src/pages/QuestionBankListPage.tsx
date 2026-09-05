import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { createQuestionBank, deleteQuestionBank, listQuestionBanks, type QuestionBank } from "../api/questionBanks";
import { ApiError } from "../api/client";
import "./resources.css";

export function QuestionBankListPage() {
  const [banks, setBanks] = useState<QuestionBank[]>([]);
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
    listQuestionBanks()
      .then((data) => { if (!cancelled) setBanks(data); })
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
      await createQuestionBank(newName.trim());
      setNewName("");
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    } finally {
      setPending(false);
    }
  }

  async function onDelete(id: number) {
    if (!confirm("Delete this question bank and all its questions?")) return;
    setError(null);
    setPending(true);
    try {
      await deleteQuestionBank(id);
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
          <p className="eyebrow">Resources / Question library</p>
          <h1>Questions worth racing for.</h1>
          <p className="resource-intro">Build a bank for each topic, then bring it into your next classroom tournament.</p>
        </div>
        <Link className="btn" to="/match-setup">Prepare a match &rarr;</Link>
      </header>
      <form onSubmit={onCreate} className="card resource-create">
        <div className="field">
          <label htmlFor="bank-name">New question bank name</label>
          <input id="bank-name" placeholder="e.g. Fractions Unit 2" required value={newName} onChange={(e) => setNewName(e.target.value)} disabled={pending} />
        </div>
        <button className="btn btn-primary" type="submit" disabled={pending || !newName.trim()}>Create bank</button>
      </form>
      {error && <p className="resource-error" role="alert">{error}</p>}
      <div className="section-heading resource-section-heading">
        <h2>Your question banks</h2>
        {!loading && !loadError && <span className="resource-meta">{banks.length} {banks.length === 1 ? "bank" : "banks"}</span>}
      </div>
      {loading ? (
        <p className="card resource-status" role="status">Loading question banks...</p>
      ) : loadError ? (
        <div className="resource-error" role="alert"><p>Could not load question banks: {loadError}</p><button className="btn" onClick={refresh}>Retry loading</button></div>
      ) : banks.length === 0 ? (
        <div className="empty-state"><h3>Your library starts here.</h3><p>Create a bank above, then add questions individually or import a batch.</p></div>
      ) : (
        <div className="resource-grid">
          {banks.map((b) => (
            <article className="card resource-item" key={b.id}>
              <p className="resource-meta">Question bank</p>
              <h3>{b.name}</h3>
              <p className="resource-detail">Created {new Date(b.created_at).toLocaleDateString()}</p>
              <div className="resource-item-actions">
                <Link className="btn" to={`/question-banks/${b.id}`} aria-label={`Open bank: ${b.name}`}>Open bank &rarr;</Link>
                <button className="btn btn-danger" onClick={() => onDelete(b.id)} disabled={pending} aria-label={`Delete ${b.name}`}>Delete</button>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
