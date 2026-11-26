/* =====================================================================
   Проєкт №3 · Контекст «Відгуки та обговорення» (Reviews) · MongoDB
   БД: cinema_reviews
   Запуск:  mongosh "mongodb://localhost:27017" db/p3/collections.js
   Скрипт ідемпотентний: якщо колекція вже є, оновлюється лише її
   валідатор (collMod); createIndex для наявного індексу нічого не робить.

   Колекції (4):
     reviews             — відгук = агрегат: вбудовані author, comments[] (EMBEDDING)
                           + discussionId -> discussions (REFERENCING)
     discussions         — тема обговорення: останні повідомлення вбудовані,
                           повна історія — окремою колекцією (ГІБРИДНИЙ підхід)
     discussionMessages  — усі повідомлення теми, discussionId -> discussions (REFERENCING)
     movieRatings        — зведений рейтинг фільму (read-модель, денормалізація)

   Посилання на інші контексти (без FK, лише Id):
     movieId              -> CinemaCatalog.dbo.Movies.Id      (Проєкт №2)
     author.customerId    -> CinemaBooking.dbo.Customers.Id   (Проєкт №1)
   ===================================================================== */

const dbName = "cinema_reviews";
const rdb = db.getSiblingDB(dbName);

function ensureCollection(name, validator) {
  const exists = rdb.getCollectionNames().includes(name);
  if (!exists) {
    rdb.createCollection(name, {
      validator,
      validationLevel: "moderate",   // старі документи не ламаються при зміні схеми
      validationAction: "error",
    });
    print(`created: ${name}`);
  } else {
    rdb.runCommand({ collMod: name, validator, validationLevel: "moderate" });
    print(`validator updated: ${name}`);
  }
}

/* ---------- reviews ----------
   Обов'язкові лише спільні поля; решта (photos, criticInfo, containsSpoilers,
   watchedFormat ...) є тільки в частини документів — ГНУЧКА СХЕМА. */
ensureCollection("reviews", {
  $jsonSchema: {
    bsonType: "object",
    required: ["movieId", "movieTitle", "author", "type", "rating", "createdAt", "isDeleted"],
    properties: {
      movieId:    { bsonType: "long", description: "Id фільму з Каталогу (без FK)" },
      movieTitle: { bsonType: "string", description: "копія Movies.Title, оновлюється подіями з Каталогу" },
      type:       { enum: ["viewer", "critic", "quick"] },
      rating:     { bsonType: "int", minimum: 1, maximum: 5 },
      text:       { bsonType: "string", maxLength: 5000 },
      author: {
        bsonType: "object",
        required: ["customerId", "displayName"],
        properties: {
          customerId:  { bsonType: "long", description: "Id клієнта з Бронювання (без FK)" },
          displayName: { bsonType: "string", description: "копія Customers.FullName, оновлюється подіями" },
        },
      },
      verifiedViewer: { bsonType: "bool", description: "знімок: клієнт мав CheckedIn-бронювання на момент відгуку" },
      photos:   { bsonType: "array", items: { bsonType: "string" } },
      comments: {
        bsonType: "array",
        maxItems: 50,               // обмежений вбудований масив; довгі розмови -> discussions
        items: {
          bsonType: "object",
          required: ["authorName", "text", "createdAt"],
          properties: {
            authorName: { bsonType: "string" },
            text:       { bsonType: "string", maxLength: 1000 },
            createdAt:  { bsonType: "date" },
          },
        },
      },
      discussionId: { bsonType: ["objectId", "null"] },
      criticInfo: {
        bsonType: "object",
        required: ["outlet"],
        properties: {
          outlet:   { bsonType: "string" },
          score100: { bsonType: "int", minimum: 0, maximum: 100 },
        },
      },
      isDeleted: { bsonType: "bool" },
      version:   { bsonType: "int", description: "оптимістична конкурентність (update ... {version: n})" },
      createdAt: { bsonType: "date" },
      updatedAt: { bsonType: "date" },
    },
  },
});

