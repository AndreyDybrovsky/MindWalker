# Дизайн-документ (GDD) — «DiplomWork»

> Рабочее название проекта. Дипломная работа. Жанр: сюжетный FPS с уровнями-«вариациями» по психическим расстройствам.
> Движок: **Unity 6** (URP), C#. Документ описывает текущее состояние проекта и служит основой для дальнейшего развития.
>
> **Назначение документа:** дать другой ИИ/разработчику полную картину — лор, структуру, механики, системы и техническую реализацию — чтобы продолжить и углубить работу. Разделы помечены статусом реализации: ✅ готово, 🟡 частично, ⛔ не сделано/задел.

---

## 1. Высокоуровневая концепция (High Concept)

**Главный герой** — врач-инженер таинственной организации. Он создал устройство, позволяющее «проникать» в сознание пациентов и решать их психологические проблемы изнутри. Каждый уровень — это путешествие в искажённый внутренний мир конкретного пациента, отражающий его расстройство.

- **Жанр:** FPS от первого лица с сильной нарративной и атмосферной составляющей. Часть уровней меняют жанр (платформер, стелс top-down, нарративная «симуляция дня»).
- **Структура игры:** **Лобби (хаб)** → выбор одной из **6 палат** (6 пациентов с уникальными болезнями и геймплеем) → возврат в лобби → по завершении всех — **одна из 4 концовок**.
- **Ключевая идея дизайна:** каждый уровень — авторская интерпретация расстройства через механику и сеттинг, а не просто «ещё одна стрелялка». Атмосфера и постобработка несут смысловую нагрузку.
- **Цель игрока:** «Спаси их: X/6» — вылечить как можно больше пациентов. Пациента можно **спасти**, **потерять** (сдаться/неудача) или оставить **больным** (не пройти).

### Тон и аудитория
Мрачный, психологический, эмпатичный. Тема — деликатная (психические расстройства), подаётся как метафора и попытка понять, а не высмеять.

---

## 2. Пациенты и уровни (обзор)

Игра построена вокруг 6 пациентов. Имена пациентов закреплены в проекте.

| # | Пациент | Расстройство | Сеттинг | Сигнатурная механика | Сцена (`Assets/Import/Scenes/Levels/`) |
|---|---------|--------------|---------|----------------------|-----------------------------------------|
| 1 | **Liukshin** | Лудомания (игромания) | Пропащий город / мусор → казино, босс | Игровые автоматы приманивают, наносят DoT-урон; выход — QTE по буквам клавиатуры | `Gambling disease.unity` |
| 2 | **Petrova** | Депрессия | Серый постсоветский город, тёмные тела-враги | Случайно появляется «паническая девочка» — успокоить (E), иначе урон; 3-фазный босс | `Depression.unity` |
| 3 | **Grishanin** | ПТСР | Окопы, война, солдаты | Принудительное изъятие оружия (нокаут) → стелс-секция top-down → вернуть оружие | `PTSD.unity` + `PTSD in Danger.unity` |
| 4 | **Zaharova** | Аутизм | Гиперболизированный мир: детская фантазия → постепенно «жуть»/реализм («взросление») | Платформер; цветовое оружие (стихии Red/Blue/Green) | `Autism.unity` |
| 5 | **Krivets** | ОКР | Советская квартира; день ото дня хуже (одержимость порядком) → разрыв петли | Нарративная «симуляция дней», квесты-рутина, не стрелялка | `OCD.unity` |
| 6 | **Vasiltsova** | Биполярное расстройство | Чередование: яркие холмы (Dreamcore / Meadow) ↔ гиперреалистичные жуткие тёмные миры | Резкие перепады локаций (Mindscape), искажённая постобработка | `Bipolar.unity` |

> **Примечание о PTSD:** уровень разбит на 2 сцены (`PTSD` и `PTSD in Danger`). В метрике прогресса они считаются **одной группой пациента** (см. `LevelSceneProgress.CountUniquePatientGroups`).

---

## 3. Лор и нарратив

