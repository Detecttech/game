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
