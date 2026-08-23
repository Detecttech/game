import type Database from "better-sqlite3";
import { hashSecret } from "../auth/passwordHash";

export function seedDatabase(db: Database.Database) {
  const teacherCount = (db.prepare("SELECT COUNT(*) as count FROM teachers").get() as { count: number }).count;
  if (teacherCount > 0) {
    return; // Already seeded
  }

  console.log("[db] Seeding demo teachers, classes, and question banks...");

  const now = Date.now();
  const passwordHash = hashSecret("password123");
  const defaultPinHash = hashSecret("1234");

  // 1. Create Default Teacher
  const teacherStmt = db.prepare(
    "INSERT INTO teachers (username, password_hash, display_name, created_at) VALUES (?, ?, ?, ?)"
  );
  const teacherResult = teacherStmt.run("teacher", passwordHash, "Demo Teacher", now);
  const teacherId = teacherResult.lastInsertRowid as number;

  // 2. Create Sample Classes with Simple Codes
  const classStmt = db.prepare(
    "INSERT INTO class_rosters (teacher_id, name, class_code, created_at) VALUES (?, ?, ?, ?)"
  );

  const mathClass = classStmt.run(teacherId, "Math Champions 101", "MATH1", now);
  const sciClass = classStmt.run(teacherId, "Science Explorers", "SCI1", now);
  const demoClass = classStmt.run(teacherId, "General Knowledge Arena", "DEMO1", now);

  // 3. Create Sample Students
  const studentStmt = db.prepare(
    "INSERT INTO student_profiles (class_roster_id, name, pin_hash, xp_total, created_at) VALUES (?, ?, ?, ?, ?)"
  );

  const sampleNames = ["Alex", "Jordan", "Taylor", "Sam", "Morgan"];
  for (const classId of [mathClass.lastInsertRowid, sciClass.lastInsertRowid, demoClass.lastInsertRowid]) {
    for (const name of sampleNames) {
      studentStmt.run(classId, name, defaultPinHash, 100, now);
    }
  }

  // 4. Create Sample Question Banks
  const bankStmt = db.prepare(
    "INSERT INTO question_banks (teacher_id, name, created_at) VALUES (?, ?, ?)"
  );
  const questionStmt = db.prepare(
    "INSERT INTO questions (question_bank_id, text, choice_0, choice_1, choice_2, choice_3, correct_index, created_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?)"
  );

  // --- Bank 1: Elementary Math & Logic (10 Questions) ---
  const mathBank = bankStmt.run(teacherId, "Elementary Math & Logic", now);
  const mathBankId = mathBank.lastInsertRowid as number;

  const mathQuestions = [
    { text: "What is 15 + 27?", choices: ["38", "42", "44", "45"], correct: 1 },
    { text: "What is 8 x 7?", choices: ["54", "56", "58", "64"], correct: 1 },
    { text: "What is 100 divided by 4?", choices: ["20", "24", "25", "30"], correct: 2 },
    { text: "What is the next number: 2, 4, 8, 16, ...?", choices: ["24", "30", "32", "36"], correct: 2 },
    { text: "How many sides does a hexagon have?", choices: ["5", "6", "7", "8"], correct: 1 },
    { text: "What is 9 x 9?", choices: ["72", "81", "89", "90"], correct: 1 },
    { text: "If a square has perimeter 20, what is each side's length?", choices: ["4", "5", "6", "10"], correct: 1 },
    { text: "What is 45 - 19?", choices: ["24", "26", "27", "28"], correct: 1 },
    { text: "Which of these is a prime number?", choices: ["9", "15", "17", "21"], correct: 2 },
    { text: "What is 12 x 11?", choices: ["121", "131", "132", "144"], correct: 2 },
  ];

  for (const q of mathQuestions) {
    questionStmt.run(mathBankId, q.text, q.choices[0], q.choices[1], q.choices[2], q.choices[3], q.correct, now);
  }

  // --- Bank 2: Science & Nature (10 Questions) ---
  const sciBank = bankStmt.run(teacherId, "Science & Nature", now);
  const sciBankId = sciBank.lastInsertRowid as number;

  const sciQuestions = [
    { text: "What planet is known as the Red Planet?", choices: ["Venus", "Mars", "Jupiter", "Saturn"], correct: 1 },
    { text: "What gas do plants absorb during photosynthesis?", choices: ["Oxygen", "Carbon Dioxide", "Nitrogen", "Helium"], correct: 1 },
    { text: "What is the chemical formula for water?", choices: ["CO2", "NaCl", "H2O", "O2"], correct: 2 },
    { text: "How many bones are in the adult human body?", choices: ["186", "206", "216", "256"], correct: 1 },
    { text: "What is the hardest natural substance on Earth?", choices: ["Gold", "Iron", "Quartz", "Diamond"], correct: 3 },
    { text: "Which animal is the largest mammal on Earth?", choices: ["African Elephant", "Blue Whale", "Giraffe", "Colossal Squid"], correct: 1 },
    { text: "What is the center of an atom called?", choices: ["Electron", "Nucleus", "Proton", "Quark"], correct: 1 },
    { text: "Which organ in the human body pumps blood?", choices: ["Brain", "Lungs", "Heart", "Liver"], correct: 2 },
    { text: "What force pulls objects toward the center of the Earth?", choices: ["Magnetism", "Friction", "Gravity", "Inertia"], correct: 2 },
    { text: "What is the closest star to Earth?", choices: ["Proxima Centauri", "Polaris", "The Sun", "Sirius"], correct: 2 },
  ];

  for (const q of sciQuestions) {
    questionStmt.run(sciBankId, q.text, q.choices[0], q.choices[1], q.choices[2], q.choices[3], q.correct, now);
  }

  // --- Bank 3: World & General Knowledge (10 Questions) ---
  const genBank = bankStmt.run(teacherId, "World Trivia & History", now);
  const genBankId = genBank.lastInsertRowid as number;

  const genQuestions = [
    { text: "Which ocean is the largest on Earth?", choices: ["Atlantic", "Indian", "Arctic", "Pacific"], correct: 3 },
    { text: "What is the capital of France?", choices: ["Rome", "Madrid", "Paris", "Berlin"], correct: 2 },
    { text: "How many continents are there on Earth?", choices: ["5", "6", "7", "8"], correct: 2 },
    { text: "What is the largest desert in the world?", choices: ["Sahara", "Gobi", "Antarctic", "Kalahari"], correct: 2 },
    { text: "Who painted the Mona Lisa?", choices: ["Vincent van Gogh", "Leonardo da Vinci", "Pablo Picasso", "Claude Monet"], correct: 1 },
    { text: "In which country are the Great Pyramids of Giza located?", choices: ["Greece", "Mexico", "Egypt", "Peru"], correct: 2 },
    { text: "Which instrument has 88 keys?", choices: ["Guitar", "Piano", "Flute", "Violin"], correct: 1 },
    { text: "What is the tallest mountain in the world above sea level?", choices: ["K2", "Mount Everest", "Mount Kilimanjaro", "Mount Fuji"], correct: 1 },
    { text: "How many colors are in a standard rainbow?", choices: ["5", "6", "7", "8"], correct: 2 },
    { text: "What is the longest river in the world?", choices: ["Amazon", "Nile", "Yangtze", "Mississippi"], correct: 1 },
  ];

  for (const q of genQuestions) {
    questionStmt.run(genBankId, q.text, q.choices[0], q.choices[1], q.choices[2], q.choices[3], q.correct, now);
  }

  console.log("[db] Successfully seeded demo teacher, 3 classes, and 30 questions.");
}