### Сюжетная рамка
- Организация исследует/лечит сознание. Герой — инженер-врач, оператор устройства погружения.
- Лобби — это «реальный мир» / база. Палаты — точки входа в сознание пациентов.
- В каждом уровне разбросаны **6 коллекционных документов** («записки») — это нарративная мета-история. Их сбор влияет на концовку (раскрывает «правду»).

### Нарративные средства
- **Коллекционные документы** (`CollectableDocument`): скрытые объекты с пульсирующим светом, вращением, подсказкой E, звуком приближения. ID документа = имя сцены по умолчанию. Прогресс глобальный (`CollectableDocumentProgress`), нужно собрать все **6** для «полных» концовок.
- **Нарративные зоны/моменты** (`MessageZoneTrigger`, `OCDMomentTrigger`, субтитры `OCDCaptionUI`): текстовые/субтитровые вставки, триггеримые входом в зону.
- **Локализация** (RU/EN): система `LocalizationManager`, ключи в `strings_*.json` / `LocalizationBase`. Все тексты подсказок и UI локализуемы (ключ + fallback).

---

## 4. Структура игры и поток сцен (Game Flow)

```
MainMenu (главное меню, слоты сохранений, настройки)
   │
   ▼
Main  ←──────────────── ЛОББИ / ХАБ ─────────────────┐
   │  (выбор палаты через SceneTransitionTrigger)     │
   │   "Спаси их: X/6"                                 │
   ├── Gambling disease ──┐                            │
   ├── Depression ────────┤                            │
   ├── PTSD → PTSD in Danger ─┤  (по завершении уровня │
   ├── Autism ────────────┤    возврат в лобби) ───────┘
   ├── OCD ───────────────┤
   └── Bipolar ───────────┘
                          │
   когда обработаны ВСЕ пациенты (спасены + потеряны >= 6):
                          ▼
        EndingEvaluator → одна из 4 концовок:
        TrueVictory / FalseEnding / BadEnding / Failure
```

### Сцены проекта (не из «Trash»)
- **Меню/хаб:** `MainMenu`, `Main` (лобби), `Polygon` (тест-полигон).
- **Уровни:** `Gambling disease`, `Depression`, `PTSD`, `PTSD in Danger`, `Autism`, `OCD`, `Bipolar`.
- **Концовки:** `TrueVictory`, `FalseEnding`, `BadEnding`, `Failure`.

### Вход в палату
`SceneTransitionTrigger` (на зоне в лобби): подсказка «Press E» с локализацией, 3D-доска с инфо о пациенте у камеры (`PatientInfoBoardView`), звуки входа/выхода, по E — затемнение и загрузка целевой сцены.

---

## 5. Система концовок (Endings) ✅

Логика: `EndingEvaluator.cs`, поток: `LevelSuccessFlow.cs`.

Концовка определяется двумя параметрами:
- **Собраны ли все 6 документов** (`CollectableDocumentProgress.AllCollected`).
- **Сколько уникальных пациентов спасено** (`GlobalProgressTracker.CompletedPatientGroupsCount`) из 6.

| Все 6 документов | Спасены все пациенты | Спасено ≥ 3 | Концовка |
|:---:|:---:|:---:|---|
| Да | Да | — | **TrueEnding** (`TrueVictory`) — истинная победа |
| Да | Нет | — | **BadEnding** (`BadEnding`) — знал правду, но не всех спас |
| Нет | — | Да | **FalseEnding** (`FalseEnding`) — спас часть, но не узнал правды |
| Нет | — | Нет | **Failure** (`Failure`) — провал |

Концовка триггерится, когда **все пациенты обработаны**: `(спасённые + потерянные) >= totalLevels (6)`. До этого завершение уровня просто возвращает в лобби.

**Сцены концовок** содержат катсцены (`SimpleEndingCutscene`, `OtherEndingCutscene`, `TrueEndingCutscene`, `EndingSceneController`) и панель статистики (`EndingStatsPanel`).

---

## 6. Прогрессия и статусы пациентов ✅

