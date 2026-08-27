import { Router } from "express";
import { requireTeacher, type AuthedRequest } from "../middleware/authTeacher";
import { listQuestionBanksByTeacher, createQuestionBank, type QuestionBank } from "../../db/repositories/questionBankRepo";
import { listQuestionsByBank, createQuestion } from "../../db/repositories/questionRepo";
import { listClassRostersByTeacher, createClassRoster, type ClassRoster } from "../../db/repositories/classRepo";
import { listStudentProfilesByClass, createStudentProfile, type StudentProfile } from "../../db/repositories/studentRepo";
import { hashSecret } from "../../auth/passwordHash";

export const backupRoutes = Router();
backupRoutes.use(requireTeacher);

function randomClassCode(): string {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let code = "";
  for (let i = 0; i < 6; i++) code += alphabet[Math.floor(Math.random() * alphabet.length)];
  return code;
}

// Export all question banks, questions, classes, and rosters for this teacher
backupRoutes.get("/backup/export", (req: AuthedRequest, res) => {
  const teacherId = req.teacherId!;

  const banks = listQuestionBanksByTeacher(teacherId);
  const fullBanks = banks.map((b: QuestionBank) => ({
    name: b.name,
    questions: listQuestionsByBank(b.id).map((q) => ({
      text: q.text,
      choice_0: q.choice_0,
      choice_1: q.choice_1,
      choice_2: q.choice_2,
      choice_3: q.choice_3,
      correct_index: q.correct_index,
    })),
  }));

  const rosters = listClassRostersByTeacher(teacherId);
  const fullClasses = rosters.map((c: ClassRoster) => ({
    name: c.name,
    class_code: c.class_code,
    students: listStudentProfilesByClass(c.id).map((s: StudentProfile) => ({
      name: s.name,
    })),
  }));

  res.json({
    version: 1,
    exportedAt: new Date().toISOString(),
    questionBanks: fullBanks,
    classes: fullClasses,
  });
});

// Import question banks, questions, and classes
backupRoutes.post("/backup/import", (req: AuthedRequest, res) => {
  const teacherId = req.teacherId!;
  const data = req.body ?? {};

  let importedBanks = 0;
  let importedQuestions = 0;
  let importedClasses = 0;
  let importedStudents = 0;

  // Import question banks
  if (Array.isArray(data.questionBanks)) {
    const existingBanks = listQuestionBanksByTeacher(teacherId);
    for (const b of data.questionBanks) {
      if (!b.name) continue;
      let targetBank = existingBanks.find((eb: QuestionBank) => eb.name === b.name);
      if (!targetBank) {
        targetBank = createQuestionBank(teacherId, b.name);
        importedBanks++;
      }

      if (Array.isArray(b.questions)) {
        const existingQs = listQuestionsByBank(targetBank.id);
        for (const q of b.questions) {
          if (!q.text) continue;
          const duplicate = existingQs.some((eq) => eq.text.trim().toLowerCase() === q.text.trim().toLowerCase());
          if (!duplicate) {
            createQuestion(targetBank.id, {
              text: q.text,
              choices: [q.choice_0 ?? "", q.choice_1 ?? "", q.choice_2 ?? "", q.choice_3 ?? ""],
              correctIndex: Number(q.correct_index ?? 0),
            });
            importedQuestions++;
          }
        }
      }
    }
  }

  // Import classes and rosters
  if (Array.isArray(data.classes)) {
    const existingClasses = listClassRostersByTeacher(teacherId);
    const defaultPinHash = hashSecret("1234");
    for (const c of data.classes) {
      if (!c.name) continue;
      let targetClass = existingClasses.find((ec: ClassRoster) => ec.name === c.name);
      if (!targetClass) {
        targetClass = createClassRoster(teacherId, c.name, c.class_code || randomClassCode());
        importedClasses++;
      }

      if (Array.isArray(c.students)) {
        const existingStudents = listStudentProfilesByClass(targetClass.id);
        for (const s of c.students) {
          if (!s.name) continue;
          const duplicate = existingStudents.some((es: StudentProfile) => es.name.trim().toLowerCase() === s.name.trim().toLowerCase());
          if (!duplicate) {
            createStudentProfile(targetClass.id, s.name.trim(), defaultPinHash);
            importedStudents++;
          }
        }
      }
    }
  }

  res.json({
    ok: true,
    importedBanks,
    importedQuestions,
    importedClasses,
    importedStudents,
  });
});
