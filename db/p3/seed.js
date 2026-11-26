/* =====================================================================
   Проєкт №3 · cinema_reviews · Тестові дані
   Запуск (після collections.js):
     mongosh "mongodb://localhost:27017" db/p3/seed.js
   ІДЕМПОТЕНТНО: у кожного документа фіксований _id, вставка через
   updateOne({_id}, {$setOnInsert: doc}, {upsert: true}) — повторний запуск
   не створює дублікатів і не перезаписує змінені дані.
   movieId / customerId / movieTitle / displayName узгоджені з
   db/p2/seed.sql (Каталог) і db/p1/seed.sql (Бронювання).
   ===================================================================== */

const rdb = db.getSiblingDB("cinema_reviews");
const L = (n) => NumberLong(String(n));
const I = (n) => NumberInt(n);
const D = (s) => ISODate(s);

function upsertAll(coll, docs) {
  let inserted = 0;
  for (const doc of docs) {
    const r = rdb[coll].updateOne({ _id: doc._id }, { $setOnInsert: doc }, { upsert: true });
    inserted += r.upsertedCount;
  }
  print(`${coll}: inserted ${inserted}, total ${rdb[coll].countDocuments()}`);
}

/* ---------- discussions (гібрид: останні повідомлення вбудовані) ---------- */
const DISC_DOVBUSH = ObjectId("6750a0000000000000000001");
const DISC_DUNE    = ObjectId("6750a0000000000000000002");
const MSG = (n) => ObjectId("6750b00000000000000000" + String(n).padStart(2, "0"));

upsertAll("discussions", [
  {
    _id: DISC_DOVBUSH,
    movieId: L(1), movieTitle: "Довбуш",
    title: "Наскільки фільм відповідає історії?",
    createdBy: { customerId: L(1), displayName: "Олена Коваль" },
    status: "open",
    tags: ["історія", "карпати"],
    recentMessages: [                       // копії 3 останніх з discussionMessages
      { messageId: MSG(2), authorName: "Андрій Мельник", text: "Костюми зроблені дуже ретельно.", createdAt: D("2026-11-20T10:05:00Z") },
      { messageId: MSG(3), authorName: "Ірина Бондар",   text: "А мені бракувало більше про побут.", createdAt: D("2026-11-20T11:30:00Z") },
      { messageId: MSG(4), authorName: "Олена Коваль",   text: "Погоджуюсь, але для кіно — норм.", createdAt: D("2026-11-21T08:00:00Z") },
    ],
    messagesCount: I(4),
    lastMessageAt: D("2026-11-21T08:00:00Z"),
    createdAt: D("2026-11-20T09:00:00Z"),
  },
  {
    _id: DISC_DUNE,
    movieId: L(3), movieTitle: "Дюна: Частина друга",
    title: "IMAX чи звичайний зал?",
    createdBy: { customerId: L(2), displayName: "Андрій Мельник" },
    status: "open",
    tags: ["imax", "звук"],
    recentMessages: [
      { messageId: MSG(5), authorName: "Андрій Мельник", text: "Хто дивився в IMAX — є різниця?", createdAt: D("2026-11-22T19:00:00Z") },
    ],
    messagesCount: I(1),
    lastMessageAt: D("2026-11-22T19:00:00Z"),
    createdAt: D("2026-11-22T19:00:00Z"),
  },
]);

/* ---------- discussionMessages (referencing на discussions) ---------- */
upsertAll("discussionMessages", [
  { _id: MSG(1), discussionId: DISC_DOVBUSH, replyTo: null,
    author: { customerId: L(1), displayName: "Олена Коваль" },
    text: "Хтось читав історичні джерела про Довбуша?", reactions: { like: I(3) },
    createdAt: D("2026-11-20T09:00:00Z") },
  { _id: MSG(2), discussionId: DISC_DOVBUSH, replyTo: MSG(1),
    author: { customerId: L(2), displayName: "Андрій Мельник" },
    text: "Костюми зроблені дуже ретельно.", reactions: { like: I(5), love: I(1) },
    createdAt: D("2026-11-20T10:05:00Z") },
  { _id: MSG(3), discussionId: DISC_DOVBUSH, replyTo: MSG(1),
    author: { customerId: L(3), displayName: "Ірина Бондар" },
    text: "А мені бракувало більше про побут.",
    createdAt: D("2026-11-20T11:30:00Z") },
  { _id: MSG(4), discussionId: DISC_DOVBUSH, replyTo: MSG(3),
    author: { customerId: L(1), displayName: "Олена Коваль" },
    text: "Погоджуюсь, але для кіно — норм.",
    createdAt: D("2026-11-21T08:00:00Z"), editedAt: D("2026-11-21T08:03:00Z") },
  { _id: MSG(5), discussionId: DISC_DUNE, replyTo: null,
    author: { customerId: L(2), displayName: "Андрій Мельник" },
    text: "Хто дивився в IMAX — є різниця?",
    createdAt: D("2026-11-22T19:00:00Z") },
]);