- **`GlobalProgressTracker`** (singleton, DontDestroyOnLoad): хранит набор завершённых сцен-уровней, отображает «Спаси их: X/6». Прогресс грузится **только из сохранения** (не из PlayerPrefs при старте), чтобы не наследовать прогресс между слотами.
- **`LobbyPatientProgress`** (статика): статусы пациентов:
  - **Здоров (спасён)** — сцена в `completedLevels`.
  - **Потерян** — `MarkLost` (напр. в ОКР, если игрок «сдаётся»/ложится спать); хранится в сохранении.
  - **Болен** — не завершён и не потерян.
- **`LevelSceneProgress`** — нормализует группы: PTSD + «PTSD in Danger» = одна группа пациента (`CountUniquePatientGroups`).
- В лобби доски пациентов (`PatientInfoBoardView`, `LobbyPatientProgress`) и `LobbyTransitionBootstrap` отражают статусы.

---

## 7. Сохранения, статистика, настройки

### Сохранения ✅
- **`SaveManager`** (singleton): слоты сохранений, текущий слот, `GameSaveData`.
- `GameSaveData` хранит: позицию/здоровье игрока (`SaveGamePlayerUtility`), состояние врагов (`SaveGameEnemyUtility`), кастомные данные (`SaveGameCustomDataUtility` — произвольные блоки по ключу), прогресс документов, завершённые/потерянные уровни.
- Чекпойнты в нарративных уровнях: `OCDCheckpointSaveData`, `DepressionPanicSaveData`, `CollectableDocumentSaveData`.
- ⚠️ **Особенность тестирования:** автотест отдельной сцены в Play Mode не работает изолированно — бутстрап сохранений (`GlobalProgressTracker.ApplySaveDataAfterSceneLoad`) подгружает сейв и уводит из сцены. Проверять уровни **вручную** через нормальный поток (меню → палата). Структурные проверки делать скриптом в Edit Mode.

### Статистика ✅
- **`GameStatsTracker`** (singleton, авто-создаётся `RuntimeInitializeOnLoadMethod`): время прохождения каждого уровня и общее, полученный/нанесённый урон, убитые враги, пойман автоматом (лудомания), успокоено пациентов. Сохраняется в `customData` слота. Используется на экранах концовок.

### Настройки ✅
- **`SettingsManager`** + `GameSettingsScript`, `SettingsPanelUi`, `SettingsManager.OnSettingsApplied`.
- Громкость (Master/Music/SFX через AudioMixer — `AudioMixerRoutingUtility`), язык (RU/EN), геймплейные параметры (`GameplaySettingsUtility`, `PlayerGameplaySettingsApplier` — напр. чувствительность/инверсия).

---

## 8. Базовые механики (ядро, общее для уровней)

### 8.1 Управление и игрок
- **FPS-контроллер:** сторонний `FirstPersonControllerPro` (namespace `ElmanGameDevTools.PlayerSystem`, класс `PlayerController`) — ходьба/бег/прыжок/приседание, `CharacterController`.
- **Префаб игрока:** `Assets/Import/Prefabs/Base/Player.prefab`. Иерархия: `Player → Player_Object → Main Camera → Tools/Gun` (оружие — viewmodel у камеры) + модель `GG`.
- **Альтернативные режимы движения:**
  - `PlatformerPlayerMovement` (Autism — платформер, трамплины).
  - `TopDownPlayerMovement` + `TopDownCameraFollow` (PTSD in Danger — вид сверху, стелс; FPS-контроллер и Main Camera отключаются `PTSDInDangerSceneConfigurator`).
- **Взаимодействие:** базовый класс `PlayerInteractionZone` — зона + подсказка «E» (`PressEPromptView`, координируется `PressEPromptCoordinator`). От него наследуются документы, зоны успокоения, выходы и т.п.
- **Блокировка ввода:** `GameplayInputBlocker.IsBlocked` (во время катсцен/диалогов/QTE).

### 8.2 Здоровье ✅
- **`PlayerHealth`**: maxHealth 100, события `OnHealthChanged`/`OnPlayerDeath`, звуки урона/лечения, кулдаун звука урона, `SetSuppressDamageFeedback`. Смерть → `OnPlayerDeath` (триггерит аниматор смерти + `GameOverManager`).
- **`EnemyHealth`**: `OnHealthChanged`, `OnEnemyDeath` (UnityEvent), `OnDamaged`. Учитывается в статистике.
- **`HealthPickup`** — лечение, **`PlayerDamageVignette`** — красная виньетка при уроне.
- **`GameOverManager`** — экран поражения при смерти игрока.

