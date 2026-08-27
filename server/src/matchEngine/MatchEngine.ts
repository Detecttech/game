import {
  DEFAULT_BONUS_MOVE_STEPS,
  createMatchState,
  type GridPos,
  type MatchState,
  type PlayerState,
} from "./MatchState";
import { getCharacter } from "./characters/characterConfig";
import { rollReward, expireStaleRewards, consumeReward } from "./RewardResolver";
import { attackFor, resolveAttack, tickDot, type AttackOutcome } from "./CombatResolver";

export function createMatch(
  matchId: number,
  mode: "ffa" | "teams",
  maxRounds = 20,
  gridHeight?: number,
  timerSeconds = 30
): MatchState {
  return createMatchState(matchId, mode, maxRounds, gridHeight, timerSeconds);
}

export function addPlayer(
  state: MatchState,
  playerId: number,
  name: string,
  characterId: string,
  team: string | null,
  startPos: GridPos
): PlayerState {
  const character = getCharacter(characterId);
  const player: PlayerState = {
    playerId,
    name,
    characterId,
    team,
    hp: character.baseStats.hp,
    maxHp: character.baseStats.hp,
    pos: startPos,
    alive: true,
    consecutiveCorrect: 0,
    lastRewardType: null,
    pendingReward: null,
    pendingDot: null,
    totalCorrectAnswers: 0,
    maxStreak: 0,
    questionsAnswered: 0,
    currentQuestion: null,
    goalReached: false,
    finishRank: null,
    finishedAt: null,
    frozen: false,
    lastTargetedPlayerId: null,
  };
  state.players.set(playerId, player);
  return player;
}

export function startMatch(state: MatchState): void {
  state.status = "active";
}

/** Moves a player toward the goal row (grid.height - 1), clamped there, and
 * flags goalReached once they hit it. In 3+ player matches, records their finish rank. */
export function advancePlayer(player: PlayerState, state: MatchState, steps: number): void {
  const goalY = state.grid.height - 1;
  const nextY = Math.min(goalY, player.pos.y + steps);
  player.pos = { x: player.pos.x, y: nextY };
  if (nextY >= goalY && !player.goalReached) {
    player.goalReached = true;
    player.finishedAt = Date.now();
    if (!state.finishOrder.includes(player.playerId)) {
      state.finishOrder.push(player.playerId);
      player.finishRank = state.finishOrder.length;
    }
  }
}

export function pushQuestion(state: MatchState, playerId: number, questionId: number, correctIndex: number): void {
  const player = state.players.get(playerId);
  if (!player) return;
  player.currentQuestion = { questionId, correctIndex, questionNumber: player.questionsAnswered + 1 };
}

export interface SubmitAnswerResult {
  ok: boolean;
  error?: string;
  correct?: boolean;
  streakCount?: number;
  rewardOffered?: { rewardId: string; type: string } | null;
  newPos?: GridPos;
  goalReached?: boolean;
  dotDamage?: number;
  result?: MatchState["result"];
}

export function submitAnswer(
  state: MatchState,
  playerId: number,
  choiceIndex: number,
  rng: () => number = Math.random
): SubmitAnswerResult {
  const player = state.players.get(playerId);
  if (!player || !player.alive) return { ok: false, error: "not_in_match" };
  if (!player.currentQuestion) return { ok: false, error: "no_active_question" };

  const correct = choiceIndex === player.currentQuestion.correctIndex;
  player.currentQuestion = null;
  player.questionsAnswered += 1;

  player.consecutiveCorrect = correct ? player.consecutiveCorrect + 1 : 0;
  if (correct) {
    player.totalCorrectAnswers += 1;
    player.maxStreak = Math.max(player.maxStreak, player.consecutiveCorrect);
  }

  let rewardOffered: SubmitAnswerResult["rewardOffered"] = null;
  if (correct && player.consecutiveCorrect >= 2 && !player.pendingReward) {
    const reward = rollReward(player, player.questionsAnswered, rng);
    player.pendingReward = reward;
    rewardOffered = { rewardId: reward.rewardId, type: reward.type };
  }
  expireStaleRewards([player], player.questionsAnswered);

  // A correct answer is what drives you toward the goal — this replaces the
  // old movement-budget/direction system entirely. A frozen player still
  // answers/builds streak normally; only this one advance is consumed/skipped.
  if (correct) {
    if (player.frozen) {
      player.frozen = false;
    } else {
      advancePlayer(player, state, 1);
    }
  }

  const dotDamage = tickDot(player);

  const result = checkWinCondition(state);
  state.result = result;
  if (result) state.status = "completed";

  return {
    ok: true,
    correct,
    streakCount: player.consecutiveCorrect,
    rewardOffered,
    newPos: player.pos,
    goalReached: player.goalReached,
    dotDamage,
    result,
  };
}

