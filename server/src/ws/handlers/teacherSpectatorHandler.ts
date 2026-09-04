import type { ClientConnection, Envelope } from "../wsServer";
import { send } from "../wsServer";
import { buildEmit } from "../liveMatchEmit";
import {
  getOrCreateLiveMatch,
  getLiveMatch,
  addSpectator,
  liveDashboard,
  lobbySnapshot,
  killLiveMatch,
  handleTeacherTriggerHazard as handleLiveHazard,
  handleTeacherTriggerSuddenQuestion as handleLiveSuddenQuestion,
} from "../../matchEngine/LiveMatchRegistry";

export function handleTeacherJoinMatch(conn: ClientConnection, msg: Envelope) {
  const payload = msg.payload as { matchId?: number } | undefined;
  if (!payload?.matchId) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "bad_request", message: "matchId required" } });
    return;
  }
  if (conn.role !== "teacher") {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "forbidden", message: "Teacher role required" } });
    return;
  }

  let live;
  try {
    live = getOrCreateLiveMatch(payload.matchId);
  } catch {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "not_found", message: "Match not found" } });
    return;
  }

  conn.matchId = payload.matchId;
  conn.isSpectator = true;
  addSpectator(live, conn.id);

  send(conn, { type: "lobby_state", payload: lobbySnapshot(live) });
  send(conn, { type: "live_dashboard", payload: liveDashboard(live) });
  if (live.state.status === "active") {
    send(conn, {
      type: "match_start",
      payload: {
        arenaLayout: { grid: live.state.grid, goalRow: live.state.grid.height - 1 },
        players: [...live.state.players.values()].map((p) => ({
          playerId: p.playerId,
          name: p.name,
          characterId: p.characterId,
          team: p.team,
          hp: p.hp,
          maxHp: p.maxHp,
          pos: p.pos,
          alive: p.alive,
        })),
        teams: live.state.mode === "teams",
      },
    });
  }
}

export function handleTeacherKillMatch(conn: ClientConnection, msg: Envelope) {
  if (conn.role !== "teacher") {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "forbidden", message: "Teacher role required" } });
    return;
  }

  const payload = msg.payload as { matchId?: number } | undefined;
  const matchId = payload?.matchId ?? conn.matchId;
  if (!matchId) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "bad_request", message: "matchId required" } });
    return;
  }

  const live = getLiveMatch(matchId);
  if (!live) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "not_found", message: "Match not found or already ended" } });
    return;
  }

  const emit = buildEmit(matchId);
  killLiveMatch(live, emit);
}

export function handleTeacherTriggerHazard(conn: ClientConnection, msg: Envelope) {
  if (conn.role !== "teacher") {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "forbidden", message: "Teacher role required" } });
    return;
  }

  const payload = msg.payload as { matchId?: number; hazardType?: string; damage?: number } | undefined;
  const matchId = payload?.matchId ?? conn.matchId;
  if (!matchId) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "bad_request", message: "matchId required" } });
    return;
  }

  const live = getLiveMatch(matchId);
  if (!live) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "not_found", message: "Match not found or already ended" } });
    return;
  }

  const emit = buildEmit(matchId);
  const result = handleLiveHazard(live, payload?.hazardType ?? "fireball_rain", payload?.damage ?? 5, emit);
  if (!result.ok) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: result.error, message: "Hazard trigger failed" } });
  }
}

export function handleTeacherTriggerSuddenQuestion(conn: ClientConnection, msg: Envelope) {
  if (conn.role !== "teacher") {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "forbidden", message: "Teacher role required" } });
    return;
  }

  const payload = msg.payload as {
    matchId?: number;
    questionId?: number;
    text?: string;
    choices?: string[];
    correctIndex?: number;
    rewardType?: "mega_attack" | "super_freeze" | "bonus_move";
    rewardDamage?: number;
    rewardName?: string;
  } | undefined;

  const matchId = payload?.matchId ?? conn.matchId;
  if (!matchId) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "bad_request", message: "matchId required" } });
    return;
  }

  const live = getLiveMatch(matchId);
  if (!live) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: "not_found", message: "Match not found or already ended" } });
    return;
  }

  const emit = buildEmit(matchId);
  const result = handleLiveSuddenQuestion(live, payload ?? {}, emit);
  if (!result.ok) {
    send(conn, { type: "error", correlationId: msg.correlationId, payload: { code: result.error, message: "Sudden question trigger failed" } });
  }
}
