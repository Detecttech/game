import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { getToken } from "../api/client";
import { getMatch, type Match } from "../api/matches";
import { getClass, type ClassRoster } from "../api/classes";
import { ArenaTrackView } from "../components/ArenaTrackView";
import type {
  SpectatorPlayer,
  ActiveAttackVisual,
  FloatingText,
  ActiveHazardVisual,
} from "../components/ArenaTrackView";
import { CombatFeed } from "../components/CombatFeed";
import type { CombatEvent } from "../components/CombatFeed";
import { LeaderboardStandings } from "../components/LeaderboardStandings";
import {
  playAdvanceSound,
  playAttackSound,
  playFreezeSound,
  playVictorySound,
  isAudioMuted,
  setAudioMuted,
} from "../utils/audioEffects";

interface LobbyPlayer {
  playerId: number;
  name: string;
  characterId: string | null;
  team: string | null;
  ready: boolean;
}

interface QuestionInfo {
  questionId: number;
  text: string;
  choices: string[];
  roundNumber?: number;
}

function wsUrl(): string {
  const proto = window.location.protocol === "https:" ? "wss:" : "ws:";
  return `${proto}//${window.location.host}/ws`;
}

export function LiveMatchMonitorPage() {
  const [params, setParams] = useSearchParams();
  const matchIdParam = params.get("matchId") ?? "";
  const [matchIdInput, setMatchIdInput] = useState(matchIdParam);
  const [inputError, setInputError] = useState<string | null>(null);
  const [attachedMatchId, setAttachedMatchId] = useState<string | null>(null);
  const connected = attachedMatchId === matchIdParam;
  const [connecting, setConnecting] = useState(false);
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const [retryAttempt, setRetryAttempt] = useState(0);
  const [match, setMatch] = useState<Match | null>(null);
  const [roster, setRoster] = useState<ClassRoster | null>(null);
  const [detailsError, setDetailsError] = useState<string | null>(null);
  const [copyMessage, setCopyMessage] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);
  const startPendingRef = useRef(false);
  const startTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const completedMatchIdRef = useRef<string | null>(null);

  // Match State
  const [status, setStatus] = useState<string>("loading");
  const [mode, setMode] = useState<"ffa" | "teams">("ffa");
  const [lobby, setLobby] = useState<LobbyPlayer[] | null>(null);
  const [players, setPlayers] = useState<SpectatorPlayer[]>([]);
  const [grid, setGrid] = useState<{ width: number; height: number; goalRow: number }>({
    width: 8,
    height: 7,
    goalRow: 6,
  });

  // Combat Visuals
  const [activeAttacks, setActiveAttacks] = useState<ActiveAttackVisual[]>([]);
  const [floatingTexts, setFloatingTexts] = useState<FloatingText[]>([]);
  const [combatEvents, setCombatEvents] = useState<CombatEvent[]>([]);
  const [selectedPlayerId, setSelectedPlayerId] = useState<number | null>(null);

  // Countdown & Match End
  const [countdown, setCountdown] = useState<{ remainingSeconds: number; message: string } | null>(null);
  const [matchEnd, setMatchEnd] = useState<{ winnerId: unknown; reason: string; standings?: unknown[] } | null>(null);
  const [question, setQuestion] = useState<QuestionInfo | null>(null);

  // UI state
  const [muted, setMutedState] = useState(isAudioMuted());
  const [isFullscreen, setIsFullscreen] = useState(false);
  const monitorContainerRef = useRef<HTMLDivElement | null>(null);
  const wsRef = useRef<WebSocket | null>(null);

  // Teacher Interventions State
  const [activeHazard, setActiveHazard] = useState<ActiveHazardVisual | null>(null);
  const [suddenQuestionEvent, setSuddenQuestionEvent] = useState<{
    text: string;
    rewardType: string;
    rewardDamage: number;
    rewardName: string;
    remainingSeconds: number;
  } | null>(null);
  const [hazardDamage, setHazardDamage] = useState<number>(5);
  const [hazardCooldown, setHazardCooldown] = useState<boolean>(false);
  const [suddenCooldown, setSuddenCooldown] = useState<boolean>(false);
  const [showCustomModal, setShowCustomModal] = useState<boolean>(false);

  // Custom Sudden Question Form
  const [customSource, setCustomSource] = useState<"random" | "custom">("random");
  const [customText, setCustomText] = useState("");
  const [customChoices, setCustomChoices] = useState(["", "", "", ""]);
  const [customCorrectIndex, setCustomCorrectIndex] = useState(0);
  const [customRewardType, setCustomRewardType] = useState<"mega_attack" | "super_freeze" | "bonus_move">("mega_attack");
  const [customDamage, setCustomDamage] = useState(35);

  // Countdown timer interval
  useEffect(() => {
    if (!countdown || countdown.remainingSeconds <= 0) return;
    const interval = setInterval(() => {
      setCountdown((prev) => {
        if (!prev || prev.remainingSeconds <= 1) return null;
        return { ...prev, remainingSeconds: prev.remainingSeconds - 1 };
      });
    }, 1000);
    return () => clearInterval(interval);
  }, [countdown]);

  // Sudden Question countdown interval
  useEffect(() => {
    if (!suddenQuestionEvent || suddenQuestionEvent.remainingSeconds <= 0) return;
    const interval = setInterval(() => {
      setSuddenQuestionEvent((prev) => {
        if (!prev || prev.remainingSeconds <= 1) return null;
        return { ...prev, remainingSeconds: prev.remainingSeconds - 1 };
      });
    }, 1000);
    return () => clearInterval(interval);
  }, [suddenQuestionEvent]);

  // Fullscreen change listener
  useEffect(() => {
    function onFsChange() {
      setIsFullscreen(!!document.fullscreenElement);
    }
    document.addEventListener("fullscreenchange", onFsChange);
    return () => document.removeEventListener("fullscreenchange", onFsChange);
  }, []);

  function toggleMute() {
    const next = !muted;
    setAudioMuted(next);
    setMutedState(next);
  }

  function toggleFullscreen() {
    if (!monitorContainerRef.current) return;
    if (!document.fullscreenElement) {
      monitorContainerRef.current.requestFullscreen().catch(() => {});
    } else {
      document.exitFullscreen().catch(() => {});
    }
  }

  function addFloatingText(playerId: number, text: string, color: string) {
    const id = `ft_${Date.now()}_${Math.random()}`;
    setFloatingTexts((prev) => [...prev, { id, playerId, text, color }]);
    setTimeout(() => {
      setFloatingTexts((prev) => prev.filter((f) => f.id !== id));
    }, 1400);
  }

  useEffect(() => {
    if (completedMatchIdRef.current === matchIdParam) {
      setAttachedMatchId(null);
      setConnecting(false);
      return;
    }
    completedMatchIdRef.current = null;

    function resetLiveState() {
      setAttachedMatchId(null);
      setStatus("loading");
      setMode("ffa");
      setLobby(null);
      setPlayers([]);
      setGrid({ width: 8, height: 7, goalRow: 6 });
      setActiveAttacks([]);
      setFloatingTexts([]);
      setCombatEvents([]);
      setSelectedPlayerId(null);
      setCountdown(null);
      setMatchEnd(null);
      setQuestion(null);
      setActiveHazard(null);
      setSuddenQuestionEvent(null);
      setHazardCooldown(false);
      setSuddenCooldown(false);
      setShowCustomModal(false);
      setStarting(false);
      startPendingRef.current = false;
      if (startTimeoutRef.current) clearTimeout(startTimeoutRef.current);
    }

    resetLiveState();
    setMatchIdInput(matchIdParam);
    setConnectionError(null);
    setMatch(null);
    setRoster(null);
    setDetailsError(null);
    setCopyMessage(null);
    setConnecting(false);
    if (!matchIdParam) return;
    if (!/^\d+$/.test(matchIdParam) || !Number.isSafeInteger(Number(matchIdParam)) || Number(matchIdParam) <= 0) {
      setConnectionError("Enter a valid positive numeric match ID from Match Setup.");
      return;
    }
    let disposed = false;
    let attached = false;
    let dashboardActive = false;
    setConnecting(true);
    getMatch(Number(matchIdParam)).then(async (loadedMatch) => {
      if (disposed) return;
      setMatch(loadedMatch);
      const loadedRoster = await getClass(loadedMatch.class_roster_id);
      if (!disposed) setRoster(loadedRoster);
    }).catch((err) => {
      if (!disposed) setDetailsError(`Could not load invitation details: ${err instanceof Error ? err.message : String(err)}`);
    });
    const ws = new WebSocket(wsUrl());
    wsRef.current = ws;
    const attachTimeout = setTimeout(() => disconnect("Could not attach to this match. Retry to reconnect."), 10000);

    function disconnect(message: string) {
      if (disposed || wsRef.current !== ws) return;
      clearTimeout(attachTimeout);
      wsRef.current = null;
      const completed = completedMatchIdRef.current === matchIdParam;
      if (completed) setAttachedMatchId(null);
      else resetLiveState();
      setConnecting(false);
      setConnectionError(completed ? "Completed match is offline. Results are preserved in this view; reconnecting is unavailable." : message);
      ws.close();
    }

    ws.onopen = () => {
      if (disposed || wsRef.current !== ws) return;
      ws.send(JSON.stringify({ type: "hello", payload: { role: "teacher", token: getToken() } }));
    };

    ws.onmessage = (evt) => {
      if (disposed || wsRef.current !== ws) return;
      const msg = JSON.parse(evt.data);
      if (msg.type === "lobby_state" || msg.type === "live_dashboard") {
        if (msg.payload.matchId !== Number(matchIdParam)) return;
        attached = true;
        clearTimeout(attachTimeout);
        setAttachedMatchId(matchIdParam);
        setConnecting(false);
      } else if (!attached && msg.type !== "hello_ack" && msg.type !== "error") {
        return;
      }

      switch (msg.type) {
        case "hello_ack":
          ws.send(JSON.stringify({ type: "teacher_join_match", payload: { matchId: Number(matchIdParam) } }));
          break;

        case "lobby_state":
          setLobby(msg.payload.players);
          if (msg.payload.mode) setMode(msg.payload.mode);
          break;

        case "character_locked":
          setLobby((prev) => prev?.map((p) => p.playerId === msg.payload.playerId ? { ...p, characterId: msg.payload.characterId } : p) ?? null);
          break;

        case "live_dashboard": {
          const payload = msg.payload;
          dashboardActive = payload.status === "active";
          if (payload.status === "completed") completedMatchIdRef.current = matchIdParam;
          setStatus(payload.status);
          if (dashboardActive && payload.timerStartedAt) {
            const remainingSeconds = Math.max(0, Math.ceil((payload.timerStartedAt + payload.timerSeconds * 1000 - Date.now()) / 1000));
            setCountdown(remainingSeconds > 0 ? { remainingSeconds, message: "Race to the finish!" } : null);
          }
          if (payload.mode) setMode(payload.mode);
          if (payload.grid) {
            setGrid({
              width: payload.grid.width,
              height: payload.grid.height,
              goalRow: payload.grid.goalRow ?? payload.grid.height - 1,
            });
          }
          if (payload.players) {
            setPlayers(
              payload.players.map((p: any) => ({
                playerId: p.playerId,
                name: p.name,
                characterId: p.characterId ?? null,
                team: p.team ?? null,
                hp: p.hp ?? 45,
                maxHp: p.maxHp ?? p.hp ?? 45,
                alive: p.alive ?? true,
                streak: p.streak ?? 0,
                pos: p.pos ?? { x: 0, y: 0 },
                goalReached: p.goalReached ?? false,
                finishRank: p.finishRank ?? null,
                frozen: p.frozen ?? false,
              }))
            );
          }
          break;
        }

        case "match_start": {
          startPendingRef.current = false;
          setStarting(false);
          if (startTimeoutRef.current) clearTimeout(startTimeoutRef.current);
          if (dashboardActive) break;
          setStatus("active");
          const payload = msg.payload;
          if (payload.teams !== undefined) setMode(payload.teams ? "teams" : "ffa");
          if (payload.arenaLayout?.grid) {
            setGrid({
              width: payload.arenaLayout.grid.width,
              height: payload.arenaLayout.grid.height,
              goalRow: payload.arenaLayout.goalRow ?? payload.arenaLayout.grid.height - 1,
            });
          }
          if (payload.players) {
            setPlayers(
              payload.players.map((p: any) => ({
                playerId: p.playerId,
                name: p.name,
                characterId: p.characterId ?? null,
                team: p.team ?? null,
                hp: p.hp ?? 45,
                maxHp: p.maxHp ?? p.hp ?? 45,
                alive: p.alive ?? true,
                streak: 0,
                pos: p.pos ?? { x: 0, y: 0 },
                goalReached: false,
                finishRank: null,
                frozen: false,
              }))
            );
          }
          setCombatEvents((prev) => [
            {
              id: `ev_${Date.now()}`,
              type: "system",
              timestamp: Date.now(),
              text: "🏁 Match started! Racers have left the starting line.",
            },
            ...prev,
          ]);
          break;
        }

        case "player_advanced": {
          const p = msg.payload;
          setPlayers((prev) =>
            prev.map((item) =>
              item.playerId === p.playerId
                ? {
                    ...item,
                    pos: p.newGridPos ?? item.pos,
                    hp: p.hp ?? item.hp,
                    maxHp: p.maxHp ?? item.maxHp,
                    alive: p.alive ?? item.alive,
                    streak: p.streak ?? item.streak,
                    goalReached: p.goalReached ?? item.goalReached,
                    finishRank: p.finishRank ?? item.finishRank,
                    frozen: p.frozen ?? item.frozen,
                  }
                : item
            )
          );

          if (p.reason === "bonus_move") {
            playAdvanceSound();
            addFloatingText(p.playerId, "⚡ +1 STEP!", "#06b6d4");
            setCombatEvents((prev) => [
              {
                id: `ev_${Date.now()}_${Math.random()}`,
                type: "bonus_move",
                timestamp: Date.now(),
                text: `${p.name} dashed forward with a bonus move!`,
                attackerName: p.name,
              },
              ...prev,
            ]);
          } else if (p.correct) {
            playAdvanceSound();
            addFloatingText(p.playerId, "✨ +1 Step", "#22c55e");
          }
          break;
        }

        case "attack_result": {
          const atk = msg.payload;
          playAttackSound();

          // Update target HP immediately
          setPlayers((prev) =>
            prev.map((item) =>
              item.playerId === atk.targetId
                ? {
                    ...item,
                    hp: atk.targetHpAfter !== undefined ? atk.targetHpAfter : Math.max(0, item.hp - atk.damage),
                    alive: atk.eliminated ? false : item.alive,
                  }
                : item
            )
          );

          // Animate attack beam connecting attacker to target
          const atkVisId = `atk_${Date.now()}_${Math.random()}`;
          setActiveAttacks((prev) => [
            ...prev,
            {
              id: atkVisId,
              attackerId: atk.attackerId,
              targetId: atk.targetId,
              damage: atk.damage,
              type: "attack",
            },
          ]);
          setTimeout(() => {
            setActiveAttacks((prev) => prev.filter((a) => a.id !== atkVisId));
          }, 1100);

          // Floating damage number on target
          addFloatingText(atk.targetId, `-${atk.damage} HP`, "#ef4444");

          setCombatEvents((prev) => [
            {
              id: `ev_${Date.now()}_${Math.random()}`,
              type: "attack",
              timestamp: Date.now(),
              text: `${atk.attackerName || `Player ${atk.attackerId}`} attacked ${atk.targetName || `Player ${atk.targetId}`} for ${atk.damage} DMG`,
              attackerName: atk.attackerName || `Player ${atk.attackerId}`,
              attackerCharacterId: atk.attackerCharacterId,
              targetName: atk.targetName || `Player ${atk.targetId}`,
              targetCharacterId: atk.targetCharacterId,
              damage: atk.damage,
              targetHpAfter: atk.targetHpAfter,
            },
            ...prev,
          ]);
          break;
        }

        case "freeze_result": {
          const frz = msg.payload;
          playFreezeSound();

          // Mark target frozen
          setPlayers((prev) =>
            prev.map((item) => (item.playerId === frz.targetId ? { ...item, frozen: true } : item))
          );

          const frzVisId = `frz_${Date.now()}_${Math.random()}`;
          setActiveAttacks((prev) => [
            ...prev,
            {
              id: frzVisId,
              attackerId: frz.casterId,
              targetId: frz.targetId,
              type: "freeze",
            },
          ]);
          setTimeout(() => {
            setActiveAttacks((prev) => prev.filter((a) => a.id !== frzVisId));
          }, 1100);

          addFloatingText(frz.targetId, "❄️ FROZEN!", "#38bdf8");

          setCombatEvents((prev) => [
            {
              id: `ev_${Date.now()}_${Math.random()}`,
              type: "freeze",
              timestamp: Date.now(),
              text: `${frz.casterName || `Player ${frz.casterId}`} froze ${frz.targetName || `Player ${frz.targetId}`}!`,
              attackerName: frz.casterName || `Player ${frz.casterId}`,
              attackerCharacterId: frz.casterCharacterId,
              targetName: frz.targetName || `Player ${frz.targetId}`,
              targetCharacterId: frz.targetCharacterId,
            },
            ...prev,
          ]);
          break;
        }

        case "match_timer_start": {
          playVictorySound();
          setCountdown({
            remainingSeconds: msg.payload.remainingSeconds,
            message: msg.payload.message || "1st Place Finished! Race to the finish!",
          });
          setCombatEvents((prev) => [
            {
              id: `ev_${Date.now()}`,
              type: "system",
              timestamp: Date.now(),
              text: `⏱️ ${msg.payload.firstFinisherName} reached 1st place! ${msg.payload.remainingSeconds}s remaining for others to finish!`,
            },
            ...prev,
          ]);
          break;
        }

        case "player_finished": {
          playVictorySound();
          const p = msg.payload;
          setPlayers((prev) =>
            prev.map((item) =>
              item.playerId === p.playerId
                ? { ...item, goalReached: true, finishRank: p.finishRank, pos: p.pos ?? item.pos }
                : item
            )
          );

          setCombatEvents((prev) => [
            {
              id: `ev_${Date.now()}_${Math.random()}`,
              type: "finish",
              timestamp: Date.now(),
              text: `🏁 ${p.name} reached the goal line! (${p.finishRank === 1 ? "1st Place" : `${p.finishRank}th Place`})`,
              attackerName: p.name,
              rank: p.finishRank,
            },
            ...prev,
          ]);
          break;
        }

        case "player_eliminated": {
          const elimId = msg.payload.playerId;
          setPlayers((prev) =>
            prev.map((item) => (item.playerId === elimId ? { ...item, alive: false, hp: 0 } : item))
          );
          const targetPlayer = players.find((p) => p.playerId === elimId);
          setCombatEvents((prev) => [
            {
              id: `ev_${Date.now()}_${Math.random()}`,
              type: "eliminated",
              timestamp: Date.now(),
              text: `💀 ${targetPlayer?.name || `Player ${elimId}`} was eliminated!`,
              targetName: targetPlayer?.name || `Player ${elimId}`,
            },
            ...prev,
          ]);
          break;
        }

        case "question_push": {
          setQuestion(msg.payload);
          break;
        }

        case "arena_hazard": {
          const haz = msg.payload;
          playAttackSound();

          const hazId = `haz_${Date.now()}`;
          setActiveHazard({ id: hazId, type: haz.hazardType, name: haz.hazardName || "Fireball Rain" });
          setTimeout(() => setActiveHazard((prev) => (prev?.id === hazId ? null : prev)), 2200);

          if (haz.targets) {
            setPlayers((prev) =>
              prev.map((item) => {
                const matched = haz.targets.find((t: any) => t.playerId === item.playerId);
                if (matched) {
                  return {
                    ...item,
                    hp: matched.hpAfter,
                    alive: matched.eliminated ? false : item.alive,
                  };
                }
                return item;
              })
            );

            haz.targets.forEach((t: any) => {
              addFloatingText(t.playerId, `-${t.damage} HP 🔥`, "#ef4444");
            });
          }

          setCombatEvents((prev) => [
            {
              id: `ev_${Date.now()}_${Math.random()}`,
              type: "attack",
              timestamp: Date.now(),
              text: `🔥 ARENA HAZARD: Fireballs struck all racers for ${haz.damage} DMG!`,
            },
            ...prev,
          ]);
          break;
        }

        case "sudden_question_started": {
          playAdvanceSound();
          const sq = msg.payload;
          setSuddenQuestionEvent({
            text: sq.text,
            rewardType: sq.rewardType,
            rewardDamage: sq.rewardDamage,
            rewardName: sq.rewardName,
            remainingSeconds: Math.round((sq.timeLimitMs || 20000) / 1000),
          });

          setCombatEvents((prev) => [
            {
              id: `ev_${Date.now()}_${Math.random()}`,
              type: "bonus_move",
              timestamp: Date.now(),
              text: `⚡ SUDDEN QUESTION EVENT: High-stakes challenge! Correct answer earns ${sq.rewardName} (${sq.rewardDamage} DMG)!`,
            },
            ...prev,
          ]);
          break;
        }

        case "match_end": {
          completedMatchIdRef.current = matchIdParam;
          startPendingRef.current = false;
          setStarting(false);
          if (startTimeoutRef.current) clearTimeout(startTimeoutRef.current);
          playVictorySound();
          setStatus("completed");
          setMatchEnd(msg.payload);
          setCountdown(null);
          setSuddenQuestionEvent(null);
          setShowCustomModal(false);
          setCombatEvents((prev) => [
            {
              id: `ev_${Date.now()}`,
              type: "system",
              timestamp: Date.now(),
              text: `🏆 Match concluded! Winner: ${JSON.stringify(msg.payload.winnerId)} (${msg.payload.reason})`,
            },
            ...prev,
          ]);
          break;
        }

        case "error": {
          const message = `${msg.payload.code}: ${msg.payload.message}`;
          if (!attached) {
            disconnect(message);
            break;
          }
          setConnectionError(message);
          startPendingRef.current = false;
          setStarting(false);
          if (startTimeoutRef.current) clearTimeout(startTimeoutRef.current);
          setCombatEvents((prev) => [
            {
              id: `ev_${Date.now()}`,
              type: "system",
              timestamp: Date.now(),
              text: `⚠️ Error: ${msg.payload.code} — ${msg.payload.message}`,
            },
            ...prev,
          ]);
          break;
        }
      }
    };

    ws.onerror = () => disconnect("WebSocket connection failed. Retry to reconnect.");
    ws.onclose = () => disconnect("Disconnected from this match. Retry to reconnect.");

    return () => {
      disposed = true;
      clearTimeout(attachTimeout);
      if (startTimeoutRef.current) clearTimeout(startTimeoutRef.current);
      if (wsRef.current === ws) wsRef.current = null;
      ws.close();
    };
  }, [matchIdParam, retryAttempt]);

  function connectToMatch() {
    const trimmed = matchIdInput.trim();
    if (!trimmed) return;
    if (!/^\d+$/.test(trimmed) || !Number.isSafeInteger(Number(trimmed)) || Number(trimmed) <= 0) {
      setInputError(
        `"${trimmed}" isn't a valid match ID — that's the numeric ID shown after creating a match (e.g. "12"), not the join code students use.`
      );
      return;
    }
    setInputError(null);
    setParams({ matchId: trimmed });
  }

  const readyCount = lobby?.filter((p) => p.ready && p.characterId).length ?? 0;
  const excludedCount = (lobby?.length ?? 0) - readyCount;
  const canStart = connected && status === "lobby" && readyCount >= 2 && !starting;
  const joinLink = match?.id === Number(matchIdParam) && roster?.class_code && match.join_code
    ? `${window.location.origin}/play/?${new URLSearchParams({ classCode: roster.class_code, joinCode: match.join_code })}`
    : "";

  async function copyJoinLink() {
    const ws = wsRef.current;
    try {
      await navigator.clipboard.writeText(joinLink);
      if (wsRef.current === ws) setCopyMessage("Student link copied.");
    } catch {
      if (wsRef.current === ws) setCopyMessage("Could not copy automatically. Select and copy the student link below.");
    }
  }

  function startMatch() {
    const ws = wsRef.current;
    if (!canStart || startPendingRef.current || ws?.readyState !== WebSocket.OPEN) return;
    if (excludedCount > 0 && !window.confirm(`Start with ${readyCount} ready players? ${excludedCount} student(s) without both Ready and a character pick will be excluded.`)) return;
    startPendingRef.current = true;
    setStarting(true);
    setConnectionError(null);
    ws.send(JSON.stringify({ type: "teacher_start_match", payload: {} }));
    startTimeoutRef.current = setTimeout(() => {
      if (wsRef.current !== ws) return;
      setConnectionError("No start confirmation received. Retry to check the match before starting again.");
      ws.close();
    }, 10000);
  }

  function killMatch() {
    if (!connected || wsRef.current?.readyState !== WebSocket.OPEN) return;
    if (!window.confirm("Are you sure you want to end this match immediately for all students?")) return;
    wsRef.current?.send(JSON.stringify({ type: "teacher_kill_match", payload: { matchId: Number(matchIdParam) } }));
  }

  function triggerHazard(dmg = hazardDamage) {
    if (hazardCooldown || !connected || wsRef.current?.readyState !== WebSocket.OPEN) return;
    setHazardCooldown(true);
    const ws = wsRef.current;
    setTimeout(() => { if (wsRef.current === ws) setHazardCooldown(false); }, 3000);

    wsRef.current?.send(
      JSON.stringify({
        type: "teacher_trigger_hazard",
        payload: { matchId: Number(matchIdParam), hazardType: "fireball_rain", damage: dmg },
      })
    );
  }

  function triggerQuickSuddenQuestion() {
    if (suddenCooldown || !connected || wsRef.current?.readyState !== WebSocket.OPEN) return;
    setSuddenCooldown(true);
    const ws = wsRef.current;
    setTimeout(() => { if (wsRef.current === ws) setSuddenCooldown(false); }, 5000);

    wsRef.current?.send(
      JSON.stringify({
        type: "teacher_trigger_sudden_question",
        payload: {
          matchId: Number(matchIdParam),
          rewardType: "mega_attack",
          rewardDamage: 35,
          rewardName: "Mega Strike",
        },
      })
    );
  }

  function submitCustomSuddenQuestion() {
    if (suddenCooldown || !connected || wsRef.current?.readyState !== WebSocket.OPEN) return;
    setShowCustomModal(false);
    setSuddenCooldown(true);
    const ws = wsRef.current;
    setTimeout(() => { if (wsRef.current === ws) setSuddenCooldown(false); }, 5000);

    const payload: any = {
      matchId: Number(matchIdParam),
      rewardType: customRewardType,
      rewardDamage: customDamage,
      rewardName:
        customRewardType === "mega_attack"
          ? "Mega Strike"
          : customRewardType === "super_freeze"
          ? "Super Freeze"
          : "Mega Dash",
    };

    if (customSource === "custom" && customText.trim()) {
      payload.text = customText.trim();
      payload.choices = customChoices.map((c, i) => c.trim() || `Option ${i + 1}`);
      payload.correctIndex = customCorrectIndex;
    }

    wsRef.current?.send(
      JSON.stringify({
        type: "teacher_trigger_sudden_question",
        payload,
      })
    );
  }

  if (!matchIdParam) {
    return (
      <div className="page" style={{ maxWidth: 460 }}>
        <h1>Live Match Spectator & Monitor</h1>
        <p className="muted">Enter the numeric match ID from Match Setup to watch the live match in real-time.</p>
        <div className="card" style={{ display: "flex", gap: "10px", marginTop: "1rem" }}>
          <input
            placeholder="Match ID (e.g. 1)"
            value={matchIdInput}
            onChange={(e) => setMatchIdInput(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && connectToMatch()}
            style={{ flex: 1 }}
          />
          <button className="btn btn-primary" onClick={connectToMatch}>
            Watch Match
          </button>
        </div>
        {inputError && <p className="error-text">{inputError}</p>}
      </div>
    );
  }

  return (
    <div ref={monitorContainerRef} className={`live-spectator-page ${isFullscreen ? "fullscreen-mode" : ""}`}>
      {/* Top Header Bar */}
      <div className="spectator-top-bar">
        <div className="spectator-title-group">
          <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
            <span style={{ fontSize: "1.5rem" }}>🏟️</span>
            <div>
              <h1 className="spectator-title">
                Live Match #{matchIdParam}
                <span className={`status-badge status-${status}`}>{connected ? status.toUpperCase() : status === "completed" ? "COMPLETED (OFFLINE)" : connecting ? "CONNECTING" : "OFFLINE"}</span>
              </h1>
              <div className="spectator-subtitle">
                <span className={`connection-dot ${connected ? "connected" : "disconnected"}`} />
                {connected ? "Connected to match" : connecting ? "Connecting to match..." : "Disconnected"} · Mode: <strong>{connected || status === "completed" ? mode.toUpperCase() : "--"}</strong> · Racers: <strong>{connected || status === "completed" ? status === "lobby" ? lobby?.length ?? 0 : players.length : "--"}</strong>
              </div>
            </div>
          </div>
        </div>

        {/* Action Controls */}
        <div className="spectator-controls-group">
          {!connected && status !== "completed" && <button className="btn" onClick={() => setRetryAttempt((n) => n + 1)} disabled={connecting}>Retry</button>}
          <button
            className="btn btn-tool"
            onClick={toggleMute}
            title={muted ? "Unmute sound effects" : "Mute sound effects"}
          >
            {muted ? "🔇 Sound Off" : "🔊 Sound On"}
          </button>

          <button
            className="btn btn-tool"
            onClick={toggleFullscreen}
            title="Toggle Fullscreen (for Projector / Smartboard)"
          >
            {isFullscreen ? "🗗 Exit Fullscreen" : "⛶ Projector Fullscreen"}
          </button>

          {(status === "lobby" || status === "loading") && (
            <button className="btn btn-primary" style={{ fontWeight: 700 }} onClick={startMatch} disabled={!canStart}>
              {starting ? "Starting..." : "▶ Start Match"}
            </button>
          )}

          {status !== "completed" && (
            <button
              className="btn btn-danger"
              style={{ fontWeight: 600 }}
              onClick={killMatch}
              disabled={!connected || starting}
            >
              {status === "active" ? "End Match" : "Cancel Match"}
            </button>
          )}
        </div>
      </div>

      {connectionError && <p className="error-text" role="alert">{connectionError}</p>}

      {status !== "completed" && (status === "lobby" || !connected) && (
        <div className="card">
          <h2>Invite students{roster ? ` to ${roster.name}` : ""}</h2>
          {detailsError ? <p className="error-text" role="alert">{detailsError} <button className="btn" onClick={() => setRetryAttempt((n) => n + 1)}>Retry details</button></p> : !roster && !connectionError && <p role="status">Loading invitation details...</p>}
          <div className="row" style={{ gap: "2rem" }}>
            <div>Class code<strong style={{ display: "block", fontSize: "1.75rem", letterSpacing: "0.08em" }}>{roster?.class_code ?? "--"}</strong></div>
            <div>Match join code<strong style={{ display: "block", fontSize: "1.75rem", letterSpacing: "0.08em" }}>{match?.join_code ?? "--"}</strong></div>
            <button className="btn btn-primary" disabled={!joinLink} onClick={copyJoinLink}>Copy student link</button>
          </div>
          {joinLink && <div className="field" style={{ marginTop: "1rem" }}><label htmlFor="student-link">Student link (no PIN or login token)</label><input id="student-link" readOnly value={joinLink} onFocus={(e) => e.currentTarget.select()} /></div>}
          {copyMessage && <p role="status">{copyMessage}</p>}
          <p className="muted">Students open the link, sign in, choose a character{mode === "teams" ? " and a team" : ""}, then select Ready. Only you can start the match.</p>
        </div>
      )}

      {/* Countdown Alert Banner */}
      {countdown && (
        <div className="countdown-alert-banner">
          <span className="countdown-icon">⏱️</span>
          <div className="countdown-body">
            <strong>{countdown.message}</strong>
            <span className="countdown-seconds">{countdown.remainingSeconds}s REMAINING</span>
          </div>
        </div>
      )}

      {/* Match Completed Banner */}
      {matchEnd && (
        <div className="match-completed-banner card">
          <div style={{ fontSize: "2rem" }}>🏆</div>
          <div>
            <h2 style={{ margin: 0 }}>Match Concluded!</h2>
            <p style={{ margin: "4px 0 0" }}>
              Winner: <strong>{JSON.stringify(matchEnd.winnerId)}</strong> · Reason: <em>{matchEnd.reason}</em>
            </p>
          </div>
        </div>
      )}

      {/* Lobby State Table (if pre-match) */}
      {status === "lobby" && lobby && (
        <div className="card" style={{ marginBottom: "1.25rem" }}>
          <h2>Racers in Lobby ({lobby.length})</h2>
          <p role="status"><strong>{readyCount} / {lobby.length} ready with a character picked.</strong> At least 2 are required to start.</p>
          {excludedCount > 0 && <p className="muted">{excludedCount} student(s) will be excluded if you start before they are ready with a character picked.</p>}
          {lobby.length === 0 && <p className="muted">Waiting for students to join using the link or codes above.</p>}
          <table style={{ width: "100%", marginTop: "0.5rem" }}>
            <thead>
              <tr>
                <th>Student Name</th>
                <th>Hero Pick</th>
                <th>Team</th>
                <th>Ready State</th>
              </tr>
            </thead>
            <tbody>
              {lobby.map((p) => (
                <tr key={p.playerId}>
                  <td><strong>{p.name}</strong></td>
                  <td>{p.characterId ?? <span className="muted">Choosing...</span>}</td>
                  <td>{p.team ?? <span className="muted">—</span>}</td>
                  <td>
                    {p.ready && p.characterId ? (
                      <span style={{ color: "#22c55e", fontWeight: 700 }}>✓ READY</span>
                    ) : (
                      <span className="muted">Not ready</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Sudden Question Active Alert Banner */}
      {suddenQuestionEvent && (
        <div className="sudden-question-alert-banner">
          <div className="sq-badge">⚡ SUDDEN QUESTION IN PROGRESS</div>
          <div className="sq-info">
            <strong>{suddenQuestionEvent.text}</strong>
            <span className="sq-reward-tag">
              🏆 Reward: {suddenQuestionEvent.rewardName} ({suddenQuestionEvent.rewardDamage} DMG)
            </span>
          </div>
          <div className="sq-timer">{suddenQuestionEvent.remainingSeconds}s remaining</div>
        </div>
      )}

      {/* Teacher Live Interventions Deck (Active Match Only) */}
      {status === "active" && (
        <div className="teacher-intervention-deck card">
          <div className="deck-header">
            <div className="deck-title">
              <span className="deck-icon">🎮</span>
              <div>
                <strong>Teacher Match Controls & Interventions</strong>
                <span className="deck-subtitle">Influence the live colosseum in real-time</span>
              </div>
            </div>
            <div className="deck-status-badges">
              {hazardCooldown && <span className="deck-cooldown-badge">🔥 Fireball Cooling Down</span>}
              {suddenCooldown && <span className="deck-cooldown-badge">⚡ Sudden Question Ready Soon</span>}
            </div>
          </div>

          <div className="intervention-buttons-grid">
            {/* Fireball Rain Hazard */}
            <div className="intervention-card card-fire">
              <div className="int-card-header">
                <span className="int-icon">🔥</span>
                <div>
                  <h4>Fireball Rain (Low Effect)</h4>
                  <p>Send low-damage fireballs raining on all living racers</p>
                </div>
              </div>
              <div className="int-card-controls">
                <div className="damage-chips">
                  {[5, 8, 10].map((dmg) => (
                    <button
                      key={dmg}
                      type="button"
                      className={`chip-btn ${hazardDamage === dmg ? "active" : ""}`}
                      onClick={() => setHazardDamage(dmg)}
                    >
                      {dmg} HP {dmg === 5 ? "(Light)" : dmg === 8 ? "(Medium)" : "(Spicy)"}
                    </button>
                  ))}
                </div>
                <button
                  type="button"
                  className="btn btn-hazard-fire"
                  disabled={!connected || hazardCooldown}
                  onClick={() => triggerHazard(hazardDamage)}
                >
                  {hazardCooldown ? "⏳ Recharging..." : `🔥 Send Fireballs (-${hazardDamage} HP to Everyone)`}
                </button>
              </div>
            </div>

            {/* Sudden Question Event */}
            <div className="intervention-card card-sudden">
              <div className="int-card-header">
                <span className="int-icon">⚡</span>
                <div>
                  <h4>Sudden Question Event</h4>
                  <p>Push an instant high-stakes question granting 35 DMG Mega Attacks</p>
                </div>
              </div>
              <div className="int-card-controls">
                <div style={{ display: "flex", gap: "8px" }}>
                  <button
                    type="button"
                    className="btn btn-sudden-gold"
                    style={{ flex: 1 }}
                    disabled={!connected || suddenCooldown}
                    onClick={triggerQuickSuddenQuestion}
                  >
                    {suddenCooldown ? "⏳ Recharging..." : "⚡ Quick Sudden Question (35 DMG Mega Strike)"}
                  </button>
                  <button
                    type="button"
                    className="btn btn-tool"
                    title="Customize sudden question text and rewards"
                    disabled={!connected || suddenCooldown}
                    onClick={() => setShowCustomModal(true)}
                  >
                    ⚙️ Custom
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Custom Sudden Question Modal */}
      {showCustomModal && (
        <div className="modal-backdrop" onClick={() => setShowCustomModal(false)}>
          <div className="modal-content card" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 540 }}>
            <div className="modal-header">
              <h3>⚡ Configure Sudden Question</h3>
              <button className="btn-close" onClick={() => setShowCustomModal(false)}>
                ✕
              </button>
            </div>
            <div className="modal-body" style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              {/* Question Source */}
              <div>
                <label style={{ fontWeight: 600, display: "block", marginBottom: "6px" }}>Question Source</label>
                <div style={{ display: "flex", gap: "10px" }}>
                  <label style={{ display: "flex", alignItems: "center", gap: "6px", cursor: "pointer" }}>
                    <input
                      type="radio"
                      name="qSource"
                      checked={customSource === "random"}
                      onChange={() => setCustomSource("random")}
                    />
                    Pick Random Question from Bank
                  </label>
                  <label style={{ display: "flex", alignItems: "center", gap: "6px", cursor: "pointer" }}>
                    <input
                      type="radio"
                      name="qSource"
                      checked={customSource === "custom"}
                      onChange={() => setCustomSource("custom")}
                    />
                    Write Custom Question On-The-Fly
                  </label>
                </div>
              </div>

              {/* Custom Question Inputs */}
              {customSource === "custom" && (
                <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                  <div>
                    <label style={{ fontSize: "0.85rem", fontWeight: 600 }}>Question Text</label>
                    <input
                      placeholder="e.g. What is the powerhouse of the cell?"
                      value={customText}
                      onChange={(e) => setCustomText(e.target.value)}
                      style={{ width: "100%", marginTop: "4px" }}
                    />
                  </div>
                  <div>
                    <label style={{ fontSize: "0.85rem", fontWeight: 600 }}>Answer Choices (Select the correct one):</label>
                    <div style={{ display: "flex", flexDirection: "column", gap: "6px", marginTop: "4px" }}>
                      {customChoices.map((choice, idx) => (
                        <div key={idx} style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                          <input
                            type="radio"
                            name="correctChoice"
                            checked={customCorrectIndex === idx}
                            onChange={() => setCustomCorrectIndex(idx)}
                            title="Mark as correct answer"
                          />
                          <input
                            placeholder={`Choice ${idx + 1}`}
                            value={choice}
                            onChange={(e) => {
                              const next = [...customChoices];
                              next[idx] = e.target.value;
                              setCustomChoices(next);
                            }}
                            style={{ flex: 1 }}
                          />
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              )}

              {/* Reward Selection */}
              <div>
                <label style={{ fontWeight: 600, display: "block", marginBottom: "6px" }}>Reward for Correct Answer</label>
                <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                  <label
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "10px",
                      padding: "8px 12px",
                      background: customRewardType === "mega_attack" ? "rgba(239, 68, 68, 0.15)" : "rgba(255,255,255,0.03)",
                      borderRadius: "6px",
                      border: customRewardType === "mega_attack" ? "1px solid #ef4444" : "1px solid rgba(255,255,255,0.1)",
                      cursor: "pointer",
                    }}
                  >
                    <input
                      type="radio"
                      name="rewardType"
                      checked={customRewardType === "mega_attack"}
                      onChange={() => {
                        setCustomRewardType("mega_attack");
                        setCustomDamage(35);
                      }}
                    />
                    <div>
                      <strong>💥 High HP Mega Attack ({customDamage} DMG)</strong>
                      <div style={{ fontSize: "0.8rem", opacity: 0.8 }}>Unleashes massive devastating damage on any chosen opponent</div>
                    </div>
                  </label>

                  <label
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "10px",
                      padding: "8px 12px",
                      background: customRewardType === "super_freeze" ? "rgba(56, 189, 248, 0.15)" : "rgba(255,255,255,0.03)",
                      borderRadius: "6px",
                      border: customRewardType === "super_freeze" ? "1px solid #38bdf8" : "1px solid rgba(255,255,255,0.1)",
                      cursor: "pointer",
                    }}
                  >
                    <input
                      type="radio"
                      name="rewardType"
                      checked={customRewardType === "super_freeze"}
                      onChange={() => {
                        setCustomRewardType("super_freeze");
                        setCustomDamage(15);
                      }}
                    />
                    <div>
                      <strong>❄️ Super Freeze (Freeze + 15 DMG)</strong>
                      <div style={{ fontSize: "0.8rem", opacity: 0.8 }}>Freezes target racer and deals 15 damage</div>
                    </div>
                  </label>

                  <label
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "10px",
                      padding: "8px 12px",
                      background: customRewardType === "bonus_move" ? "rgba(234, 179, 8, 0.15)" : "rgba(255,255,255,0.03)",
                      borderRadius: "6px",
                      border: customRewardType === "bonus_move" ? "1px solid #eab308" : "1px solid rgba(255,255,255,0.1)",
                      cursor: "pointer",
                    }}
                  >
                    <input
                      type="radio"
                      name="rewardType"
                      checked={customRewardType === "bonus_move"}
                      onChange={() => {
                        setCustomRewardType("bonus_move");
                        setCustomDamage(0);
                      }}
                    />
                    <div>
                      <strong>⚡ Mega Dash (+2 Steps toward Goal)</strong>
                      <div style={{ fontSize: "0.8rem", opacity: 0.8 }}>Surges 2 rows forward on the colosseum track</div>
                    </div>
                  </label>
                </div>
              </div>

              {/* Damage override slider if mega_attack */}
              {customRewardType === "mega_attack" && (
                <div>
                  <label style={{ fontSize: "0.85rem", fontWeight: 600 }}>
                    Attack Damage: <strong>{customDamage} DMG</strong>
                  </label>
                  <input
                    type="range"
                    min="25"
                    max="45"
                    value={customDamage}
                    onChange={(e) => setCustomDamage(Number(e.target.value))}
                    style={{ width: "100%", marginTop: "4px" }}
                  />
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.75rem", opacity: 0.7 }}>
                    <span>25 DMG (Heavy)</span>
                    <span>35 DMG (Devastating)</span>
                    <span>45 DMG (Near-One-Shot)</span>
                  </div>
                </div>
              )}
            </div>

            <div className="modal-footer" style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "1.25rem" }}>
              <button className="btn" onClick={() => setShowCustomModal(false)}>
                Cancel
              </button>
              <button className="btn btn-primary" onClick={submitCustomSuddenQuestion} disabled={!connected || suddenCooldown}>
                🚀 Launch Sudden Question to Everyone
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Main Live Match Layout: Arena Track (Left) & Standings/Combat (Right) */}
      <div className="spectator-grid-layout">
        {/* Left Column: Visual Colosseum Race Track */}
        <div className="spectator-track-column">
          <ArenaTrackView
            grid={grid}
            players={players}
            activeAttacks={activeAttacks}
            floatingTexts={floatingTexts}
            selectedPlayerId={selectedPlayerId}
            onSelectPlayer={setSelectedPlayerId}
            mode={mode}
            activeHazard={activeHazard}
          />
        </div>

        {/* Right Column: Standings Leaderboard & Live Combat Feed */}
        <div className="spectator-side-column">
          {/* Live Standings & Health Vitals */}
          <LeaderboardStandings
            players={players}
            goalRow={grid.goalRow}
            selectedPlayerId={selectedPlayerId}
            onSelectPlayer={setSelectedPlayerId}
            mode={mode}
          />

          {/* Active Question Preview */}
          {question && status === "active" && (
            <div className="card" style={{ padding: "0.85rem 1rem", fontSize: "0.88rem" }}>
              <div style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "4px" }}>
                <span>📝</span>
                <strong>Current Classroom Question</strong>
              </div>
              <p style={{ margin: "4px 0", fontWeight: 600 }}>{question.text}</p>
              <ul style={{ margin: "4px 0 0", paddingLeft: "1.2rem", fontSize: "0.82rem", opacity: 0.85 }}>
                {question.choices.map((c, i) => (
                  <li key={i}>{c}</li>
                ))}
              </ul>
            </div>
          )}

          {/* Live Combat Event Feed */}
          <CombatFeed events={combatEvents} onClear={status === "completed" ? undefined : () => setCombatEvents([])} />
        </div>
      </div>
    </div>
  );
}