### 8.3 Оружие и стрельба ✅
- **`WeaponHandler`** (на игроке/оружии): пуля-префаб, скорость, урон, `fireRate`, ЛКМ. Спавнит `PlayerBullet`. Звуки выстрела (массив, случайный выбор). Учитывает `GameplayInputBlocker`.
- **`PlayerBullet`** — урон/скорость/владелец. **`EnemyBullet`** — пуля врага.
- **`WeaponPickupEffect`** — подбор оружия.

### 8.4 Враги (ИИ) ✅
Модульная система в `Levels/Common/Enemy/`:
- **`EnemyController`** — патруль + преследование на **NavMesh** (Unity 6). Патруль по кольцам, обнаружение через `EnemyVision`, поворот к игроку, восстановление при сходе с NavMesh, «упорное преследование» (`persistentChase`), блок движения сквозь стены зданий (CapsuleCast по слою препятствий), пространственный звук (патруль/погоня/атака/смерть).
- **`EnemyVision`** — конус зрения с проверкой препятствий (raycast), события `OnPlayerDetected`/`OnPlayerLost`.
- **`ConeVisionVisualizer`** — визуализация конуса (для стелс-врагов).
- **`EnemyShooting`** — стрельба: `fireRate`, `damage`, множители урона/скорострельности (`ApplyDamageMultiplier`, `SetFireRateMultiplier`), событие `OnShoot`. Стреляет только при прямой видимости (без стрельбы сквозь стены).
- **`StationaryEnemyController`** — стационарный стрелок (без NavMesh; стоит и стреляет; коридор Bipolar, стрелки PTSD).
- **`PatrolConeGuardEnemy`** — стелс-охранник с конусом (PTSD in Danger).
- **`PeriodicProximityDamageEnemy`** — наносит периодический урон при близости (мелейный, лудомания).
- **`EnemyCounter`** — счётчик врагов на уровне.
- **`BossSpawnManager`** — спавн боссов.
- **`EnemyAnimatorDriver`** — единый драйвер анимаций врагов (см. §11).

### 8.5 Триггеры/зоны (общие)
`FallDamageZone`, `FallRecoveryZone` (Autism — падение/возврат), `BunkerInteriorTeleportTrigger`, `KnockoutSceneTrigger` (нокаут → смена сцены), `LightFlicker`, `CameraWallClip` (камера не проходит сквозь стены), `LevelSuccessExitTrigger` (выход-победа), `GameTimer`/`WatchHandDisplay` (таймеры/часы).

### 8.6 Постобработка как нарратив ✅
- **`LevelAtmosphere`** + `PostProcessingTrigger` (+ редактор `LevelAtmosphereEditor`): зоны переключают URP Volume для смены настроения. Принцип — постобработка несёт смысл (тревога, мрак, искажение).

---

## 9. Сигнатурные механики по уровням

### 9.1 Лудомания (Gambling) — Liukshin
- **Игровой автомат** (`SlotMachineController`, ~750 строк): зона приманки (`SlotMachineLureZone`) притягивает игрока к автомату, блокирует управление, запускает мини-игру:
  - Последовательность из N букв (по умолч. 5), на каждую букву ~4с.
  - **DoT-урон** `healthDrainPerSecond` (10/с), пока игрок «пойман»; неверная клавиша — доп. урон; таймаут — доп. урон.
  - Успешный ввод последовательности → освобождение + кулдаун. Смерть у автомата — особый звук.
  - UI букв (`SlotMachineLetterUI`), опасные эффекты (`SlotMachineDangerFX`), ambient казино.
- **Колесо фортуны** (`FortuneWheelController`, `FortuneWheelSpinZone`).
- **Враги-бездомные** (`EnemyLudomania` = Homeless мелейный; `EnemyLudomania2` = стрелок; `BossLudomania`).
- **Казино/босс:** `CasinoZoneActivation`, `CasinoChoicePresenter` (выбор), `CasinoBossEncounterTrigger`.
- **Истинная концовка-катсцена:** `TrueEndingCutscene` связана с этим уровнем.

