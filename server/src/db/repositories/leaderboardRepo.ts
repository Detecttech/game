import { db } from "../client";

export interface LeaderboardEntry {
  student_profile_id: number;
  name: string;
  xp_total: number;
  class_roster_id: number;
}

export function leaderboardForClass(classRosterId: number, limit = 20): LeaderboardEntry[] {
  return db
    .prepare(
      `SELECT id AS student_profile_id, name, xp_total, class_roster_id
       FROM student_profiles WHERE class_roster_id = ? ORDER BY xp_total DESC LIMIT ?`
    )
    .all(classRosterId, limit) as LeaderboardEntry[];
}

export function leaderboardGlobal(limit = 20): LeaderboardEntry[] {
  return db
    .prepare(
      `SELECT id AS student_profile_id, name, xp_total, class_roster_id
       FROM student_profiles ORDER BY xp_total DESC LIMIT ?`
    )
    .all(limit) as LeaderboardEntry[];
}

export function exportLeaderboardCsv(classRosterId?: number): string {
  let rows: Array<{
    id: number;
    name: string;
    xp_total: number;
    className: string;
    classCode: string;
    createdAt: number;
  }>;

  if (classRosterId !== undefined && !isNaN(classRosterId)) {
    rows = db
      .prepare(
        `SELECT sp.id, sp.name, sp.xp_total, cr.name AS className, cr.class_code AS classCode, sp.created_at AS createdAt
         FROM student_profiles sp
         JOIN class_rosters cr ON sp.class_roster_id = cr.id
         WHERE sp.class_roster_id = ?
         ORDER BY sp.xp_total DESC, sp.name ASC`
      )
      .all(classRosterId) as any[];
  } else {
    rows = db
      .prepare(
        `SELECT sp.id, sp.name, sp.xp_total, cr.name AS className, cr.class_code AS classCode, sp.created_at AS createdAt
         FROM student_profiles sp
         LEFT JOIN class_rosters cr ON sp.class_roster_id = cr.id
         ORDER BY sp.xp_total DESC, sp.name ASC`
      )
      .all() as any[];
  }

  const lines = ["Rank,Student ID,Student Name,Class Name,Class Code,Total XP,Date Created"];
  rows.forEach((row, index) => {
    const rank = index + 1;
    const cleanName = `"${(row.name || "").replace(/"/g, '""')}"`;
    const cleanClass = `"${(row.className || "Default").replace(/"/g, '""')}"`;
    const cleanCode = `"${(row.classCode || "").replace(/"/g, '""')}"`;
    const dateStr = row.createdAt ? new Date(row.createdAt).toISOString().split("T")[0] : "";
    lines.push(`${rank},${row.id},${cleanName},${cleanClass},${cleanCode},${row.xp_total},${dateStr}`);
  });

  return lines.join("\n");
}
