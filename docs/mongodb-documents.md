# Проєкт №3 · cinema_reviews (MongoDB): структура колекцій і приклади документів

Скрипти: `db/p3/collections.js` (колекції, JSON Schema валідація, індекси), `db/p3/seed.js` (тестові дані), `db/p3/queries.js` (запити й aggregation pipeline).

## Колекції

| Колекція | Що зберігає | Прийом моделювання |
|---|---|---|
| `reviews` | відгук на фільм | **embedding**: `author`, `comments[]`, `criticInfo`; **referencing**: `discussionId` → `discussions`; **гнучка схема**: типи `viewer` / `quick` / `critic` мають різні поля; **денормалізація**: `movieTitle`, `author.displayName` |
| `discussions` | тема обговорення фільму | **гібрид**: 3 останні повідомлення вбудовані в `recentMessages[]`, повна історія лежить у `discussionMessages` |
| `discussionMessages` | усі повідомлення теми | **referencing**: `discussionId` → `discussions`, `replyTo` → інше повідомлення |
| `movieRatings` | зведений рейтинг фільму (`_id` = movieId) | read-модель, денормалізація для швидкого читання; перераховується aggregation pipeline з `$merge` |

### Коли embedding, а коли referencing

* **Embedding** використовуємо, коли дані тісно пов'язані з агрегатом, завжди потрібні разом з ним і їх обмежена кількість. Автор відгуку і кілька коментарів до відгуку (валідатор обмежує `comments` до 50 елементів) читаються одним запитом без `$lookup`.
* **Referencing** використовуємо, коли дані слабко пов'язані або необмежено ростуть. Обговорення може мати тисячі повідомлень, тому вони лежать в окремій колекції і читаються посторінково (індекс `discussionId + createdAt`).
* **Гібрид**: у `discussions.recentMessages` лежать копії 3 останніх повідомлень, щоб список тем показувався одним запитом. Повна історія береться з `discussionMessages`.

## Приклади документів

### `reviews`: повний відгук глядача (embedding + referencing + денормалізація)

```json
{
  "_id": { "$oid": "6750c0000000000000000001" },
  "type": "viewer",
  "movieId": { "$numberLong": "1" },
  "movieTitle": "Довбуш",
  "author": { "customerId": { "$numberLong": "1" }, "displayName": "Олена Коваль" },
  "rating": 5,
  "text": "Неймовірні краєвиди Карпат і сильна акторська гра.",
  "verifiedViewer": true,
  "watchedFormat": "IMAX",
  "photos": ["dovbush-hall.jpg", "dovbush-ticket.jpg"],
  "comments": [
    { "authorName": "Андрій Мельник", "text": "Згоден, музика теж топ.", "createdAt": { "$date": "2026-11-19T12:00:00Z" } },
    { "authorName": "Ірина Бондар", "text": "Треба йти!", "createdAt": { "$date": "2026-11-19T13:10:00Z" } }
  ],
  "discussionId": { "$oid": "6750a0000000000000000001" },
  "isDeleted": false,
  "version": 1,
  "createdAt": { "$date": "2026-11-19T10:00:00Z" }
}
```

* `movieId` посилається на Каталог, `author.customerId` на Бронювання, **без FK**.
* `movieTitle` і `author.displayName` денормалізовано для читання, вони оновлюються подіями від власника.
* `comments` вбудовано (embedding), `discussionId` це посилання (referencing).

### `reviews`: короткий відгук (лише оцінка): інший набір полів у тій самій колекції

```json
{
  "_id": { "$oid": "6750c0000000000000000002" },
  "type": "quick",
  "movieId": { "$numberLong": "1" },
  "movieTitle": "Довбуш",
  "author": { "customerId": { "$numberLong": "3" }, "displayName": "Ірина Бондар" },
  "rating": 4,
  "isDeleted": false,
  "version": 1,
  "createdAt": { "$date": "2026-11-20T08:00:00Z" }
}
```

### `reviews`: відгук критика (вкладений об'єкт `criticInfo`, свої поля `pros`/`cons`)

```json
{
  "_id": { "$oid": "6750c0000000000000000003" },
  "type": "critic",
  "movieId": { "$numberLong": "3" },
  "movieTitle": "Дюна: Частина друга",
  "author": { "customerId": { "$numberLong": "2" }, "displayName": "Андрій Мельник" },
  "rating": 5,
  "text": "Масштабне продовження з бездоганним звуком.",
  "criticInfo": { "outlet": "Кіноблог КН", "score100": 92 },
  "pros": ["візуал", "саундтрек"],
  "cons": ["тривалість"],
  "containsSpoilers": false,
  "comments": [],
  "isDeleted": false,
  "version": 1,
  "createdAt": { "$date": "2026-11-22T21:00:00Z" }
}
```

