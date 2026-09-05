import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import {
  createQuestion,
  deleteQuestion,
  importQuestions,
  listQuestionBanks,
  listQuestions,
  updateQuestion,
  type Question,
  type QuestionBank,
  type QuestionInput,
} from "../api/questionBanks";
import { ApiError } from "../api/client";
import "./resources.css";

const emptyForm: QuestionInput = { text: "", choices: ["", "", "", ""], correctIndex: 0 };

export function QuestionBankEditorPage() {
  const { id } = useParams();
  const bankId = Number(id);
  const [bank, setBank] = useState<QuestionBank | null>(null);
  const [questions, setQuestions] = useState<Question[]>([]);
  const [form, setForm] = useState<QuestionInput>(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [csvText, setCsvText] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loadAttempt, setLoadAttempt] = useState(0);
  const [pending, setPending] = useState(false);
  const questionInput = useRef<HTMLTextAreaElement>(null);

  function refresh() {
    setLoadAttempt((n) => n + 1);
  }

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setLoadError(null);
    Promise.all([listQuestionBanks(), listQuestions(bankId)])
      .then(([banks, data]) => {
        if (cancelled) return;
        const selectedBank = banks.find((b) => b.id === bankId);
        if (!selectedBank) throw new Error("This question bank could not be found.");
        setBank(selectedBank);
        setQuestions(data);
      })
      .catch((e) => { if (!cancelled) setLoadError(e instanceof ApiError ? e.message : String(e)); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [bankId, loadAttempt]);

  useEffect(() => {
    setBank(null);
    setEditingId(null);
    setForm(emptyForm);
    setCsvText("");
    setError(null);
  }, [bankId]);

  function startEdit(q: Question) {
    setEditingId(q.id);
    setForm({ text: q.text, choices: [q.choice_0, q.choice_1, q.choice_2, q.choice_3], correctIndex: q.correct_index });
    questionInput.current?.focus();
  }

  function resetForm() {
    setEditingId(null);
    setForm(emptyForm);
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (pending || loading || loadError) return;
    setError(null);
    if (!form.text.trim() || form.choices.some((c) => !c.trim())) {
      setError("Question text and all four choices are required.");
      return;
    }
    setPending(true);
    try {
      if (editingId) {
        await updateQuestion(editingId, form);
      } else {
        await createQuestion(bankId, form);
      }
      resetForm();
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    } finally {
      setPending(false);
    }
  }

  async function onDelete(qid: number) {
    if (!confirm("Delete this question?")) return;
    setError(null);
    setPending(true);
    try {
      await deleteQuestion(qid);
      if (editingId === qid) resetForm();
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    } finally {
      setPending(false);
    }
  }

  async function onImportCsv() {
    if (pending || loading || loadError) return;
    setError(null);
    const rows = csvText
      .split("\n")
      .map((l) => l.trim())
      .filter(Boolean);
    const parsed: QuestionInput[] = [];
    for (const row of rows) {
      const parts = row.split(",").map((p) => p.trim());
      if (parts.length !== 6) {
        setError(`Each line needs 6 comma-separated fields: text,choiceA,choiceB,choiceC,choiceD,correctIndex(0-3). Problem line: "${row}"`);
        return;
      }
      const correctIndex = Number(parts[5]);
      if (!Number.isInteger(correctIndex) || correctIndex < 0 || correctIndex > 3) {
        setError(`Correct index must be 0-3. Problem line: "${row}"`);
        return;
      }
      parsed.push({ text: parts[0], choices: [parts[1], parts[2], parts[3], parts[4]], correctIndex });
    }
    if (parsed.length === 0) {
      setError("Paste at least one question line to import.");
      return;
    }
    setPending(true);
    try {
      await importQuestions(bankId, parsed);
      setCsvText("");
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="page resource-page">
      <p className="resource-back"><Link to="/question-banks">&larr; All question banks</Link></p>
      <header className="page-heading resource-heading">
        <div>
          <p className="eyebrow">Resources / Question editor</p>
          <h1>{bank?.name ?? "Question bank"}</h1>
          <p className="resource-intro">Clear questions. Four choices. One correct answer.</p>
        </div>
      </header>
      {loading && <p className="resource-status" role="status">Loading question bank...</p>}
      {loadError && <div className="resource-error" role="alert"><p>Could not load question bank: {loadError}</p><button className="btn" onClick={refresh}>Retry loading</button></div>}
      {error && <p className="resource-error" role="alert">{error}</p>}

      <div className="resource-editor-grid">
        <section className="card resource-question-editor" aria-labelledby="question-editor-heading">
          <p className="resource-meta">{editingId ? "Revise a question" : "One at a time"}</p>
          <h2 id="question-editor-heading">{editingId ? "Edit question" : "Add a question"}</h2>
          <form onSubmit={onSubmit}>
            <fieldset className="resource-fieldset" disabled={pending || loading || !!loadError}>
              <legend className="sr-only">Question and answers</legend>
              <div className="field">
                <label htmlFor="qtext">Question text</label>
                <textarea id="qtext" ref={questionInput} rows={3} required value={form.text} onChange={(e) => setForm({ ...form, text: e.target.value })} />
              </div>
              <fieldset className="resource-fieldset resource-answers">
                <legend>Answer choices</legend>
                <p className="resource-detail">Select the radio button beside the correct answer.</p>
                {form.choices.map((choice, i) => (
                  <div className={`resource-choice${form.correctIndex === i ? " is-correct" : ""}`} key={i}>
                    <label className="resource-correct-control">
                      <input type="radio" name="correctIndex" checked={form.correctIndex === i} onChange={() => setForm({ ...form, correctIndex: i })} />
                      <span>{form.correctIndex === i ? "Correct" : "Mark correct"}<span className="sr-only">: choice {"ABCD"[i]}</span></span>
                    </label>
                    <div className="field">
                      <label htmlFor={`choice-${i}`}>Choice {"ABCD"[i]}</label>
                      <input id={`choice-${i}`} required value={choice} onChange={(e) => {
                        const choices = [...form.choices] as QuestionInput["choices"];
                        choices[i] = e.target.value;
                        setForm({ ...form, choices });
                      }} />
                    </div>
                  </div>
                ))}
              </fieldset>
              <div className="row resource-form-actions">
                <button className="btn btn-primary" type="submit">{pending ? "Working..." : editingId ? "Save changes" : "Add question"}</button>
                {editingId && <button className="btn" type="button" onClick={resetForm}>Cancel edit</button>}
              </div>
            </fieldset>
          </form>
        </section>

        <section className="card resource-import" aria-labelledby="import-heading">
          <p className="resource-meta">Bring your own questions</p>
          <h2 id="import-heading">Bulk import</h2>
          <p className="resource-detail" id="csv-format">One question per line, with six comma-separated fields. Use 0 for choice A, 1 for B, 2 for C, or 3 for D. Fields cannot contain commas.</p>
          <code className="resource-format">text,choiceA,choiceB,choiceC,choiceD,correctIndex</code>
          <fieldset className="resource-fieldset" disabled={pending || loading || !!loadError}>
            <legend className="sr-only">Import questions</legend>
            <div className="field">
              <label htmlFor="question-csv">Question rows (CSV)</label>
              <textarea id="question-csv" rows={8} aria-describedby="csv-format" placeholder="2+2?,3,4,5,6,1" value={csvText} onChange={(e) => setCsvText(e.target.value)} />
            </div>
            <button className="btn" onClick={onImportCsv} disabled={!csvText.trim()}>Import questions</button>
          </fieldset>
        </section>
      </div>

      <div className="section-heading resource-section-heading">
        <h2>In this bank</h2>
        {!loading && !loadError && <span className="resource-meta">{questions.length} {questions.length === 1 ? "question" : "questions"}</span>}
      </div>
      {!loading && !loadError && (questions.length === 0 ? (
        <div className="empty-state"><h3>No questions yet.</h3><p>Add your first question or import a batch. A bank needs at least one question before it can be used in a match.</p></div>
      ) : (
        <div className="table-scroll resource-table" role="region" aria-label="Questions in this bank" tabIndex={0}>
          <table>
            <thead>
              <tr>
                <th scope="col">Question</th>
                <th scope="col">Correct answer</th>
                <th scope="col"><span className="sr-only">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              {questions.map((q) => (
                <tr key={q.id} className={editingId === q.id ? "resource-editing-row" : undefined}>
                  <th scope="row" className="resource-text-cell">{q.text}</th>
                  <td className="resource-answer-cell"><span className="resource-answer-letter">{"ABCD"[q.correct_index]}</span> {[q.choice_0, q.choice_1, q.choice_2, q.choice_3][q.correct_index]}</td>
                  <td><div className="resource-table-actions">
                    <button className="btn" onClick={() => startEdit(q)} disabled={pending} aria-label={`Edit question: ${q.text}`}>Edit</button>
                    <button className="btn btn-danger" onClick={() => onDelete(q.id)} disabled={pending} aria-label={`Delete question: ${q.text}`}>Delete</button>
                  </div></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ))}
    </div>
  );
}
