import type { SpectatorPlayer } from "./ArenaTrackView";
import { getCharacterMeta } from "../utils/characters";

interface LeaderboardStandingsProps {
  players: SpectatorPlayer[];
  goalRow: number;
  selectedPlayerId: number | null;
  onSelectPlayer: (playerId: number) => void;
  mode: "ffa" | "teams";
}

export function LeaderboardStandings({
  players,
  goalRow,
  selectedPlayerId,
  onSelectPlayer,
  mode,
}: LeaderboardStandingsProps) {
  // Sort standings:
  // 1. Finished players by finishRank ASC
  // 2. Unfinished players: alive first, then lane progress DESC, then HP DESC
  const rankedPlayers = [...players].sort((a, b) => {
    if (a.goalReached && b.goalReached) {
      return (a.finishRank ?? 999) - (b.finishRank ?? 999);
    }
    if (a.goalReached) return -1;
    if (b.goalReached) return 1;
    if (a.alive !== b.alive) return Number(b.alive) - Number(a.alive);
    if (b.pos.y !== a.pos.y) return b.pos.y - a.pos.y;
    return b.hp - a.hp;
  });

  return (
    <div className="standings-card card">
      <div className="standings-header">
        <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
          <span style={{ fontSize: "1.2rem" }}>🏅</span>
          <h2 style={{ margin: 0, fontSize: "1.05rem" }}>Live Standings</h2>
        </div>
        <span className="muted" style={{ fontSize: "0.8rem" }}>
          Goal: Row {goalRow}
        </span>
      </div>
      <p className="standings-hint">Select a racer to follow on the track.</p>

      <div className="standings-list">
        {rankedPlayers.length === 0 ? (
          <p className="muted">No players in match</p>
        ) : (
          rankedPlayers.map((p, idx) => {
            const rank = p.finishRank ?? idx + 1;
            const meta = getCharacterMeta(p.characterId);
            const isSelected = selectedPlayerId === p.playerId;
            const hpPct = Math.max(0, Math.min(100, (p.hp / (p.maxHp || 45)) * 100));
            const hpColor = hpPct > 50 ? "#236b4b" : hpPct > 25 ? "#895b0d" : "#b83e1c";
            const progressPct = Math.round((Math.min(goalRow, p.pos.y) / (goalRow || 1)) * 100);

            return (
              <button
                type="button"
                key={p.playerId}
                className={`standings-item ${isSelected ? "selected" : ""} ${!p.alive ? "eliminated" : ""}`}
                aria-pressed={isSelected}
                onClick={() => onSelectPlayer(p.playerId)}
              >
                <span className="standings-rank">
                  {p.goalReached && rank === 1 ? (
                    <span className="rank-badge rank-1">🥇 1st</span>
                  ) : p.goalReached && rank === 2 ? (
                    <span className="rank-badge rank-2">🥈 2nd</span>
                  ) : p.goalReached && rank === 3 ? (
                    <span className="rank-badge rank-3">🥉 3rd</span>
                  ) : (
                    <span className="rank-badge rank-num">#{rank}</span>
                  )}
                </span>

                <span className="standings-char-icon" style={{ background: meta.badgeBg, borderColor: meta.border }} aria-hidden="true">
                  {p.alive ? meta.icon : "💀"}
                </span>

                <span className="standings-info">
                  <span className="standings-name-row">
                    <span className="standings-player-name">{p.name}</span>
                    <span className="standings-char-name">
                      {meta.name}
                    </span>
                    {mode === "teams" && p.team && (
                      <span className={`team-tag team-${p.team.toLowerCase()}`}>Team {p.team}</span>
                    )}
                    {p.streak >= 2 && <span className="streak-tag">🔥 {p.streak} streak</span>}
                    {p.frozen && <span className="frozen-tag">❄️ Frozen</span>}
                  </span>

                  {/* HP Bar */}
                  <span className="standings-bar-container" aria-hidden="true">
                    <span className="standings-bar-fill" style={{ width: `${hpPct}%`, background: hpColor }} />
                  </span>

                  <span className="standings-stats-row">
                    <span style={{ color: hpColor, fontWeight: 600, fontSize: "0.8rem" }}>
                      {p.hp}/{p.maxHp} HP
                    </span>
                    <span className="muted" style={{ fontSize: "0.8rem" }}>
                      Row {p.pos.y}/{goalRow} ({progressPct}%)
                    </span>
                    <span className="muted" style={{ fontSize: "0.8rem" }}>
                      {p.goalReached ? "🏁 Finished" : !p.alive ? "Eliminated" : "Racing"}
                    </span>
                  </span>
                </span>
              </button>
            );
          })
        )}
      </div>
    </div>
  );
}