### `discussions`: гібридний підхід

```json
{
  "_id": { "$oid": "6750a0000000000000000001" },
  "movieId": { "$numberLong": "1" },
  "movieTitle": "Довбуш",
  "title": "Наскільки фільм відповідає історії?",
  "createdBy": { "customerId": { "$numberLong": "1" }, "displayName": "Олена Коваль" },
  "status": "open",
  "tags": ["історія", "карпати"],
  "recentMessages": [
    { "messageId": { "$oid": "6750b0000000000000000002" }, "authorName": "Андрій Мельник", "text": "Костюми зроблені дуже ретельно.", "createdAt": { "$date": "2026-11-20T10:05:00Z" } },
    { "messageId": { "$oid": "6750b0000000000000000003" }, "authorName": "Ірина Бондар", "text": "А мені бракувало більше про побут.", "createdAt": { "$date": "2026-11-20T11:30:00Z" } },
    { "messageId": { "$oid": "6750b0000000000000000004" }, "authorName": "Олена Коваль", "text": "Погоджуюсь, але для кіно — норм.", "createdAt": { "$date": "2026-11-21T08:00:00Z" } }
  ],
  "messagesCount": 4,
  "lastMessageAt": { "$date": "2026-11-21T08:00:00Z" },
  "createdAt": { "$date": "2026-11-20T09:00:00Z" }
}
```

### `discussionMessages`: referencing

```json
{
  "_id": { "$oid": "6750b0000000000000000002" },
  "discussionId": { "$oid": "6750a0000000000000000001" },
  "replyTo": { "$oid": "6750b0000000000000000001" },
  "author": { "customerId": { "$numberLong": "2" }, "displayName": "Андрій Мельник" },
  "text": "Костюми зроблені дуже ретельно.",
  "reactions": { "like": 5, "love": 1 },
  "createdAt": { "$date": "2026-11-20T10:05:00Z" }
}
```

### `movieRatings`: read-модель

```json
{
  "_id": { "$numberLong": "1" },
  "movieTitle": "Довбуш",
  "average": 4.5,
  "count": 2,
  "distribution": { "1": 0, "2": 0, "3": 0, "4": 1, "5": 1 },
  "updatedAt": { "$date": "2026-11-26T13:00:00Z" }
}
```

## Індекси

| Індекс | Колекція | Під який запит |
|---|---|---|
| `ix_reviews_movie_createdAt` (partial `isDeleted: false`) | reviews | відгуки фільму, новіші першими |
| `ix_reviews_author` | reviews | «мої відгуки» в профілі |
| `ux_reviews_movie_author_viewer` (unique, partial `type: viewer`) | reviews | правило: один відгук глядача на фільм |
| `tx_reviews_text` (text) | reviews | повнотекстовий пошук |
| `ix_reviews_movie` | reviews | `updateMany` за подією MovieRenamed |
| `ix_discussions_movie_last` | discussions | список тем фільму, найактивніші зверху |
| `ix_discussions_tags` (multikey) | discussions | теми за тегом |
| `ix_messages_discussion_createdAt` | discussionMessages | пагінація повідомлень теми |
| `ix_movieRatings_top` | movieRatings | топ фільмів за рейтингом |

## Приклади aggregation pipeline (повністю в `db/p3/queries.js`)

```js
// відгук + його обговорення (referencing через $lookup)
db.reviews.aggregate([
  { $match: { discussionId: { $ne: null, $exists: true } } },
  { $lookup: { from: "discussions", localField: "discussionId", foreignField: "_id", as: "discussion" } }
]);

// перерахунок рейтингів у read-модель movieRatings
db.reviews.aggregate([
  { $match: { isDeleted: false } },
  { $group: { _id: "$movieId", movieTitle: { $first: "$movieTitle" },
              average: { $avg: "$rating" }, count: { $sum: 1 } } },
  { $merge: { into: "movieRatings", on: "_id", whenMatched: "merge" } }
]);

// найактивніші коментатори ($unwind вбудованого масиву)
db.reviews.aggregate([
  { $unwind: "$comments" },
  { $group: { _id: "$comments.authorName", comments: { $sum: 1 } } },
  { $sort: { comments: -1 } }
]);
```

## Версіонування схеми

Валідація через `$jsonSchema` з `validationLevel: "moderate"`: нові й змінені документи перевіряються, старі не ламаються. Зміна схеми робиться повторним запуском `collections.js` (він оновлює валідатор через `collMod`). Міграції даних оформлюються окремими скриптами в `db/p3/` (або Mongock у Лаб. 04).
