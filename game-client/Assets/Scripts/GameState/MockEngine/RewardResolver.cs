using System;
using System.Collections.Generic;

namespace QuizBattle.GameState.MockEngine
{
    /// Mirrors server/src/matchEngine/RewardResolver.ts — keep the two in sync.
    public static class RewardResolver
    {
        // Weights: 60% attack, 32% freeze, 8% bonus_move (bonus jump made rare)
        private const double AttackChoiceWeight = 0.60;
        private const double FreezeWeight = 0.32;
        private static int _nextRewardSeq = 1;

        /// Anti-repeat rule: if the roll would grant another attack_choice immediately
        /// after the player's last *consumed* reward was also an attack, it alternates
        /// to freeze instead — keeping attacks from spamming while keeping bonus jumps rare.
        public static PendingReward RollReward(PlayerState player, int currentQuestionCount, Func<double> rng = null)
        {
            rng ??= () => UnityEngine.Random.value;
            double roll = rng();
            var type = roll < AttackChoiceWeight ? RewardType.AttackChoice
                : roll < AttackChoiceWeight + FreezeWeight ? RewardType.Freeze
                : RewardType.BonusMove;
            if (type == RewardType.AttackChoice && player.lastRewardType == RewardType.AttackChoice)
            {
                type = RewardType.Freeze;
            }
            else if (type == RewardType.Freeze && player.lastRewardType == RewardType.Freeze)
            {
                type = RewardType.AttackChoice;
            }
            return new PendingReward
            {
                rewardId = $"reward-{_nextRewardSeq++}",
                type = type,
                expiresAtQuestion = currentQuestionCount + MatchState.RewardExpiryQuestions,
            };
        }

        /// Each player's reward expiry is evaluated against their own question count,
        /// not a shared round — call this for a single player right after their
        /// questionsAnswered count changes.
        public static void ExpireStaleRewards(IEnumerable<PlayerState> players, int currentQuestionCount)
        {
            foreach (var player in players)
            {
                if (player.pendingReward != null && player.pendingReward.expiresAtQuestion < currentQuestionCount)
                {
                    player.pendingReward = null;
                }
            }
        }

        public struct ConsumeResult
        {
            public bool ok;
            public string error;
        }

        public static ConsumeResult ConsumeReward(PlayerState player, string rewardId)
        {
            if (player.pendingReward == null || player.pendingReward.rewardId != rewardId)
            {
                return new ConsumeResult { ok = false, error = "no_such_reward" };
            }
            player.lastRewardType = player.pendingReward.type;
            player.pendingReward = null;
            return new ConsumeResult { ok = true };
        }
    }
}
