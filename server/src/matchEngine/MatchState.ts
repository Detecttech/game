export interface GridPos {
  x: number;
  y: number;
}

export type RewardType = "attack_choice" | "freeze" | "bonus_move";

export interface PendingReward {
  rewardId: string;
  type: RewardType;
  expiresAtQuestion: number; // relative to the owning player's own questionsAnswered count
}

export interface ActiveQuestion {
  questionId: number;
  correctIndex: number;
  questionNumber: number; // this player's own Nth question, not a match-wide round
}

export interface PlayerState {
  playerId: number;
  name: string;
  characterId: string;
  team: string | null;
  hp: number;
  maxHp: number;
  pos: GridPos;
  alive: boolean;
  consecutiveCorrect: number;
  lastRewardType: RewardType | null;
  pendingReward: PendingReward | null;
  pendingDot: { damage: number; remainingRounds: number } | null;
  totalCorrectAnswers: number;
  maxStreak: number;
  questionsAnswered: number; // drives reward expiry + the match-length forced-decision cap
  currentQuestion: ActiveQuestion | null; // independent per player, not shared
  goalReached: boolean;
  finishRank: number | null; // 1 for 1st place, 2 for 2nd place, etc.
  finishedAt: number | null;
  frozen: boolean; // consumed on this player's next correct answer — that answer won't advance them
  lastTargetedPlayerId: number | null; // for the "can't attack/freeze the same player twice in a row" rule
}

export type WinReason = "hp" | "goal" | "progress" | "timeout";

export interface MatchResult {
  winnerId: number | string | null; // playerId (FFA) or team name (teams)
  reason: WinReason;
}

export interface MatchState {
  matchId: number;
  mode: "ffa" | "teams";
  status: "lobby" | "active" | "completed";
  maxRounds: number; // max questions any one player answers before a forced progress tiebreak
  timerSeconds: number; // grace timer when 1st place finishes in 3+ player games
  timerStartedAt: number | null;
  finishOrder: number[]; // playerIds in order of finish
  grid: { width: number; height: number };
  players: Map<number, PlayerState>;
  result: MatchResult | null;
}

export const DEFAULT_GRID = { width: 8, height: 6 };
export const REWARD_EXPIRY_QUESTIONS = 4;
export const DEFAULT_BONUS_MOVE_STEPS = 1;

// Steps-to-win is clamped to this range even for very small/large question banks — too
// few steps makes the race trivial (won on luck), too many makes it drag on forever.
const MIN_GOAL_STEPS = 4;
const MAX_GOAL_STEPS = 30;

/** Derives "steps to reach the goal row" from how many questions are in the match's
 * question bank, so a race is roughly as long as the class's own question set —
 * clamped to a sane range. Grid height is steps + 1 (players start at row 0). */
export function computeGridHeight(questionCount: number): number {
  const steps = Math.min(MAX_GOAL_STEPS, Math.max(MIN_GOAL_STEPS, questionCount));
  return steps + 1;
}

export function createMatchState(
  matchId: number,
  mode: "ffa" | "teams",
  maxRounds = 20,
  gridHeight: number = DEFAULT_GRID.height,
  timerSeconds = 30
): MatchState {
  return {
    matchId,
    mode,
    status: "lobby",
    maxRounds,
    timerSeconds,
    timerStartedAt: null,
    finishOrder: [],
    grid: { width: DEFAULT_GRID.width, height: gridHeight },
    players: new Map(),
    result: null,
  };
}
