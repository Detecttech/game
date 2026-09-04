import test from "node:test";
import assert from "node:assert/strict";
import * as Engine from "./MatchEngine";
import {
  getOrCreateLiveMatch,
  joinLobby,
  selectCharacter,
  setReady,
  startMatchFlow,
  handleSubmitAnswer,
  handleUseAttack,
  handleUseFreeze,
  liveDashboard,
  type Emit,
} from "./LiveMatchRegistry";
import { createMatch } from "../db/repositories/matchRepo";
import { createTeacher } from "../db/repositories/teacherRepo";
import { createClassRoster } from "../db/repositories/classRepo";
import { createStudentProfile } from "../db/repositories/studentRepo";
import { createQuestionBank } from "../db/repositories/questionBankRepo";
import { createQuestion } from "../db/repositories/questionRepo";

test("teacher live match spectator flow receives all movement and combat events", async () => {
  // 1. Setup mock DB entities
  const teacher = createTeacher(`spec_t_${Date.now()}`, "pw123", "Spectator Teacher");
  const roster = createClassRoster(teacher.id, "Spectator Class", `SC_${Date.now()}`);
  const s1 = createStudentProfile(roster.id, "Alice", "pin1");
  const s2 = createStudentProfile(roster.id, "Bob", "pin2");
  const s3 = createStudentProfile(roster.id, "Charlie", "pin3");

  const bank = createQuestionBank(teacher.id, "Spectator Bank");
  for (let i = 1; i <= 8; i++) {
    createQuestion(bank.id, {
      text: `Test Question ${i}`,
      choices: ["Answer A", "Answer B", "Answer C", "Answer D"],
      correctIndex: 0,
    });
  }

  const joinCode = `J${Math.floor(100000 + Math.random() * 900000)}`;
  const match = createMatch(roster.id, bank.id, "ffa", joinCode, 30);
  const live = getOrCreateLiveMatch(match.id);

  // 2. Add players to lobby
  joinLobby(live, s1.id, 101, "Alice");
  selectCharacter(live, s1.id, "blaze");
  setReady(live, s1.id, true);

  joinLobby(live, s2.id, 102, "Bob");
  selectCharacter(live, s2.id, "aegis");
  setReady(live, s2.id, true);

  joinLobby(live, s3.id, 103, "Charlie");
  selectCharacter(live, s3.id, "zephyr");
  setReady(live, s3.id, true);

  // 3. Track events emitted to spectator
  const emittedEvents: Array<{ type: string; payload: any; target: string | number }> = [];
  const emit: Emit = (event, target) => {
    emittedEvents.push({ type: event.type, payload: event.payload, target });
  };

  // 4. Start match
  const startResult = startMatchFlow(live, emit);
  assert.equal(startResult.ok, true);

  // Verify match_start broadcast
  const matchStart = emittedEvents.find((e) => e.type === "match_start");
  assert.ok(matchStart, "match_start must be emitted");
  assert.equal(matchStart.target, "broadcast");
  assert.equal(matchStart.payload.players.length, 3);
  assert.ok(matchStart.payload.arenaLayout.grid.height > 0);

  // Verify liveDashboard contains grid and rich player states
  const dash = liveDashboard(live);
  assert.equal(dash.matchId, match.id);
  assert.equal(dash.status, "active");
  assert.ok(dash.grid.height > 0);
  assert.equal(dash.players.length, 3);
  assert.ok(dash.players[0].characterId !== null);
  assert.ok(dash.players[0].maxHp > 0);

  // 5. Test player movement (correct answer advances racer)
  emittedEvents.length = 0;
  const ansResult = handleSubmitAnswer(live, s1.id, 0, emit);
  assert.equal(ansResult?.ok, true);
  assert.equal(ansResult?.correct, true);

  const advancedEv = emittedEvents.find((e) => e.type === "player_advanced");
  assert.ok(advancedEv, "player_advanced must be broadcast on correct answer");
  assert.equal(advancedEv.target, "broadcast");
  assert.equal(advancedEv.payload.playerId, s1.id);
  assert.equal(advancedEv.payload.newGridPos.y, 1); // moved from 0 to 1!

  // 6. Test attack broadcast with attacker and target metadata
  emittedEvents.length = 0;
  live.state.players.get(s1.id)!.pendingReward = { rewardId: "attack_1", type: "attack_choice", expiresAtQuestion: 10 };
  const atkResult = handleUseAttack(live, s1.id, "attack_1", s2.id, emit);
  assert.equal(atkResult.ok, true);

  const atkEv = emittedEvents.find((e) => e.type === "attack_result");
  assert.ok(atkEv, "attack_result must be broadcast");
  assert.equal(atkEv.target, "broadcast");
  assert.equal(atkEv.payload.attackerId, s1.id);
  assert.equal(atkEv.payload.targetId, s2.id);
  assert.equal(atkEv.payload.attackerName, "Alice");
  assert.equal(atkEv.payload.targetName, "Bob");
  assert.equal(atkEv.payload.attackerCharacterId, "blaze");
  assert.equal(atkEv.payload.targetCharacterId, "aegis");
  assert.ok(atkEv.payload.damage > 0);
  assert.ok(atkEv.payload.targetHpAfter < 55);

  // 7. Test freeze broadcast with metadata
  emittedEvents.length = 0;
  live.state.players.get(s3.id)!.pendingReward = { rewardId: "freeze_1", type: "freeze", expiresAtQuestion: 10 };
  const frzResult = handleUseFreeze(live, s3.id, "freeze_1", s1.id, emit);
  assert.equal(frzResult.ok, true);

  const frzEv = emittedEvents.find((e) => e.type === "freeze_result");
  assert.ok(frzEv, "freeze_result must be broadcast");
  assert.equal(frzEv.target, "broadcast");
  assert.equal(frzEv.payload.casterId, s3.id);
  assert.equal(frzEv.payload.targetId, s1.id);
  assert.equal(frzEv.payload.casterName, "Charlie");
  assert.equal(frzEv.payload.targetName, "Alice");

  // Cleanup timers so test runner exits immediately
  for (const timer of live.playerTimers.values()) clearTimeout(timer);
  live.playerTimers.clear();
});