/* ---------- reviews: документи РІЗНОЇ структури в одній колекції ---------- */
const REV = (n) => ObjectId("6750c00000000000000000" + String(n).padStart(2, "0"));

upsertAll("reviews", [
  // 1) повний відгук глядача: фото, вбудовані коментарі, посилання на обговорення
  { _id: REV(1), type: "viewer",
    movieId: L(1), movieTitle: "Довбуш",
    author: { customerId: L(1), displayName: "Олена Коваль" },
    rating: I(5), text: "Неймовірні краєвиди Карпат і сильна акторська гра.",
    verifiedViewer: true,
    watchedFormat: "IMAX",
    photos: ["dovbush-hall.jpg", "dovbush-ticket.jpg"],
    comments: [
      { authorName: "Андрій Мельник", text: "Згоден, музика теж топ.", createdAt: D("2026-11-19T12:00:00Z") },
      { authorName: "Ірина Бондар",   text: "Треба йти!",              createdAt: D("2026-11-19T13:10:00Z") },
    ],
    discussionId: DISC_DOVBUSH,
    isDeleted: false, version: I(1),
    createdAt: D("2026-11-19T10:00:00Z") },

  // 2) короткий відгук: лише оцінка, без тексту й коментарів
  { _id: REV(2), type: "quick",
    movieId: L(1), movieTitle: "Довбуш",
    author: { customerId: L(3), displayName: "Ірина Бондар" },
    rating: I(4),
    isDeleted: false, version: I(1),
    createdAt: D("2026-11-20T08:00:00Z") },

  // 3) відгук критика: вкладений об'єкт criticInfo, свої поля (pros/cons)
  { _id: REV(3), type: "critic",
    movieId: L(3), movieTitle: "Дюна: Частина друга",
    author: { customerId: L(2), displayName: "Андрій Мельник" },
    rating: I(5), text: "Масштабне продовження з бездоганним звуком.",
    criticInfo: { outlet: "Кіноблог КН", score100: I(92) },
    pros: ["візуал", "саундтрек"], cons: ["тривалість"],
    containsSpoilers: false,
    comments: [],
    isDeleted: false, version: I(1),
    createdAt: D("2026-11-22T21:00:00Z") },

  // 4) відгук глядача зі спойлерами, без фото
  { _id: REV(4), type: "viewer",
    movieId: L(2), movieTitle: "Мавка. Лісова пісня",
    author: { customerId: L(2), displayName: "Андрій Мельник" },
    rating: I(4), text: "Гарна анімація, фінал трохи передбачуваний.",
    verifiedViewer: false,
    containsSpoilers: true,
    comments: [
      { authorName: "Олена Коваль", text: "Діти були в захваті.", createdAt: D("2026-11-23T09:00:00Z") },
    ],
    discussionId: null,
    isDeleted: false, version: I(2),
    createdAt: D("2026-11-23T08:00:00Z"), updatedAt: D("2026-11-23T08:30:00Z") },

  // 5) відгук глядача на «Дюну» з оцінкою 3
  { _id: REV(5), type: "viewer",
    movieId: L(3), movieTitle: "Дюна: Частина друга",
    author: { customerId: L(1), displayName: "Олена Коваль" },
    rating: I(3), text: "Красиво, але задовго.",
    verifiedViewer: true,
    isDeleted: false, version: I(1),
    createdAt: D("2026-11-24T20:00:00Z") },
]);

/* ---------- movieRatings: перераховується агрегацією і $merge ----------
   Ідемпотентно за своєю природою: $merge оновлює документ з тим самим _id. */
rdb.reviews.aggregate([
  { $match: { isDeleted: false } },
  { $group: {
      _id: "$movieId",
      movieTitle: { $first: "$movieTitle" },
      average: { $avg: "$rating" },
      count: { $sum: 1 },
      r1: { $sum: { $cond: [{ $eq: ["$rating", 1] }, 1, 0] } },
      r2: { $sum: { $cond: [{ $eq: ["$rating", 2] }, 1, 0] } },
      r3: { $sum: { $cond: [{ $eq: ["$rating", 3] }, 1, 0] } },
      r4: { $sum: { $cond: [{ $eq: ["$rating", 4] }, 1, 0] } },
      r5: { $sum: { $cond: [{ $eq: ["$rating", 5] }, 1, 0] } } } },
  { $project: {
      movieTitle: 1,
      average: { $round: ["$average", 2] },
      count: { $toInt: "$count" },
      distribution: { "1": "$r1", "2": "$r2", "3": "$r3", "4": "$r4", "5": "$r5" },
      updatedAt: "$$NOW" } },
  { $merge: { into: "movieRatings", on: "_id", whenMatched: "replace", whenNotMatched: "insert" } },
]);
print(`movieRatings: total ${rdb.movieRatings.countDocuments()}`);

print("seed.js: OK");
