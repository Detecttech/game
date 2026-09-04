import { useMemo } from "react";
import { getCharacterMeta } from "../utils/characters";

export interface SpectatorPlayer {
  playerId: number;
  name: string;
  characterId: string | null;
  team: string | null;
  hp: number;
  maxHp: number;
  alive: boolean;
  streak: number;
  pos: { x: number; y: number };
  goalReached: boolean;
  finishRank: number | null;
  frozen: boolean;
}

export interface ActiveAttackVisual {
  id: string;
  attackerId: number;
  targetId: number;
  damage?: number;
  type: "attack" | "freeze" | "bonus";
}

export interface FloatingText {
  id: string;
  playerId: number;
  text: string;
  color: string;
}

export interface ActiveHazardVisual {
  id: string;
  type: string;
  name: string;
}

interface ArenaTrackViewProps {
  grid: { width: number; height: number; goalRow: number };
  players: SpectatorPlayer[];
  activeAttacks: ActiveAttackVisual[];
  floatingTexts: FloatingText[];
  selectedPlayerId: number | null;
  onSelectPlayer: (playerId: number) => void;
  mode: "ffa" | "teams";
  activeHazard?: ActiveHazardVisual | null;
}

export function ArenaTrackView({
  grid,
  players,
  activeAttacks,
  floatingTexts,
  selectedPlayerId,
  onSelectPlayer,
  mode,
  activeHazard,
}: ArenaTrackViewProps) {
  const goalRow = grid.goalRow > 0 ? grid.goalRow : grid.height - 1;
  const totalRows = goalRow + 1; // 0 to goalRow inclusive

  // Map each player to their assigned column / lane
  // Players start spread across columns, or by index if width matches player count
  const sortedPlayers = useMemo(() => {
    return [...players].sort((a, b) => a.pos.x - b.pos.x || a.playerId - b.playerId);
  }, [players]);

  // Compute player token positions (percentages for CSS)
  // X: column center percentage
  // Y: bottom percentage from 0 (row 0) to 100% (goal row)
  const playerPositions = useMemo(() => {
    const map = new Map<number, { xPct: number; yPct: number }>();
    const count = Math.max(1, sortedPlayers.length);

    sortedPlayers.forEach((p, idx) => {
      // If player has explicit x in range, use it; otherwise spread evenly
      const xPct = ((idx + 0.5) / count) * 100;
      const progress = Math.min(goalRow, Math.max(0, p.pos.y));
      const yPct = (progress / goalRow) * 100;
      map.set(p.playerId, { xPct, yPct });
    });
    return map;
  }, [sortedPlayers, goalRow]);

  return (
    <div className="arena-track-container card">
      {/* Track Header / Goal Banner */}
      <div className="arena-goal-arch">
        <div className="checkered-line" />
        <div className="goal-banner-content">
          <span className="goal-icon">🏆</span>
          <span className="goal-title">COLOSSEUM GOAL LINE — ROW {goalRow}</span>
          <span className="goal-icon">🏆</span>
        </div>
        <div className="checkered-line" />
      </div>

      {/* Main Track Stage */}
      <div className="arena-track-stage">
        {/* Lane dividers and row milestone lines */}
        <div className="arena-grid-background">
          {Array.from({ length: totalRows }).map((_, rIdx) => {
            const rowNumber = totalRows - 1 - rIdx;
            const isGoal = rowNumber === goalRow;
            const isStart = rowNumber === 0;

            return (
              <div
                key={rowNumber}
                className={`track-row-line ${isGoal ? "row-goal" : isStart ? "row-start" : ""}`}
                style={{ top: `${(rIdx / (totalRows - 1)) * 100}%` }}
              >
                <span className="row-milestone-badge">
                  {isGoal ? "🏁 GOAL" : isStart ? "START" : `Row ${rowNumber}`}
                </span>
              </div>
            );
          })}

          {/* Lane Columns */}
          <div className="track-lanes-columns">
            {sortedPlayers.map((p, idx) => {
              const meta = getCharacterMeta(p.characterId);
              return (
                <div
                  key={p.playerId}
                  className="track-lane-column"
                  style={{
                    left: `${(idx / sortedPlayers.length) * 100}%`,
                    width: `${100 / sortedPlayers.length}%`,
                  }}
                >
                  <div className="lane-header-tag" style={{ color: meta.color }}>
                    Lane {idx + 1}
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* SVG Attack Beam Overlay */}
        <svg className="arena-attack-overlay" viewBox="0 0 100 100" preserveAspectRatio="none">
          <defs>
            <linearGradient id="attackBeamGrad" x1="0%" y1="0%" x2="100%" y2="0%">
              <stop offset="0%" stopColor="#ef4444" stopOpacity="0.8" />
              <stop offset="50%" stopColor="#f59e0b" stopOpacity="1" />
              <stop offset="100%" stopColor="#dc2626" stopOpacity="0.9" />
            </linearGradient>
            <linearGradient id="freezeBeamGrad" x1="0%" y1="0%" x2="100%" y2="0%">
              <stop offset="0%" stopColor="#38bdf8" stopOpacity="0.8" />
              <stop offset="50%" stopColor="#06b6d4" stopOpacity="1" />
              <stop offset="100%" stopColor="#bae6fd" stopOpacity="0.9" />
            </linearGradient>
            <filter id="glow" x="-20%" y="-20%" width="140%" height="140%">
              <feGaussianBlur stdDeviation="2" result="blur" />
              <feComposite in="SourceGraphic" in2="blur" operator="over" />
            </filter>
          </defs>

          {activeAttacks.map((atk) => {
            const pFrom = playerPositions.get(atk.attackerId);
            const pTo = playerPositions.get(atk.targetId);
            if (!pFrom || !pTo) return null;

            // Convert CSS percentages to SVG coordinates
            // In SVG viewBox 0-100, Y is 0 at top and 100 at bottom
            const x1 = pFrom.xPct;
            const y1 = 100 - (pFrom.yPct * 0.85 + 7.5);
            const x2 = pTo.xPct;
            const y2 = 100 - (pTo.yPct * 0.85 + 7.5);

            const isFreeze = atk.type === "freeze";
            const strokeGrad = isFreeze ? "url(#freezeBeamGrad)" : "url(#attackBeamGrad)";

            return (
              <g key={atk.id} className="svg-attack-group">
                {/* Attack Arc Line */}
                <line
                  x1={x1}
                  y1={y1}
                  x2={x2}
                  y2={y2}
                  stroke={strokeGrad}
                  strokeWidth="2.5"
                  strokeLinecap="round"
                  filter="url(#glow)"
                  className="svg-attack-line"
                />
                {/* Projectile Pulse */}
                <circle
                  cx={(x1 + x2) / 2}
                  cy={(y1 + y2) / 2}
                  r="3.5"
                  fill={isFreeze ? "#06b6d4" : "#f59e0b"}
                  filter="url(#glow)"
                  className="svg-projectile-pulse"
                />
                {/* Target Impact Burst */}
                <circle
                  cx={x2}
                  cy={y2}
                  r="5"
                  fill="none"
                  stroke={isFreeze ? "#38bdf8" : "#ef4444"}
                  strokeWidth="2"
                  className="svg-impact-burst"
                />
              </g>
            );
          })}
        </svg>

        {/* Arena Hazard Visuals (Fireball Rain) */}
        {activeHazard && (
          <div className="arena-hazard-overlay">
            <div className="hazard-warning-toast">
              <span>🔥</span>
              <span>{activeHazard.name.toUpperCase()} ACTIVATED!</span>
              <span>🔥</span>
            </div>
            <div className="hazard-fireball-rain">
              {sortedPlayers.map((p) => {
                const pos = playerPositions.get(p.playerId);
                if (!pos) return null;
                return (
                  <div
                    key={`hazard_${p.playerId}`}
                    className="hazard-fireball-projectile"
                    style={{ left: `${pos.xPct}%` }}
                  >
                    <div className="hazard-fireball-flame">🔥</div>
                    <div className="hazard-fireball-burst" />
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Dynamic Player Racers */}
        <div className="arena-racers-layer">
          {sortedPlayers.map((p) => {
            const pos = playerPositions.get(p.playerId);
            if (!pos) return null;

            const meta = getCharacterMeta(p.characterId);
            const isSelected = selectedPlayerId === p.playerId;
            const hpPct = Math.max(0, Math.min(100, (p.hp / (p.maxHp || 45)) * 100));
            const hpColor = hpPct > 50 ? "#22c55e" : hpPct > 25 ? "#f59e0b" : "#ef4444";

            // Y coordinate: 0% is bottom, 100% is top goal
            // In CSS, bottom: calc(7% + yPct * 0.82)
            const bottomPercent = 7 + pos.yPct * 0.82;

            const playerFloatingTexts = floatingTexts.filter((f) => f.playerId === p.playerId);

            return (
              <div
                key={p.playerId}
                className={`racer-token-container ${isSelected ? "selected" : ""} ${
                  !p.alive ? "eliminated" : ""
                } ${p.frozen ? "frozen" : ""} ${p.goalReached ? "goal-reached" : ""}`}
                style={{
                  left: `${pos.xPct}%`,
                  bottom: `${bottomPercent}%`,
                }}
                onClick={() => onSelectPlayer(p.playerId)}
                title={`${p.name} — ${p.hp}/${p.maxHp} HP — Streak ${p.streak}`}
              >
                {/* Floating Damage / Combat Text */}
                {playerFloatingTexts.map((ft) => (
                  <div key={ft.id} className="floating-combat-text" style={{ color: ft.color }}>
                    {ft.text}
                  </div>
                ))}

                {/* Racer Head Badge (Rank Medal or Status) */}
                {p.finishRank !== null && p.finishRank > 0 && (
                  <div className="racer-finish-medal">
                    {p.finishRank === 1 ? "🥇 1st" : p.finishRank === 2 ? "🥈 2nd" : p.finishRank === 3 ? "🥉 3rd" : `#${p.finishRank}`}
                  </div>
                )}

                {p.frozen && !p.goalReached && (
                  <div className="racer-status-pill frozen-pill">❄️ FROZEN</div>
                )}

                {/* Racer Avatar Token */}
                <div
                  className="racer-avatar-card"
                  style={{
                    borderColor: p.frozen ? "#38bdf8" : meta.border,
                    boxShadow: isSelected ? `0 0 16px ${meta.color}` : `0 4px 12px rgba(0,0,0,0.5)`,
                  }}
                >
                  <div className="racer-avatar-icon" style={{ background: meta.badgeBg }}>
                    {p.alive ? meta.icon : "💀"}
                  </div>

                  <div className="racer-info-block">
                    <div className="racer-name-row">
                      <span className="racer-name">{p.name}</span>
                      {mode === "teams" && p.team && (
                        <span className={`team-tag team-${p.team.toLowerCase()}`}>{p.team}</span>
                      )}
                    </div>

                    {/* Visual Health Bar */}
                    <div className="racer-hp-bar-bg">
                      <div
                        className="racer-hp-bar-fill"
                        style={{
                          width: `${hpPct}%`,
                          backgroundColor: hpColor,
                        }}
                      />
                    </div>

                    <div className="racer-stats-row">
                      <span className="racer-hp-text" style={{ color: hpColor }}>
                        {p.hp}/{p.maxHp} HP
                      </span>
                      {p.streak >= 2 && (
                        <span className="racer-streak-badge" title={`${p.streak} correct streak`}>
                          🔥 {p.streak}
                        </span>
                      )}
                    </div>
                  </div>
                </div>

                {/* Lane Step Pin at Bottom */}
                <div className="racer-step-marker">
                  Step {p.pos.y}/{goalRow}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Track Footer: Start Line */}
      <div className="arena-start-bar">
        <span>🚩 START LINE — ROW 0</span>
      </div>
    </div>
  );
}