### 9.2 Депрессия (Depression) — Petrova
- **Паническая девочка/пациент** (`DepressionPanicMomentZone`, `DepressionCalmInteraction`, `PanicPatientAmbientAudio`): случайно появляется модель в панике — игрок должен подойти и **успокоить (E)**, иначе получает урон (метафора ментального состояния). Прогресс/чекпойнт: `DepressionPanicSaveData`. Учитывается в статистике (`RecordPatientCalmed`).
- **Босс — 3 фазы** (`DepressionBossController`):
  1. **Апатия** (100–60% HP): патруль+погоня, периодический «Крик отчаяния» — замедление игрока + тряска камеры.
  2. **Раскол** (60–30%): спавн фантома-копии (`BossPhantomMark`), затемнение экрана.
  3. **Паника** (<30%): телепорты каждые 4с, спавн 2 теней, сильное затемнение, удвоенная скорострельность.
- **Враги** — тёмные тела (модель «Тень»), стреляют. `DepressionCountdownUI`.

### 9.3 ПТСР (PTSD + PTSD in Danger) — Grishanin
- **Сцена `PTSD`:** окопы, война, солдаты-стрелки (стационарные/патрульные, риг `SandSoldier`).
- **Изъятие оружия → стелс:** `KnockoutSceneTrigger` — игрока «вырубают» (удар дубинкой, звук, затемнение) → загрузка `PTSD in Danger`.
- **Сцена `PTSD in Danger`:** `PTSDInDangerSceneConfigurator` переключает на **вид сверху** (top-down), отключает FPS-управление и оружие → **стелс-секция**: обойти охранников с конусами зрения (`PatrolConeGuardEnemy`, `ConeVisionVisualizer`). Камера — `TopDownCameraFollow`, движение — `TopDownPlayerMovement`.
- По завершении стелса — возврат оружия и/или возврат в основную линию (⛔ финальная связка требует доработки/проверки).

### 9.4 Аутизм (Autism) — Zaharova ✅ (детально проработан)
- **Жанр:** платформер (летающие острова, трамплины).
- **Цветовое оружие** (стихии): `ColorManager` (enum `ElementColor` Red/Blue/Green), `ColorBullet`, `WeaponColorController` (Q/E/колесо мыши), `ColorPickupController` (подбор `ColorGunObject` открывает крашение), `ColorHUDIndicator` (круг текущего цвета).
  - **`ColorEnemy`:** попадание **совпадающим** цветом → урон; **несовпадающим** → бафф урона врага (до N стэков). Модель врага тинтится в свой цвет (подсказка).
- **Двухслойная постобработка:**
  1. **Пространственный слой** (priority 0): зоны `Volume_Calm`/`Dark`/`Overwhelm` через `PostProcessingTrigger` — «линза» сенсорного путешествия (Bloom/Vignette/Chromatic Aberration/Lens Distortion/Film Grain).
  2. **Слой «взросления»** (priority 20+, рантайм): `AutismMaturationController` — мир от Дня 1 (фантазия, насыщенность +45, тёплый split-toning) к Дню 4 (реализм, −12, ACES, холоднее). API `SetDaySmooth`/`AdvanceDay`, событие `DaySet`. Триггер — `AutismMaturationGate`.
- **Оживление мира:** `FloatingIslandDrift` (покачивание островов), `TrampolineJuice`/`TrampolinePad` (squash/частицы/вспышка), `AutismSkyMaturation`/`AutismSkyEffects` (цвет неба/солнца/тумана синхронно с днём).
- **Метафора:** мир взрослеет → становится «реалистичнее»/жёстче («не такой, как все»).

