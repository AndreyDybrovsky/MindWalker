# Система противников

## Стационарный враг (EnemyPTSD)

- Компонент `StationaryEnemyController` — враг стоит на месте, при видимости игрока поворачивается и стреляет (`EnemyVision` + `EnemyShooting` + `EnemyHealth`).
- Префаб: `Assets/Import/Prefabs/Enemy/EnemyPTSD.prefab`.
- Повторная настройка выбранного объекта: меню **Tools → PTSD → Настроить выбранный объект как стационарного врага**.

## Нокаут и переход сцены

- `KnockoutSceneTrigger` — стилизованная подсказка E (`PressEPrompt`), удар, затемнение, загрузка сцены (по умолчанию `PTSD in Danger`).
- Для засады сзади включите **Fast Knockout Fade** (~0.35 с).
- Префаб: `Assets/Import/Prefabs/Triggers/KnockoutSceneTrigger.prefab`.

## Землянка

- `BunkerInteriorTeleportTrigger` — E, звук двери, плавное затемнение, телепорт к `Destination`.
- Назначьте пустой объект внутри землянки в поле **Destination**.

## Патрульный охранник (конус)

- Префаб: `Assets/Import/Prefabs/Enemy/EnemyPTSDInDanger.prefab`
- `PatrolConeGuardEnemy` + дочерний **VisionCone** (`ConeVisionVisualizer`) — патруль по **Patrol Points** (2+), конус обрезается стенами, 0.5 с в зоне → game over.
- **Ping Pong Route** — туда-обратно; иначе по кругу.
- **Obstacle Layer** = Default (стены).
- Один раз в Unity: **Tools → PTSD → Настроить префаб EnemyPTSDInDanger** (точки патруля + конус).
- Экземпляр в сцене: **Tools → PTSD → Настроить выбранный EnemyPTSDInDanger в сцене**.

## Автосохранение

- `AutoSaveSceneNotifier` подключается автоматически при запуске игры.
- При загрузке сцены (кроме MainMenu) сохраняет слот и показывает «Автосохранение» снизу слева (ключ `game.autosave`).

Эта система реализует полнофункциональных противников с патрулированием, обнаружением игрока, преследованием и стрельбой.

## Компоненты системы

### 1. PlayerHealth.cs
Система здоровья игрока. Добавьте этот компонент на объект игрока (с тегом "Player").

**Настройки:**
- `maxHealth` - максимальное здоровье (по умолчанию 100)
- События: `OnHealthChanged`, `OnPlayerDeath`

### 2. EnemyController.cs
Основной контроллер противника. Управляет патрулированием и преследованием.

**Требования:**
- NavMeshAgent компонент на объекте противника
- NavMesh должен быть запечен в сцене

**Настройки:**
- `patrolCenter` - центр зоны патрулирования (полусфера)
- `patrolRadius` - радиус зоны патрулирования
- `patrolSpeed` - скорость патрулирования
- `chaseSpeed` - скорость преследования
- `patrolWaitTime` - время ожидания на точке патрулирования

### 3. EnemyVision.cs
Область обнаружения игрока (зона глаз). Добавьте на дочерний объект противника.

**Настройки:**
- `detectionRadius` - радиус обнаружения
- `playerLayer` - слой игрока
- `obstacleLayer` - слой препятствий (для проверки видимости)

**События:**
- `OnPlayerDetected` - вызывается при обнаружении игрока
- `OnPlayerLost` - вызывается при потере игрока

### 4. EnemyShooting.cs
Система стрельбы противника.

**Настройки:**
- `bulletPrefab` - префаб пули
- `firePoint` - точка выстрела (Transform)
- `fireRate` - частота стрельбы (выстрелов в секунду)
- `damage` - урон от пули
- `bulletSpeed` - скорость пули

### 5. EnemyBullet.cs
Пуля противника с нанесением урона.

**Настройки:**
- `damage` - урон (можно установить через SetDamage)
- `speed` - скорость (можно установить через SetSpeed)
- `lifetime` - время жизни пули
- `playerTag` - тег игрока ("Player")

### 6. GameOverManager.cs
Менеджер окончания игры с затемнением экрана.

**Настройки:**
- `fadeCanvasGroup` - CanvasGroup для затемнения (создается автоматически если не указан)
- `fadeDuration` - длительность затемнения
- `holdTime` - время удержания черного экрана перед закрытием игры

## Настройка в Unity

### Шаг 1: Настройка игрока
1. Убедитесь, что объект игрока имеет тег "Player"
2. Добавьте компонент `PlayerHealth` на объект игрока
3. Настройте `maxHealth` по необходимости

### Шаг 2: Создание противника
1. Создайте GameObject для противника
2. Добавьте компоненты:
   - `NavMeshAgent`
   - `EnemyController`
   - `EnemyShooting`
3. Настройте NavMeshAgent:
   - Установите подходящие значения для скорости, ускорения и т.д.

### Шаг 3: Настройка области обнаружения
1. Создайте дочерний GameObject для области обнаружения
2. Добавьте компонент `EnemyVision`
3. Настройте радиус обнаружения
4. Установите правильные слои для `playerLayer` и `obstacleLayer`

### Шаг 4: Создание пули противника
1. Создайте GameObject для пули (например, сфера)
2. Добавьте компоненты:
   - `Rigidbody` (useGravity = false)
   - `Collider` (isTrigger = true)
   - `EnemyBullet`
3. Создайте префаб из этого объекта
4. Назначьте префаб в `EnemyShooting.bulletPrefab`

### Шаг 5: Настройка зоны патрулирования
1. В `EnemyController` установите `patrolCenter` (можно использовать сам противник)
2. Установите `patrolRadius` для определения размера зоны
3. Убедитесь, что NavMesh покрывает эту область

### Шаг 6: Настройка GameOverManager
1. Создайте пустой GameObject "GameOverManager"
2. Добавьте компонент `GameOverManager`
3. Система автоматически найдет `PlayerHealth` и подпишется на события

## Важные замечания

- **NavMesh (Unity 6)**: 
  - В Unity 6 используется **NavMeshSurface** компонент для запечения NavMesh
  - Добавьте компонент `NavMeshSurface` на пустой GameObject в сцене
  - Нажмите "Build" в компоненте NavMeshSurface для запечения
  - Убедитесь, что противник находится на поверхности с запеченным NavMesh
  - В `EnemyController` можно настроить `navMeshAreaMask` для указания конкретных областей NavMesh
- **Теги**: Игрок должен иметь тег "Player"
- **Слои**: Настройте слои для правильной работы обнаружения
- **Препятствия**: Используйте `obstacleLayer` для проверки видимости через препятствия

## Настройка NavMeshSurface в Unity 6

1. Создайте пустой GameObject в сцене (например, "NavMeshManager")
2. Добавьте компонент **NavMesh Surface** (Component → Navigation → NavMesh Surface)
3. В компоненте:
   - Выберите объекты, которые должны быть частью NavMesh (или используйте Layer)
   - Нажмите кнопку **"Build"** для запечения NavMesh
4. Убедитесь, что противник находится на запеченной поверхности при старте игры

## События и интеграция

Система использует Unity Events для связи компонентов:
- `PlayerHealth.OnPlayerDeath` → `GameOverManager.StartGameOver()`
- `EnemyVision.OnPlayerDetected` → `EnemyController.StartChase()`
- `EnemyVision.OnPlayerLost` → `EnemyController.StopChase()`

Все события настраиваются автоматически через код.
