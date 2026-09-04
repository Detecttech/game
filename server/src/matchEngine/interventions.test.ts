import test from "node:test";
import assert from "node:assert/strict";
import {
  getOrCreateLiveMatch,
  joinLobby,
  selectCharacter,
  setReady,
  startMatchFlow,
  handleSubmitAnswer,
  handleUseAttack,
  handleTeacherTriggerHazard,
  handleTeacherTriggerSuddenQuestion,
  type Emit,
} from "./LiveMatchRegistry";
import { createMatch } from "../db/repositories/matchRepo";
import { createTeacher } from "../db/repositories/teacherRepo";
import { createClassRoster } from "../db/repositories/classRepo";
import { createStudentProfile } from "../db/repositories/studentRepo";
import { createQuestionBank } from "../db/repositories/questionBankRepo";
import { createQuestion } from "../db/repositories/questionRepo";

test("teacher live match interventions: hazard fireball rain and high-hp sudden questions", async () => {
  // 1. Setup mock DB entities
  const teacher = createTeacher(`int_t_${Date.now()}`, "pw123", "Intervention Teacher");
  const roster = createClassRoster(teacher.id, "Intervention Class", `IC_${Date.now()}`);
  const s1 = createStudentProfile(roster.id, "Alice", "pin1");
  const s2 = createStudentProfile(roster.id, "Bob", "pin2");
  const s3 = createStudentProfile(roster.id, "Charlie", "pin3");

  const bank = createQuestionBank(teacher.id, "Intervention Bank");
  for (let i = 1; i <= 6; i++) {
    createQuestion(bank.id, {
      text: `Question ${i}`,
      choices: ["Choice 0", "Choice 1", "Choice 2", "Choice 3"],
      correctIndex: 0,
    });
  }

  const joinCode = `J${Math.floor(100000 + Math.random() * 900000)}`;
  const match = createMatch(roster.id, bank.id, "ffa", joinCode, 30);
  const live = getOrCreateLiveMatch(match.id);

  // 2. Add players to lobby
  joinLobby(live, s1.id, 201, "Alice");
  selectCharacter(live, s1.id, "blaze");
  setReady(live, s1.id, true);

  joinLobby(live, s2.id, 202, "Bob");
  selectCharacter(live, s2.id, "aegis"); // Aegis has 25% damage reduction!
  setReady(live, s2.id, true);

  joinLobby(live, s3.id, 203, "Charlie");
  selectCharacter(live, s3.id, "zephyr");
  setReady(live, s3.id, true);

  // 3. Track events emitted
  const emittedEvents: Array<{ type: string; payload: any; target: string | number }> = [];
  const emit: Emit = (event, target) => {
    emittedEvents.push({ type: event.type, payload: event.payload, target });
  };

  const startRes = startMatchFlow(live, emit);
  assert.equal(startRes.ok, true);

  const initialAliceHp = live.state.players.get(s1.id)!.hp; // Blaze: 45
  const initialBobHp = live.state.players.get(s2.id)!.hp;   // Aegis: 55

  // 4. Test Teacher Fireball Rain Hazard (Low effect: 8 damage)
  emittedEvents.length = 0;
  const hazardRes = handleTeacherTriggerHazard(live, "fireball_rain", 8, emit);
  assert.equal(hazardRes.ok, true);

  // Verify arena_hazard event broadcast
  const hazardEvent = emittedEvents.find((e) => e.type === "arena_hazard");
  assert.ok(hazardEvent, "arena_hazard must be broadcast");
  assert.equal(hazardEvent.target, "broadcast");
  assert.equal(hazardEvent.payload.hazardType, "fireball_rain");
  assert.equal(hazardEvent.payload.damage, 8);
  assert.equal(hazardEvent.payload.targets.length, 3);

  // Alice took 8 damage
  assert.equal(live.state.players.get(s1.id)!.hp, initialAliceHp - 8);
  // Bob (Aegis) took 8 * 0.75 = 6 damage due to Bulwark
  assert.equal(live.state.players.get(s2.id)!.hp, initialBobHp - 6);

  // 5. Test Teacher Sudden Question Event with High-HP Attack Reward (35 DMG)
  emittedEvents.length = 0;
  const suddenRes = handleTeacherTriggerSuddenQuestion(
    live,
    {
      text: "What is 2 + 2?",
      choices: ["4", "3", "5", "22"],
      correctIndex: 0,
      rewardType: "mega_attack",
      rewardDamage: 35,
      rewardName: "Mega Strike",
    },
    emit
  );
  assert.equal(suddenRes.ok, true);

  // Verify sudden_question_started broadcast
  const suddenStartEv = emittedEvents.find((e) => e.type === "sudden_question_started");
  assert.ok(suddenStartEv, "sudden_question_started must be broadcast");
  assert.equal(suddenStartEv.payload.rewardType, "mega_attack");
  assert.equal(suddenStartEv.payload.rewardDamage, 35);

  // Verify question_push sent to Alice with isSudden: true and high-stakes metadata
  const aliceQ = emittedEvents.find((e) => e.type === "question_push" && e.target === s1.id);
  assert.ok(aliceQ, "Alice should receive sudden question");
  assert.equal(aliceQ.payload.isSudden, true);
  assert.equal(aliceQ.payload.suddenRewardType, "mega_attack");
  assert.equal(aliceQ.payload.rewardDamage, 35);

  // 6. Alice answers the sudden question correctly -> receives high-HP attack reward!
  emittedEvents.length = 0;
  const ansRes = handleSubmitAnswer(live, s1.id, 0, emit);
  assert.equal(ansRes.ok, true);
  assert.equal(ansRes.correct, true);
  assert.ok(ansRes.rewardOffered, "Alice should be offered sudden reward");
  assert.equal(ansRes.rewardOffered?.type, "mega_attack");
  assert.equal(ansRes.rewardOffered?.damage, 35);

  // Alice's pending reward has customDamage = 35
  const alicePending = live.state.players.get(s1.id)!.pendingReward;
  assert.ok(alicePending);
  assert.equal(alicePending.type, "mega_attack");
  assert.equal(alicePending.customDamage, 35);

  // 7. Alice unleashes the 35 DMG Mega Attack on Charlie (Zephyr)
  const charlieHpBefore = live.state.players.get(s3.id)!.hp;
  emittedEvents.length = 0;
  const attackRes = handleUseAttack(live, s1.id, alicePending.rewardId, s3.id, emit);
  assert.equal(attackRes.ok, true);

  const atkEv = emittedEvents.find((e) => e.type === "attack_result");
  assert.ok(atkEv, "attack_result must be broadcast");
  assert.equal(atkEv.payload.damage, 35, "Damage must be 35 from Mega Attack");
  assert.equal(live.state.players.get(s3.id)!.hp, charlieHpBefore - 35);

  // Cleanup timers
  for (const timer of live.playerTimers.values()) clearTimeout(timer);
  live.playerTimers.clear();
});