### 9.5 ОКР (OCD) — Krivets ✅ (нарративная система дней)
- **Жанр:** нарративная «симуляция дней», не стрелялка. Советская квартира.
- **Структура дней** (`OCDMissionDayController`): корни `Mission/Day1..Day4`, активен один; смена дня в темноте. С каждым днём — деградация.
- **Деградация картинки** (`OCDDayDeteriorationController`): 4 URP Volume, нарастающий мрак/холод/аберрация (День1 нейтрально → День4 тёмно, тревожно).
- **Навязчивые мысли** (`OCDIntrusiveThoughtOverlay`), **ambient по дням** (`OCDAmbientDaySound`), субтитры (`OCDCaptionUI`), цели (`OCDObjectiveUI`), моменты (`OCDMomentTrigger`, `OCDMomentZone`, `OCDAutoMomentZone`), пресеты атмосферы (`OCDAtmospherePreset`/`Overrides`), подсветка-подсказка (`OCDGuideLightPulse`).
- **Геймплей-рутина:** базовые квесты (поесть, убраться) — но одержимость порядком нарастает.
- **Финальная развилка Дня 4** (важно — влияет на мета-концовку):
  - Цепочка: Поешь → Уберись → Уберись → Ляг спать → момент «Выберись» → появляются **4 точки FixAll** (`OCDFixAllCollectiblePoint`/`Manager`) + **зона сна** (`OCDSleepGiveUpZone`). Цель сверху: «Забудь(?)».
  - **Ветка «Сон»** (E у кровати): пациент **ПОТЕРЯН** (`LobbyPatientProgress.MarkLost`), уровень завершён, возврат в лобби.
  - **Ветка «Починка»** (собрать все 4 FixAll): момент «К чёрту всё…» → цель «Излечись» → `HealExit` (`LevelSuccessExitTrigger`) помечает пациента **СПАСЁННЫМ**.
- Гейтинг через деактивацию + `enableDuringBlack` (точки/выход стартуют выключенными, раскрываются нужным моментом).

### 9.6 Биполярное расстройство (Bipolar) — Vasiltsova 🟡
- **Чередование восприятия** (`BipolarMindscapeController` на объекте `Locations`): режимы `Meadow` (0, добрый мир / Dreamcore) ↔ `Nightmare` (1, жуткий тёмный мир). Эффекты кошмара — `BipolarNightmareEffects`, амбиент — `BipolarAmbientController`/`Zone`.
- **Структура карты:** `GoodWorld` (луг/Meadow, хаб) + 3 локации (`Location1/2/3`). В добром мире — кубы-порталы (`BipolarMindscapeSwitchTrigger`) телепортируют в локации:
  - **TP1 → Церковь (Location1)**, **TP2 → Коридор (Location2)**, **TP3 → Квартира (Location3)**.
  - Выход из локации возвращает в добрый мир к следующей точке (цепочка): церковь→TP2, коридор→TP3, квартира→финал.
- **Босс «Церковь»** (`EnemyBipolarCranch` — Монахиня): стоит до обнаружения, затем чейз; на 50% и 25% HP — фаза стана `BipolarBossStunPhase` («Не двигайся»); смерть → `BipolarBossDefeatWarp` (возврат в Meadow+TP2).
- **Коридор:** 4 стационарных стрелка (`EnemyBipolarCorr`).
- **Завершение уровня:** `BipolarEndTrigger` (в квартире). `LevelCompletionManager` намеренно удалён (механика «убей всех → конец» здесь не нужна).
- **Сбор таблеток** (`PillBottlePickup`, `PillCollectionManager`, `PillQuestActivateTrigger`) — квест-линия.
- **Статус:** точки телепортов на 2026-06-12 не разнесены (стоят в одной точке) — требует раскладки; финальная связка квартиры — доработка.

---

## 10. UI и меню

- **Главное меню** (`MainMenuController`, `WelcomeMenuController`, `MainMenuButtonsReveal`, `MenuCameraHeadSway`): анимированный вход, кнопки.
- **Слоты сохранений** (`SaveSlotListController`, `SaveSlotItem`, `InGameSaveButton`).
- **Настройки** (`SettingsPanelUi`, `SettingsTabButtonStyle`, `SettingsLocalizedText`, `GameSettingsScript`): вкладки, локализация, звук.
- **Игровой UI:** прогресс «Спаси их: X/6» (`QuestText`), здоровье, подсказки «E» (`PressEPromptView`), цели уровней (`OCDObjectiveUI`), субтитры, цветовой индикатор (`ColorHUDIndicator`), виньетка урона, экран концовки + статистика (`EndingStatsPanel`).
- **Звук:** `SoundManager`, `SoundButton`, `MusicAudioBootstrap`, `SfxAudioBootstrap`, `FootstepAudioBootstrap`, `AudioMixerRoutingUtility` (маршрутизация в AudioMixer Master/Music/SFX).

