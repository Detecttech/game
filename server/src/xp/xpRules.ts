import { CHARACTERS } from "../matchEngine/characters/characterConfig";

export const XP_PER_CORRECT_ANSWER = 7; // 65% of original 10
export const XP_PER_SUDDEN_BONUS_ANSWER = 10; // Original 10 XP for bonus sudden questions
export const XP_STREAK_MILESTONE_BONUS = 16; // 65% of original 25 (16.25)
export const STREAK_MILESTONES = [3, 5];
export const XP_MATCH_WIN_FFA = 65; // 65% of original 100
export const XP_MATCH_WIN_TEAM = 49; // 65% of original 75 (48.75)
export const XP_PARTICIPATION = 13; // 65% of original 20

export interface XpBreakdown {
  fromAnswers: number;
  fromMilestones: number;
  fromResult: number;
  total: number;
}

export function computeMatchXp(opts: {
  totalCorrectAnswers: number;
  suddenCorrectAnswers?: number;
  maxStreak: number;
  won: boolean;
  mode: "ffa" | "teams";
}): XpBreakdown {
  const fromRegular = opts.totalCorrectAnswers * XP_PER_CORRECT_ANSWER;
  const fromSudden = (opts.suddenCorrectAnswers ?? 0) * XP_PER_SUDDEN_BONUS_ANSWER;
  const fromAnswers = fromRegular + fromSudden;
  const fromMilestones = STREAK_MILESTONES.filter((t) => opts.maxStreak >= t).length * XP_STREAK_MILESTONE_BONUS;
  const fromResult = opts.won ? (opts.mode === "teams" ? XP_MATCH_WIN_TEAM : XP_MATCH_WIN_FFA) : XP_PARTICIPATION;
  return { fromAnswers, fromMilestones, fromResult, total: fromAnswers + fromMilestones + fromResult };
}

/** Characters newly unlocked by crossing an XP threshold, given the student's updated total. */
export function newlyUnlockedCharacters(newXpTotal: number, alreadyUnlocked: Set<string>): string[] {
  return CHARACTERS.filter(
    (c) => !c.unlock.defaultUnlocked && !alreadyUnlocked.has(c.id) && c.unlock.xpThreshold !== undefined && newXpTotal >= c.unlock.xpThreshold
  ).map((c) => c.id);
}
