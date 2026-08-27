import { Router } from "express";
import { requireTeacher, type AuthedRequest } from "../middleware/authTeacher";
import { findClassRosterById } from "../../db/repositories/classRepo";
import { findQuestionBankById } from "../../db/repositories/questionBankRepo";
import {
  createMatch,
  findMatchById,
  listMatchesByTeacher,
  listMatchEvents,
  listParticipants,
  setMatchStatus,
  setMatchWinner,
  type MatchMode,
} from "../../db/repositories/matchRepo";
import { getLiveMatch, killLiveMatch } from "../../matchEngine/LiveMatchRegistry";
import { buildEmit } from "../../ws/liveMatchEmit";

export const matchRoutes = Router();

matchRoutes.use(requireTeacher);

function randomJoinCode(): string {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let code = "";
  for (let i = 0; i < 6; i++) code += alphabet[Math.floor(Math.random() * alphabet.length)];
  return code;
}

matchRoutes.get("/matches", (req: AuthedRequest, res) => {
  res.json(listMatchesByTeacher(req.teacherId!));
});

matchRoutes.post("/matches", (req: AuthedRequest, res) => {
  const { classRosterId, questionBankId, mode, timerSeconds } = req.body ?? {};
  const roster = findClassRosterById(Number(classRosterId));
  if (!roster || roster.teacher_id !== req.teacherId) {
    res.status(404).json({ code: "not_found", message: "Class not found" });
    return;
  }
  const bank = findQuestionBankById(Number(questionBankId));
  if (!bank || bank.teacher_id !== req.teacherId) {
    res.status(404).json({ code: "not_found", message: "Question bank not found" });
    return;
  }
  const matchMode: MatchMode = mode === "teams" ? "teams" : "ffa";
  const timerSec = Number(timerSeconds) > 0 ? Number(timerSeconds) : 30;
  const match = createMatch(roster.id, bank.id, matchMode, randomJoinCode(), timerSec);
  res.status(201).json(match);
});

matchRoutes.get("/matches/:id", (req: AuthedRequest, res) => {
  const match = findMatchById(Number(req.params.id));
  if (!match) {
    res.status(404).json({ code: "not_found", message: "Match not found" });
    return;
  }
  const roster = findClassRosterById(match.class_roster_id);
  if (!roster || roster.teacher_id !== req.teacherId) {
    res.status(404).json({ code: "not_found", message: "Match not found" });
    return;
  }
  res.json({
    ...match,
    participants: listParticipants(match.id),
    events: listMatchEvents(match.id),
  });
});

matchRoutes.post("/matches/:id/kill", (req: AuthedRequest, res) => {
  const matchId = Number(req.params.id);
  const match = findMatchById(matchId);
  if (!match) {
    res.status(404).json({ code: "not_found", message: "Match not found" });
    return;
  }
  const roster = findClassRosterById(match.class_roster_id);
  if (!roster || roster.teacher_id !== req.teacherId) {
    res.status(403).json({ code: "forbidden", message: "Not authorized to manage this match" });
    return;
  }

  const live = getLiveMatch(matchId);
  if (live) {
    const emit = buildEmit(matchId);
    killLiveMatch(live, emit);
    res.json({ ok: true, message: "Match terminated" });
  } else {
    if (match.status !== "completed") {
      setMatchStatus(matchId, "completed");
      setMatchWinner(matchId, "none");
    }
    res.json({ ok: true, message: "Match marked as cancelled" });
  }
});
