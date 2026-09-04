import { Router } from "express";
import { leaderboardForClass, leaderboardGlobal, exportLeaderboardCsv } from "../../db/repositories/leaderboardRepo";

export const leaderboardRoutes = Router();

// Public read: students see this post-match too, not just teachers.
leaderboardRoutes.get("/leaderboard", (req, res) => {
  const scope = String(req.query.scope ?? "global");
  if (scope.startsWith("class:")) {
    const classId = Number(scope.slice("class:".length));
    res.json(leaderboardForClass(classId));
    return;
  }
  res.json(leaderboardGlobal());
});

// Download student XP leaderboard as CSV
leaderboardRoutes.get(["/leaderboard/export", "/leaderboard/csv"], (req, res) => {
  const classId = req.query.classId ? Number(req.query.classId) : undefined;
  const csvData = exportLeaderboardCsv(classId);

  const filename = classId ? `quizbattle-class-${classId}-leaderboard.csv` : `quizbattle-all-students-leaderboard.csv`;
  res.setHeader("Content-Type", "text/csv; charset=utf-8");
  res.setHeader("Content-Disposition", `attachment; filename="${filename}"`);
  res.status(200).send(csvData);
});