---

## 11. Анимации (статус) 🟡

### Игрок (Иван / GG)
- Модель `GG` (Humanoid), `Animator` + `PlayerAnimatorDriver`. Отдельный аватар `PlayerHumanAvatar.asset` (собран `AvatarBuilder.BuildHumanAvatar` под фактическую иерархию префаба — FBX-аватар ломается).
- Контроллер `PlayerAnimator.controller`: LocoUnarmed ↔ LocoArmed (по `Armed`), Crouch, Air/Jump, Death (по `Die`). Драйвер гонит Speed/Grounded/Crouch/Jump.
- В лобби (`Main`) — отдельный контроллер `GGMain` (idle/walk/run/jump) как scene-override; боевые уровни — пистолетный контроллер.
- ⛔ TODO: безоружные idle/run, авто-`Armed` по наличию `WeaponHandler`, привязка пушки к кости руки (`Palm.R`).

### Враги
- Единый драйвер `EnemyAnimatorDriver` (Speed по скорости NavMeshAgent или transform; триггеры Shoot/Attack/Hit/Die). Контроллеры: `EnemyPistol` (стрелки), `EnemyHomeless` (бездомный, удар Stabbing), `EnemyShadow` (зомби-пак для теней), `EnemyPTSD` (солдат с винтовкой).
- **Анимировано:** Bipolar Corr/Cranch, Homeless, BossLudomania (Mafia-риг), тени (Depression, Autism R/G/B, Depr/Boss — риг T-PoseShadow), PTSD-солдат (SandSoldier).
- ⛔ **Единственный без анимаций:** `EnemyLudomania2` (модель BodyGuard, нужен риганный T-pose).

---

## 12. Техническая архитектура