/** A player's personal question timer expired with no answer submitted —
 * scored the same as a wrong answer (streak reset, no movement), but still
 * ticks their own DoT and advances their personal question count so the
 * match-length cap and reward-expiry clock keep moving even if they stall. */
export function timeoutAnswer(state: MatchState, playerId: number): SubmitAnswerResult {
  const player = state.players.get(playerId);
  if (!player || !player.alive || !player.currentQuestion) return { ok: false, error: "no_active_question" };

  player.currentQuestion = null;
  player.questionsAnswered += 1;
  player.consecutiveCorrect = 0;

  // A reward the player never acted on (missed/ignored the popup) must not linger —
  // submitAnswer's `!player.pendingReward` gate would otherwise silently block every
  // future streak reward until natural expiry, several questions later.
  if (player.pendingReward) {
    consumeReward(player, player.pendingReward.rewardId);
  }
  expireStaleRewards([player], player.questionsAnswered);

  const dotDamage = tickDot(player);

  const result = checkWinCondition(state);
  state.result = result;
  if (result) state.status = "completed";

  return {
    ok: true,
    correct: false,
    streakCount: 0,
    rewardOffered: null,
    newPos: player.pos,
    goalReached: player.goalReached,
    dotDamage,
    result,
  };
}

/** Every living player attacker could legally target (alive, not self, not a
 * teammate, and NOT already finished) other than excludeId — used both to enforce
 * "can't target the same player twice in a row" and to waive that rule when there's
 * genuinely no one else to pick, rather than soft-locking the reward entirely. */
function hasAlternativeTarget(state: MatchState, attacker: PlayerState, excludeId: number): boolean {
  for (const p of state.players.values()) {
    if (!p.alive || p.goalReached || p.playerId === attacker.playerId || p.playerId === excludeId) continue;
    if (state.mode === "teams" && attacker.team && attacker.team === p.team) continue;
    return true;
  }
  return false;
}

function validateTarget(state: MatchState, attacker: PlayerState, target: PlayerState | undefined): string | null {
  if (attacker.goalReached) return "attacker_already_finished";
  if (!target || !target.alive) return "invalid_target";
  if (target.goalReached) return "target_already_finished";
  if (attacker.playerId === target.playerId) return "cannot_target_self";
  if (state.mode === "teams" && attacker.team && attacker.team === target.team) return "friendly_fire_blocked";
  if (attacker.lastTargetedPlayerId === target.playerId && hasAlternativeTarget(state, attacker, target.playerId)) {
    return "repeat_target_blocked";
  }
  return null;
}

export interface UseAttackResult {
  ok: boolean;
  error?: string;
  outcome?: AttackOutcome;
  result?: MatchState["result"];
}

export function useAttack(state: MatchState, playerId: number, rewardId: string, targetId: number): UseAttackResult {
  const attacker = state.players.get(playerId);
  const target = state.players.get(targetId);
  if (!attacker || !attacker.alive) return { ok: false, error: "not_in_match" };
  const targetError = validateTarget(state, attacker, target);
  if (targetError) return { ok: false, error: targetError };
  if (!attacker.pendingReward || attacker.pendingReward.rewardId !== rewardId || attacker.pendingReward.type !== "attack_choice") {
    return { ok: false, error: "no_such_reward" };
  }

  const consumed = consumeReward(attacker, rewardId);
  if (!consumed.ok) return { ok: false, error: consumed.error };
  attacker.lastTargetedPlayerId = targetId;

  const ability = attackFor(attacker.characterId);
  const outcome = resolveAttack(attacker, target!, ability);

  const result = checkWinCondition(state);
  state.result = result;
  if (result) state.status = "completed";

  return { ok: true, outcome, result };
}

export interface UseFreezeResult {
  ok: boolean;
  error?: string;
}

/** Freezes the target: their next correct answer won't advance them. Doesn't
 * touch HP/position itself, so — unlike attack/bonus_move — it can never
 * itself produce a match result. */
export function useFreeze(state: MatchState, playerId: number, rewardId: string, targetId: number): UseFreezeResult {
  const attacker = state.players.get(playerId);
  const target = state.players.get(targetId);
  if (!attacker || !attacker.alive) return { ok: false, error: "not_in_match" };
  const targetError = validateTarget(state, attacker, target);
  if (targetError) return { ok: false, error: targetError };
  if (!attacker.pendingReward || attacker.pendingReward.rewardId !== rewardId || attacker.pendingReward.type !== "freeze") {
    return { ok: false, error: "no_such_reward" };
  }

  const consumed = consumeReward(attacker, rewardId);
  if (!consumed.ok) return { ok: false, error: consumed.error };
  attacker.lastTargetedPlayerId = targetId;

  target!.frozen = true;
  return { ok: true };
}

export interface ConsumeBonusMoveResult {
  ok: boolean;
  error?: string;
  newPos?: GridPos;
  goalReached?: boolean;
  result?: MatchState["result"];
}

/** Consuming a bonus_move reward advances the player extra steps up their
 * lane immediately — there's no separate directional "move" action anymore
 * since a lane only has one direction to go. */