/* ---------- discussions ---------- */
ensureCollection("discussions", {
  $jsonSchema: {
    bsonType: "object",
    required: ["movieId", "movieTitle", "title", "createdBy", "status", "messagesCount", "createdAt"],
    properties: {
      movieId:    { bsonType: "long" },
      movieTitle: { bsonType: "string" },
      title:      { bsonType: "string", minLength: 3, maxLength: 200 },
      status:     { enum: ["open", "closed", "archived"] },
      createdBy: {
        bsonType: "object",
        required: ["customerId", "displayName"],
        properties: { customerId: { bsonType: "long" }, displayName: { bsonType: "string" } },
      },
      tags: { bsonType: "array", items: { bsonType: "string" } },
      recentMessages: {               // ГІБРИД: лише 3 останні повідомлення вбудовані
        bsonType: "array",
        maxItems: 3,
        items: {
          bsonType: "object",
          required: ["messageId", "authorName", "text", "createdAt"],
          properties: {
            messageId:  { bsonType: "objectId", description: "посилання на discussionMessages._id" },
            authorName: { bsonType: "string" },
            text:       { bsonType: "string" },
            createdAt:  { bsonType: "date" },
          },
        },
      },
      messagesCount: { bsonType: "int", minimum: 0 },
      lastMessageAt: { bsonType: ["date", "null"] },
      createdAt:     { bsonType: "date" },
    },
  },
});

/* ---------- discussionMessages ---------- */
ensureCollection("discussionMessages", {
  $jsonSchema: {
    bsonType: "object",
    required: ["discussionId", "author", "text", "createdAt"],
    properties: {
      discussionId: { bsonType: "objectId", description: "REFERENCE на discussions._id" },
      replyTo:      { bsonType: ["objectId", "null"], description: "REFERENCE на інше повідомлення" },
      author: {
        bsonType: "object",
        required: ["customerId", "displayName"],
        properties: { customerId: { bsonType: "long" }, displayName: { bsonType: "string" } },
      },
      text:      { bsonType: "string", minLength: 1, maxLength: 4000 },
      reactions: { bsonType: "object", description: "довільні ключі: like, love, laugh ..." },
      createdAt: { bsonType: "date" },
      editedAt:  { bsonType: "date" },
    },
  },
});

/* ---------- movieRatings (_id = movieId) ---------- */
ensureCollection("movieRatings", {
  $jsonSchema: {
    bsonType: "object",
    required: ["_id", "movieTitle", "average", "count", "distribution", "updatedAt"],
    properties: {
      _id:          { bsonType: "long", description: "movieId з Каталогу" },
      movieTitle:   { bsonType: "string" },
      average:      { bsonType: ["double", "decimal", "int"], minimum: 0, maximum: 5 },
      count:        { bsonType: "int", minimum: 0 },
      distribution: { bsonType: "object", description: "{ '1': n, ..., '5': n }" },
      updatedAt:    { bsonType: "date" },
    },
  },
});

/* =====================================================================
   ІНДЕКСИ — під конкретні запити
   ===================================================================== */
// Запит: відгуки фільму, новіші першими (сторінка фільму), без видалених
rdb.reviews.createIndex(
  { movieId: 1, createdAt: -1 },
  { name: "ix_reviews_movie_createdAt", partialFilterExpression: { isDeleted: false } });
// Запит: «мої відгуки» в профілі клієнта
rdb.reviews.createIndex({ "author.customerId": 1, createdAt: -1 }, { name: "ix_reviews_author" });
// Правило: один відгук глядача на фільм (критики й quick — без обмеження)
rdb.reviews.createIndex(
  { movieId: 1, "author.customerId": 1 },
  { name: "ux_reviews_movie_author_viewer", unique: true,
    partialFilterExpression: { type: "viewer" } });
// Запит: повнотекстовий пошук по тексту відгуків
rdb.reviews.createIndex({ text: "text" }, { name: "tx_reviews_text", default_language: "none" });
// Запит: оновлення денормалізованої назви за подією MovieRenamed (updateMany по movieId)
//  -> покривається ix_reviews_movie_createdAt (префікс movieId) лише для не видалених,
//     тому окремий індекс по movieId для всіх документів:
rdb.reviews.createIndex({ movieId: 1 }, { name: "ix_reviews_movie" });

// Запит: список тем фільму, найактивніші зверху
rdb.discussions.createIndex({ movieId: 1, lastMessageAt: -1 }, { name: "ix_discussions_movie_last" });
// Запит: теми за тегом
rdb.discussions.createIndex({ tags: 1 }, { name: "ix_discussions_tags" });   // multikey

// Запит: сторінка повідомлень теми (пагінація за часом)
rdb.discussionMessages.createIndex({ discussionId: 1, createdAt: 1 }, { name: "ix_messages_discussion_createdAt" });

// movieRatings: _id = movieId, окремий індекс не потрібен.
// Запит: топ фільмів за рейтингом
rdb.movieRatings.createIndex({ average: -1, count: -1 }, { name: "ix_movieRatings_top" });

print("collections.js: OK");
