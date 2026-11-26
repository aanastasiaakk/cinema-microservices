/* =====================================================================
   Проєкт №3 · cinema_reviews · Приклади запитів і aggregation pipeline
   Запуск: mongosh "mongodb://localhost:27017" db/p3/queries.js
   ===================================================================== */
const rdb = db.getSiblingDB("cinema_reviews");
const show = (title, data) => { print(`\n=== ${title}`); printjson(data); };

// 1. САМОПЕРЕВІРКА: документи з різними наборами полів в одній колекції
show("reviews: набори полів у кожному документі",
  rdb.reviews.aggregate([
    { $project: { type: 1, fields: { $map: { input: { $objectToArray: "$$ROOT" }, in: "$$this.k" } } } },
  ]).toArray());

// 2. EMBEDDING: відгук разом з автором і коментарями — один запит, без JOIN
show("відгук з вбудованими коментарями",
  rdb.reviews.findOne({ _id: ObjectId("6750c0000000000000000001") }, { author: 1, rating: 1, comments: 1 }));

// 3. REFERENCING: відгук -> його обговорення ($lookup за discussionId)
show("відгук + обговорення ($lookup)",
  rdb.reviews.aggregate([
    { $match: { discussionId: { $ne: null, $exists: true } } },
    { $lookup: { from: "discussions", localField: "discussionId", foreignField: "_id", as: "discussion" } },
    { $project: { movieTitle: 1, rating: 1, "discussion.title": 1, "discussion.messagesCount": 1 } },
  ]).toArray());

// 4. ГІБРИД: список тем читається з discussions (вбудовані recentMessages),
//    повна історія — пагінацією з discussionMessages
const disc = rdb.discussions.findOne({ movieId: NumberLong("1") });
show("тема з 3 останніми повідомленнями (embedded)", disc.recentMessages);
show("повна історія, сторінка 1 по 2 (referenced)",
  rdb.discussionMessages.find({ discussionId: disc._id })
    .sort({ createdAt: 1 }).skip(0).limit(2).toArray());

// 5. Денормалізоване поле: назва фільму без звернення до Каталогу
show("відгуки на 'Довбуш' (movieTitle денормалізовано)",
  rdb.reviews.find({ movieId: NumberLong("1"), isDeleted: false },
                   { movieTitle: 1, rating: 1, "author.displayName": 1 }).sort({ createdAt: -1 }).toArray());

// 6. Обробник події MovieRenamed з Каталогу (Лаб. 08) — оновлює всі копії назви
//    (тут — та сама назва, щоб не змінювати дані)
const res = rdb.reviews.updateMany({ movieId: NumberLong("1") }, { $set: { movieTitle: "Довбуш" } });
show("MovieRenamed -> updateMany", { matched: res.matchedCount });

// 7. Оптимістична конкурентність: оновлення лише якщо version не змінилась
const upd = rdb.reviews.updateOne(
  { _id: ObjectId("6750c0000000000000000004"), version: NumberInt(1) },   // застаріла версія
  { $set: { rating: NumberInt(5) }, $inc: { version: NumberInt(1) } });
show("update із застарілою version (очікуємо matched 0)", { matched: upd.matchedCount });

// 8. Aggregation: середній рейтинг і кількість відгуків за типом
show("рейтинг фільмів за типами відгуків",
  rdb.reviews.aggregate([
    { $match: { isDeleted: false } },
    { $group: { _id: { movie: "$movieTitle", type: "$type" },
                avg: { $avg: "$rating" }, n: { $sum: 1 } } },
    { $sort: { "_id.movie": 1, "_id.type": 1 } },
  ]).toArray());

// 9. Aggregation: найактивніші коментатори ($unwind вбудованого масиву)
show("топ коментаторів",
  rdb.reviews.aggregate([
    { $unwind: "$comments" },
    { $group: { _id: "$comments.authorName", comments: { $sum: 1 } } },
    { $sort: { comments: -1 } },
  ]).toArray());

// 10. Повнотекстовий пошук
show("пошук 'звук'", rdb.reviews.find({ $text: { $search: "звуком" } }, { text: 1 }).toArray());

// 11. Read-модель рейтингів (те, що публікується подією MovieRatingChanged у Каталог)
show("movieRatings", rdb.movieRatings.find().sort({ average: -1 }).toArray());

// 12. Перевірка використання індексу
show("explain: відгуки фільму",
  rdb.reviews.find({ movieId: NumberLong("1"), isDeleted: false }).sort({ createdAt: -1 })
    .explain("queryPlanner").queryPlanner.winningPlan);