export function consumeBonusMove(state: MatchState, playerId: number, rewardId: string): ConsumeBonusMoveResult {
  const player = state.players.get(playerId);
  if (!player || !player.alive) return { ok: false, error: "not_in_match" };
  if (!player.pendingReward || player.pendingReward.rewardId !== rewardId || player.pendingReward.type !== "bonus_move") {
    return { ok: false, error: "no_such_reward" };
  }
  const consumed = consumeReward(player, rewardId);
  if (!consumed.ok) return { ok: false, error: consumed.error };

  const character = getCharacter(player.characterId);
  const bonus = character.ability.type === "passive" ? character.ability.bonusMoveSteps ?? DEFAULT_BONUS_MOVE_STEPS : DEFAULT_BONUS_MOVE_STEPS;
  advancePlayer(player, state, bonus);

  const result = checkWinCondition(state);
  state.result = result;
  if (result) state.status = "completed";

  return { ok: true, newPos: player.pos, goalReached: player.goalReached, result };
}

export interface WaiveRewardResult {
  ok: boolean;
  error?: string;
}

/** Clears a pending reward with no other side effect — used when the client can't
 * act on it (e.g. an attack_choice/freeze offered with no living opponent left to
 * target). Without this, an un-consumable reward stays pending and blocks every
 * future reward roll (see submitAnswer's `!player.pendingReward` gate) until it
 * naturally expires several questions later. */
export function waiveReward(state: MatchState, playerId: number, rewardId: string): WaiveRewardResult {
  const player = state.players.get(playerId);
  if (!player) return { ok: false, error: "not_in_match" };
  if (!player.pendingReward || player.pendingReward.rewardId !== rewardId) {
    return { ok: false, error: "no_such_reward" };
  }
  return consumeReward(player, rewardId);
}

function livingGroups(state: MatchState): Map<string | number, PlayerState[]> {
  const groups = new Map<string | number, PlayerState[]>();
  for (const player of state.players.values()) {
    if (!player.alive) continue;
    const key = state.mode === "teams" && player.team ? player.team : player.playerId;
    const list = groups.get(key) ?? [];
    list.push(player);
    groups.set(key, list);
  }
  return groups;
}

export function checkWinCondition(state: MatchState): MatchState["result"] {
  const groups = livingGroups(state);

  // 1. All players eliminated (draw / complete wipeout)
  if (groups.size === 0) {
    return { winnerId: null, reason: "hp" };
  }

  // Active racers: living players who have NOT yet reached the goal
  const activeLivingPlayers = [...state.players.values()].filter((p) => p.alive && !p.goalReached);

  // Group active racers by team (or playerId in FFA)
  const activeGroups = new Map<string | number, PlayerState[]>();
  for (const p of activeLivingPlayers) {
    const key = state.mode === "teams" && p.team ? p.team : p.playerId;
    const list = activeGroups.get(key) ?? [];
    list.push(p);
    activeGroups.set(key, list);
  }

  // 2. End when there is only one player/team left actively racing
  if (state.players.size > 1) {
    if (activeGroups.size <= 1) {
      // If someone reached the goal, record any remaining active runner-up and finish
      if (state.finishOrder.length > 0) {
        for (const p of activeLivingPlayers) {
          if (!state.finishOrder.includes(p.playerId)) {
            state.finishOrder.push(p.playerId);
            p.finishRank = state.finishOrder.length;
          }
        }
        const winnerId = state.mode === "teams"
          ? (state.players.get(state.finishOrder[0])?.team ?? state.finishOrder[0])
          : state.finishOrder[0];
        return { winnerId, reason: "goal" };
      }

      // No one reached the goal, but only 1 living player/team remains (elimination by HP)
      if (groups.size === 1) {
        const [winnerId] = groups.keys();
        return { winnerId, reason: "hp" };
      }
    }
  } else {
    // Single player match: ends when the player reaches the goal or is eliminated
    if (activeGroups.size === 0) {
      if (state.finishOrder.length > 0) {
        return { winnerId: state.finishOrder[0], reason: "goal" };
      }
      return { winnerId: null, reason: "hp" };
    }
  }

  // Forced decision once any living player has been asked enough questions
  const anyoneAtCap = [...state.players.values()].some((p) => p.alive && p.questionsAnswered >= state.maxRounds);
  if (anyoneAtCap) {
    let bestKey: string | number | null = null;
    let bestProgress = -1;
    let bestHp = -1;
    for (const [key, players] of groups) {
      const progress = Math.max(...players.map((p) => p.pos.y));
      const hp = players.reduce((sum, p) => sum + p.hp, 0);
      if (progress > bestProgress || (progress === bestProgress && hp > bestHp)) {
        bestKey = key;
        bestProgress = progress;
        bestHp = hp;
      }
    }
    return { winnerId: bestKey, reason: "progress" };
  }

  return null;
}
