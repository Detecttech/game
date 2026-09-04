import { getCharacterMeta } from "../utils/characters";

export interface CombatEvent {
  id: string;
  type: "attack" | "freeze" | "advance" | "bonus_move" | "finish" | "eliminated" | "system";
  timestamp: number;
  text: string;
  attackerName?: string;
  attackerCharacterId?: string;
  targetName?: string;
  targetCharacterId?: string;
  damage?: number;
  targetHpAfter?: number;
  rank?: number;
}

interface CombatFeedProps {
  events: CombatEvent[];
  onClear?: () => void;
}

export function CombatFeed({ events, onClear }: CombatFeedProps) {
  return (
    <div className="combat-feed-card card">
      <div className="combat-feed-header">
        <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
          <span style={{ fontSize: "1.2rem" }}>⚔️</span>
          <h3 style={{ margin: 0, fontSize: "1.05rem" }}>Live Combat & Match Feed</h3>
          <span className="badge" style={{ fontSize: "0.75rem", background: "rgba(124, 58, 237, 0.15)", color: "var(--accent)" }}>
            {events.length} events
          </span>
        </div>
        {events.length > 0 && onClear && (
          <button className="btn" style={{ padding: "2px 8px", fontSize: "0.75rem" }} onClick={onClear}>
            Clear
          </button>
        )}
      </div>

      <div className="combat-feed-list">
        {events.length === 0 ? (
          <div className="combat-feed-empty">
            <span>🏟️ Waiting for match actions...</span>
          </div>
        ) : (
          events.slice(0, 30).map((ev) => {
            const attackerMeta = ev.attackerCharacterId ? getCharacterMeta(ev.attackerCharacterId) : null;
            const targetMeta = ev.targetCharacterId ? getCharacterMeta(ev.targetCharacterId) : null;

            return (
              <div key={ev.id} className={`combat-event-item event-${ev.type}`}>
                <div className="combat-event-time">
                  {new Date(ev.timestamp).toLocaleTimeString([], { hour12: false, hour: "2-digit", minute: "2-digit", second: "2-digit" })}
                </div>

                <div className="combat-event-body">
                  {ev.type === "attack" && (
                    <div className="combat-attack-line">
                      <span className="badge-char" style={{ borderColor: attackerMeta?.border }}>
                        {attackerMeta?.icon} <strong>{ev.attackerName}</strong>
                      </span>
                      <span className="attack-arrow">
                        ⚔️ <strong style={{ color: "#ef4444" }}>-{ev.damage} HP</strong> ➔
                      </span>
                      <span className="badge-char" style={{ borderColor: targetMeta?.border }}>
                        {targetMeta?.icon} <strong>{ev.targetName}</strong>
                      </span>
                      {ev.targetHpAfter !== undefined && (
                        <span className="hp-remaining">({ev.targetHpAfter} HP left)</span>
                      )}
                    </div>
                  )}

                  {ev.type === "freeze" && (
                    <div className="combat-attack-line">
                      <span className="badge-char" style={{ borderColor: attackerMeta?.border }}>
                        {attackerMeta?.icon} <strong>{ev.attackerName}</strong>
                      </span>
                      <span className="freeze-arrow">
                        ❄️ <strong style={{ color: "#06b6d4" }}>FROZE</strong> ➔
                      </span>
                      <span className="badge-char" style={{ borderColor: targetMeta?.border }}>
                        {targetMeta?.icon} <strong>{ev.targetName}</strong>
                      </span>
                    </div>
                  )}

                  {ev.type === "finish" && (
                    <div className="combat-finish-line">
                      <span style={{ fontSize: "1.2rem" }}>
                        {ev.rank === 1 ? "🥇" : ev.rank === 2 ? "🥈" : ev.rank === 3 ? "🥉" : "🏁"}
                      </span>
                      <strong>{ev.attackerName}</strong> reached the goal line!{" "}
                      <span className="badge" style={{ background: "#f59e0b", color: "#000", fontWeight: 700 }}>
                        {ev.rank === 1 ? "1st PLACE WINNER!" : `${ev.rank}th Place`}
                      </span>
                    </div>
                  )}

                  {ev.type === "eliminated" && (
                    <div className="combat-elim-line">
                      <span>💀</span>
                      <strong>{ev.targetName}</strong> was eliminated!
                    </div>
                  )}

                  {ev.type === "bonus_move" && (
                    <div className="combat-bonus-line">
                      <span>⚡</span>
                      <strong>{ev.attackerName}</strong> gained bonus momentum and dashed forward!
                    </div>
                  )}

                  {(ev.type === "advance" || ev.type === "system") && (
                    <div className="combat-generic-line">{ev.text}</div>
                  )}
                </div>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}
