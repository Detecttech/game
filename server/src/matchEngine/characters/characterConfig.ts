import fs from "node:fs";
import path from "node:path";

export type AbilityType = "active" | "passive";

export interface AbilityConfig {
  id: string;
  name: string;
  type: AbilityType;
  description: string;
  range?: number;
  targeting?: "ranged" | "melee";
  baseDamage?: number;
  dotDamage?: number;
  dotRounds?: number;
  damageReductionPct?: number;
  bonusMoveSteps?: number;
  lifestealPct?: number;
  vfxTag: string;
}

export interface CharacterConfig {
  id: string;
  name: string;
  baseStats: { hp: number; moveRange: number };
  ability: AbilityConfig;
  unlock: { defaultUnlocked: boolean; xpThreshold?: number };
}

function loadCharacters(): CharacterConfig[] {
  const possiblePaths = [
    path.join(__dirname, "..", "..", "..", "..", "shared", "characters.json"),
    path.join(__dirname, "..", "..", "shared", "characters.json"),
    path.join(__dirname, "..", "shared", "characters.json"),
    path.join(process.cwd(), "..", "shared", "characters.json"),
    path.join(process.cwd(), "shared", "characters.json"),
    "/app/shared/characters.json",
  ];

  for (const p of possiblePaths) {
    if (fs.existsSync(p)) {
      try {
        const raw = fs.readFileSync(p, "utf-8");
        const parsed = JSON.parse(raw) as { characters: CharacterConfig[] };
        if (parsed?.characters) return parsed.characters;
      } catch {
        // continue
      }
    }
  }

  // Fallback defaults if characters.json file is missing in container
  return [
    {
      id: "aegis",
      name: "Aegis",
      baseStats: { hp: 120, moveRange: 1 },
      ability: { id: "shield", name: "Iron Wall", type: "passive", description: "Takes 25% less damage", damageReductionPct: 25, vfxTag: "shield" },
      unlock: { defaultUnlocked: true }
    },
    {
      id: "blaze",
      name: "Blaze",
      baseStats: { hp: 100, moveRange: 1 },
      ability: { id: "fireball", name: "Fireball", type: "active", description: "Deals 25 damage + 10 burn DoT", baseDamage: 25, dotDamage: 10, dotRounds: 2, vfxTag: "fireball" },
      unlock: { defaultUnlocked: true }
    },
    {
      id: "frost",
      name: "Frost",
      baseStats: { hp: 100, moveRange: 1 },
      ability: { id: "ice_shard", name: "Freeze", type: "active", description: "Skips target next turn", baseDamage: 15, vfxTag: "freeze" },
      unlock: { defaultUnlocked: true }
    },
    {
      id: "surge",
      name: "Surge",
      baseStats: { hp: 90, moveRange: 2 },
      ability: { id: "dash", name: "Quick Dash", type: "active", description: "Moves +1 extra step", bonusMoveSteps: 1, vfxTag: "dash" },
      unlock: { defaultUnlocked: true }
    },
    {
      id: "vamp",
      name: "Vamp",
      baseStats: { hp: 95, moveRange: 1 },
      ability: { id: "drain", name: "Life Drain", type: "active", description: "Steals 20 HP from target", baseDamage: 20, lifestealPct: 50, vfxTag: "drain" },
      unlock: { defaultUnlocked: false, xpThreshold: 100 }
    }
  ];
}

export const CHARACTERS: readonly CharacterConfig[] = loadCharacters();

const byId = new Map(CHARACTERS.map((c) => [c.id, c]));

export function getCharacter(id: string): CharacterConfig {
  const c = byId.get(id);
  if (!c) throw new Error(`Unknown character id: ${id}`);
  return c;
}

export function defaultUnlockedCharacterIds(): string[] {
  return CHARACTERS.filter((c) => c.unlock.defaultUnlocked).map((c) => c.id);
}
