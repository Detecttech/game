export interface CharacterMeta {
  id: string;
  name: string;
  title: string;
  color: string;
  badgeBg: string;
  border: string;
  icon: string;
  abilityName: string;
  abilityType: string;
}

export const CHARACTER_CATALOG: Record<string, CharacterMeta> = {
  blaze: {
    id: "blaze",
    name: "Blaze",
    title: "Pyromancer",
    color: "#f97316",
    badgeBg: "rgba(249, 115, 22, 0.15)",
    border: "#ea580c",
    icon: "🔥",
    abilityName: "Fireball",
    abilityType: "Ranged Attack + Burn",
  },
  aegis: {
    id: "aegis",
    name: "Aegis",
    title: "Iron Knight",
    color: "#eab308",
    badgeBg: "rgba(234, 179, 8, 0.15)",
    border: "#ca8a04",
    icon: "🛡️",
    abilityName: "Bulwark",
    abilityType: "25% Armor Passive",
  },
  zephyr: {
    id: "zephyr",
    name: "Zephyr",
    title: "Wind Runner",
    color: "#06b6d4",
    badgeBg: "rgba(6, 182, 212, 0.15)",
    border: "#0891b2",
    icon: "⚡",
    abilityName: "Windstep",
    abilityType: "+1 Step Bonus Passive",
  },
  vera: {
    id: "vera",
    name: "Vera",
    title: "Shadow Drainer",
    color: "#a855f7",
    badgeBg: "rgba(168, 85, 247, 0.15)",
    border: "#9333ea",
    icon: "🔮",
    abilityName: "Life Drain",
    abilityType: "Melee Attack + 50% Heal",
  },
};

export function getCharacterMeta(characterId: string | null | undefined): CharacterMeta {
  if (!characterId) {
    return {
      id: "unknown",
      name: "Racer",
      title: "Challenger",
      color: "#94a3b8",
      badgeBg: "rgba(148, 163, 184, 0.15)",
      border: "#64748b",
      icon: "👤",
      abilityName: "Unknown",
      abilityType: "Standard",
    };
  }
  const key = characterId.toLowerCase();
  return (
    CHARACTER_CATALOG[key] ?? {
      id: key,
      name: characterId.charAt(0).toUpperCase() + characterId.slice(1),
      title: "Challenger",
      color: "#94a3b8",
      badgeBg: "rgba(148, 163, 184, 0.15)",
      border: "#64748b",
      icon: "👤",
      abilityName: "Standard",
      abilityType: "Standard",
    }
  );
}