- **Движок:** Unity 6, URP (Volume-постобработка ключевая для нарратива).
- **Навигация:** Unity NavMesh (запечённые поверхности по локациям, напр. `Bipolar_ChurchNavMesh.asset`).
- **Паттерны:**
  - Синглтоны DontDestroyOnLoad: `GlobalProgressTracker`, `GameStatsTracker`, `SaveManager`, `SettingsManager`, `LocalizationManager`.
  - Статические менеджеры/утилиты: `ColorManager`, `LobbyPatientProgress`, `LevelSceneProgress`, `EndingEvaluator`, `LevelSuccessFlow`, `CollectableDocumentProgress`.
  - События (C# `Action`/`UnityEvent`) для слабой связности (`OnShoot`, `OnPlayerDetected`, `OnHealthChanged`, `OnColorChanged`, `DaySet`).
  - Авто-создание систем через `RuntimeInitializeOnLoadMethod`.
- **Редакторные инструменты** (`Scripts/Editor/`): валидаторы и кастом-инспекторы префабов врагов/слот-машины/конусов, регистратор build-сцен, бутстрап локализации.
- **Сторонние ассеты** (в `Assets/Trash/`, `AllSkyFree`, `Mirza Beig` и т.д.): FPS-контроллер (`ElmanGameDevTools`), небеса AllSky, волюметрический туман, пистолет K-39, TextMesh Pro, URP-сэмплы. **Не считать частью авторского кода.**
- **Папки скриптов:** ядро — `Assets/Import/Scripts/` (`GlobalScripts`, `Levels/{Common,Autism,Bipolar,Depression,Gambling,PTSDInDanger}`, `Lobby`, `MainMenu`, `GameMenu`, `UI`, `Rendering`, `Editor`).

---

## 13. Текущий статус и риски (на 2026-06-16)

**Защита диплома скоро, работы много.** Приоритет — **завершение и полировка существующих уровней**, а не новые механики.

| Область | Статус | Заметки |
|---|---|---|
| Ядро (игрок, оружие, враги, здоровье) | ✅ | Стабильно |
| Сохранения/статистика/настройки/локализация | ✅ | Работает |
| Система концовок + прогрессия | ✅ | Реализована |
| Autism | ✅ | Детально проработан (цвет, взросление, оживление) |
| OCD | ✅ | Дни + финальная развилка готовы |
| Depression | ✅/🟡 | Босс 3 фазы, паническая девочка готовы |
| Gambling | 🟡 | Автоматы/колесо/босс есть; полировка |
| Bipolar | 🟡 | TP-точки не разнесены, финал квартиры — доработка |
| PTSD / PTSD in Danger | 🟡 | Стелс top-down готов; финальная связка «вернуть оружие» — проверить |
| Анимации | 🟡 | Почти всё; `EnemyLudomania2` без рига; пушка в руке игрока |

---

## 14. Глоссарий ключевых классов (быстрый указатель)

- **Прогресс/мета:** `GlobalProgressTracker`, `LobbyPatientProgress`, `LevelSceneProgress`, `EndingEvaluator`, `EndingType`, `LevelSuccessFlow`, `CollectableDocumentProgress`.
- **Сохранения/статистика:** `SaveManager`, `GameSaveData`, `GameStatsTracker`, `GameStatsData`, `SaveGame*Utility`.
- **Игрок/бой:** `PlayerController` (сторонний), `PlayerHealth`, `WeaponHandler`, `PlayerBullet`, `PlatformerPlayerMovement`, `TopDownPlayerMovement`.
- **Враги:** `EnemyController`, `EnemyVision`, `EnemyShooting`, `EnemyHealth`, `StationaryEnemyController`, `PatrolConeGuardEnemy`, `PeriodicProximityDamageEnemy`, `EnemyAnimatorDriver`.
- **Лудомания:** `SlotMachineController`, `SlotMachineLureZone`, `FortuneWheelController`, `CasinoBossEncounterTrigger`.
- **Депрессия:** `DepressionBossController`, `DepressionPanicMomentZone`, `DepressionCalmInteraction`, `BossPhantomMark`.
- **ПТСР:** `KnockoutSceneTrigger`, `PTSDInDangerSceneConfigurator`, `TopDownCameraFollow`.
- **Аутизм:** `ColorManager`, `ColorEnemy`, `WeaponColorController`, `ColorPickupController`, `AutismMaturationController`, `FloatingIslandDrift`, `TrampolinePad`.
- **ОКР:** `OCDMissionDayController`, `OCDDayDeteriorationController`, `OCDFixAllCollectibleManager`, `OCDSleepGiveUpZone`, `OCDIntrusiveThoughtOverlay`.
- **Биполярка:** `BipolarMindscapeController`, `BipolarMindscapeSwitchTrigger`, `BipolarBossStunPhase`, `BipolarBossDefeatWarp`, `BipolarEndTrigger`, `PillCollectionManager`.
- **Лобби/меню:** `SceneTransitionTrigger`, `PatientInfoBoardView`, `MainMenuController`, `SettingsManager`, `SoundManager`, `LocalizationManager`.

---

## 15. Рекомендации для дальнейшей проработки (для следующей ИИ)

Документ намеренно фактологичен. При углублении стоит раскрыть/добавить:
1. **Подробные тексты нарратива** для каждого уровня и 6 документов (сейчас есть только структура и ключи локализации).
2. **Баланс-таблицы:** HP/урон/скорость врагов и боссов, тайминги QTE, DoT-значения, пороги фаз — собрать из инспекторов префабов в единую таблицу.
3. **Полные сценарии прохождения** (walkthrough) каждого уровня по шагам + карта целей.
4. **Спецификации концовок:** содержание катсцен `TrueVictory/FalseEnding/BadEnding/Failure`.
5. **Дизайн звука и музыки** по уровням (сейчас разрознено по `Resources/Sounds`).
6. **Достижение «защитного» состояния:** чек-лист полировки незавершённых уровней (Bipolar TP, PTSD-возврат оружия, риг `EnemyLudomania2`).
