# Карта скриптов проекта

Структура папок (после реорганизации):

| Папка | Назначение |
|--------|------------|
| `GlobalScripts/` | Сохранения, прогресс, локализация, затемнение, ввод, телепорт, подсказка E |
| `MainMenu/` | Главное меню, слоты сохранений, приветствие |
| `Lobby/` | Сцена `Main` — хаб между уровнями |
| `GameMenu/` | Пауза в игре (`GameSettingsScript`, `UIManager`) |
| `Levels/Common/` | Общая логика уровней (враги, оружие, победа/поражение, триггеры) |
| `Levels/PTSDInDanger/` | Только сцена `PTSD in Danger` (вид сверху) |
| `UI/` | Панель настроек в игре |
| `Rendering/` | Атмосфера уровня |
| `Editor/` | Меню Tools, префабы врагов, build settings |

---

## Что должно быть в каждой зоне

### Главное меню (`MainMenu`)

**Сцена:** `Assets/Import/Scenes/MainMenu.unity`

| Компонент | Скрипт |
|-----------|--------|
| Камера (лёгкое покачивание) | `MenuCameraHeadSway` |
| Кнопки: играть, настройки, выход | `MainMenuController`, `WelcomeMenuController` |
| Слоты сохранений | `SaveSlotListController`, `SaveSlotItem` |
| Звуки UI | `SoundManager` |
| Локализованные подписи | `SettingsLocalizedText` |
| DontDestroyOnLoad | `SaveManager`, `SettingsManager`, `LocalizationManager` (создаются при старте) |

**Не нужно:** враги, `LevelCompletionManager`, `WeaponHandler`, лобби-триггеры.

---

### Лобби (`Main`)

**Сцена:** `Assets/Import/Scenes/Main.unity`

| Компонент | Скрипт |
|-----------|--------|
| Точки входа в уровни (E → затемнение → загрузка) | `SceneTransitionTrigger` на каждой двери/портале |
| Доска статуса пациента у камеры | `PatientInfoBoardView` (префаб + `SceneTransitionTrigger`) |
| Общие UI лобби (подсказка E, fade) | `LobbyTransitionBootstrap` |
| Прогресс «Спаси их: X/6» | `GlobalProgressTracker` (объект в сцене или DDOL) |
| Статус пациентов (здоров / болен / проигран) | `LobbyPatientProgress` (статический, данные из сохранения) |

**Не нужно:** `EnemyCounter`, `GameOverManager` (если нет боя в лобби).

---

### Игровые уровни (общее для всех)

**Сцены:** `Autism`, `Depression`, `Level4`, `Level5`, `ludomania`, `PTSD`, `PTSD in Danger`, …

| Компонент | Скрипт |
|-----------|--------|
| Старт с чёрного экрана | `FadeStart` |
| Игрок: здоровье | `PlayerHealth` |
| Пауза | префаб с `GameSettingsScript` + `UIManager` |
| Победа (все враги) | `LevelCompletionManager` + `EnemyCounter` |
| Победа (зона выхода + звуки) | `LevelSuccessExitTrigger` |
| Поражение | `GameOverManager` |
| Таймер (если есть) | `GameTimer` |
| Оружие FPS | `WeaponHandler`, префаб `PlayerBullet` |
| Враги | `EnemyHealth`, `EnemyVision`, `EnemyShooting` / `StationaryEnemyController` / `PatrolConeGuardEnemy` |
| Босс | `BossSpawnManager` |
| Часы на руке (опционально) | `WatchHandDisplay` на префабе `Watch` |
| Атмосфера | `LevelAtmosphere` |

**Глобально (не в сцене, но работают):** `AutoSaveSceneNotifier`, `SaveManager`, `GameplayInputBlocker`.

---

### PTSD (`PTSD.unity`)

Всё из **общего уровня**, плюс:

| Компонент | Скрипт |
|-----------|--------|
| Землянка / телепорт внутрь | `BunkerInteriorTeleportTrigger` |
| Засада / нокаут → другая сцена | `KnockoutSceneTrigger` |
| Стационарные враги | `StationaryEnemyController` |

---

### PTSD in Danger (`PTSD in Danger.unity`)

Всё из **общего**, но вместо FPS-движения:

| Компонент | Скрипт |
|-----------|--------|
| Bootstrap сцены | `PTSDInDangerSceneConfigurator` |
| Камера сверху | `TopDownCameraFollow` |
| Движение WASD по камере | `TopDownPlayerMovement` |
| Патруль + конус | `PatrolConeGuardEnemy`, `ConeVisionVisualizer` |

---

### Victory (`Victory.unity`)

Финальная сцена после прохождения всех уровней — только UI/камера, без боевой логики. Загрузка из `LevelSuccessFlow.ResolveReturnScene`.

---

## Удалённые / объединённые

| Было | Действие |
|------|----------|
| `TimerExample.cs` | **Удалён** — демо, нигде не использовался |
| `BulletHandler.cs` | **Удалён** — дублировал `lifetime` в `PlayerBullet` |
| `SetupFadeCanvas` в `LevelCompletionManager` / `GameOverManager` | **Объединено** с `ScreenFadeUtility` + `ScreenFadeRunner` |

**Не объединять:** `FadeStart` (вход в уровень, светлеет) и `ScreenFadeRunner` (переходы / победа / поражение) — разные сценарии.

---

## GlobalScripts — полный список

- `SaveManager`, `GameSaveData`, `AutoSaveSceneNotifier`
- `GlobalProgressTracker`, `LobbyPatientProgress`, `LevelSuccessFlow`
- `LocalizationManager`, `LocalizedTextKeyResolver`, `LocalizedUiLabelsHost`, `LanguageDropdownBinder`
- `ScreenFadeUtility`, `ScreenFadeRunner`
- `GameplayInputBlocker`, `UiMenuTransitions`
- `PlayerTeleportUtility`, `PlayerInteractionZone`
- `PressEPromptUtility`, `PressEPromptView`
- `SettingsManager`, `GameplaySettingsUtility`, `PlayerGameplaySettingsApplier`

---

## Editor (не в билде)

- `EnemyPTSDInDangerPrefabEditor`, `PatrolConeGuardPrefabEditor`, `PatrolConeGuardValidator`
- `StationaryEnemyPrefabEditor`, `LevelAtmosphereEditor`
- `ProjectBuildScenesRegistrar`, `EditorUtilities`, `LocalizationEditorBootstrap`

---

## Ошибки Inspector (`SerializedObjectNotCreatableException`)

Не связаны со скриптами C# — битая ссылка на компонент в префабе (часто `VisionCone` / `MeshRenderer` без mesh). Пересоздайте префаб через **Tools → PTSD → Настроить префаб EnemyPTSDInDanger**.
