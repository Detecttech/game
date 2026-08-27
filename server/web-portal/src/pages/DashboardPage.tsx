import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { listClasses, type ClassRoster } from "../api/classes";
import { listQuestionBanks, type QuestionBank } from "../api/questionBanks";
import { getToken } from "../api/client";
import { useAuth } from "../state/AuthContext";

export function DashboardPage() {
  const { teacher } = useAuth();
  const [classes, setClasses] = useState<ClassRoster[]>([]);
  const [banks, setBanks] = useState<QuestionBank[]>([]);

  useEffect(() => {
    listClasses().then(setClasses).catch(() => {});
    listQuestionBanks().then(setBanks).catch(() => {});
  }, []);

  return (
    <div className="page">
      <h1>Welcome{teacher ? `, ${teacher.displayName}` : ""}</h1>
      <div className="row">
        <div className="card" style={{ flex: 1, minWidth: 200 }}>
          <h2>{classes.length}</h2>
          <p className="muted">Classes</p>
          <Link className="btn" to="/rosters">Manage classes</Link>
        </div>
        <div className="card" style={{ flex: 1, minWidth: 200 }}>
          <h2>{banks.length}</h2>
          <p className="muted">Question banks</p>
          <Link className="btn" to="/question-banks">Manage question banks</Link>
        </div>
      </div>
      <div className="card">
        <h2>Start a match</h2>
        <p className="muted">Pick a class, a question bank, and get a join code students can enter.</p>
        <Link className="btn btn-primary" to="/match-setup">Set up a match</Link>
      </div>

      <div className="card" style={{ marginTop: "1rem" }}>
        <h2>Backup & Restore Cloud Data</h2>
        <p className="muted">
          Save all your custom questions, question banks, and class rosters to your computer, or restore them anytime.
        </p>
        <div style={{ display: "flex", gap: "10px", flexWrap: "wrap", marginTop: "0.5rem" }}>
          <button
            className="btn"
            onClick={async () => {
              try {
                const token = getToken();
                const res = await fetch("/api/backup/export", {
                  headers: token ? { Authorization: `Bearer ${token}` } : {},
                });
                const blob = await res.blob();
                const url = URL.createObjectURL(blob);
                const a = document.createElement("a");
                a.href = url;
                a.download = `quizbattle-backup-${new Date().toISOString().slice(0, 10)}.json`;
                a.click();
              } catch (e) {
                alert("Failed to export backup: " + e);
              }
            }}
          >
            Export Backup (.json)
          </button>

          <label className="btn btn-secondary" style={{ cursor: "pointer", display: "inline-block" }}>
            Restore from Backup
            <input
              type="file"
              accept=".json"
              style={{ display: "none" }}
              onChange={async (e) => {
                const file = e.target.files?.[0];
                if (!file) return;
                try {
                  const text = await file.text();
                  const json = JSON.parse(text);
                  const token = getToken();
                  const res = await fetch("/api/backup/import", {
                    method: "POST",
                    headers: {
                      "Content-Type": "application/json",
                      ...(token ? { Authorization: `Bearer ${token}` } : {}),
                    },
                    body: JSON.stringify(json),
                  });
                  const result = await res.json();
                  alert(`Restored successfully! Imported ${result.importedBanks ?? 0} banks, ${result.importedQuestions ?? 0} questions, ${result.importedClasses ?? 0} classes.`);
                  listClasses().then(setClasses).catch(() => {});
                  listQuestionBanks().then(setBanks).catch(() => {});
                } catch (err) {
                  alert("Failed to restore backup: " + err);
                }
              }}
            />
          </label>
        </div>
      </div>
    </div>
  );
}
