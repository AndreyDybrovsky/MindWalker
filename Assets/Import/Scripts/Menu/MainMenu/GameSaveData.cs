using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameSaveData
{
    // Метаданные сохранения
    public string saveName;
    // Для дефолтного имени: хранить ключ и номер (чтобы имя не зависело от языка)
    public string saveNameKey;
    public int saveNameNumber;
    public string saveDate;
    public int saveSlotIndex;
    public bool isEmpty;
    
    // Данные игрока
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public float playerHealth;
    public float playerMaxHealth;
    
    // Данные противников
    [Serializable]
    public class EnemyData
    {
        public string enemyId;
        public Vector3 position;
        public Quaternion rotation;
        public float health;
        public float maxHealth;
        public bool isAlive;
        public string enemyType; // Тип противника (для идентификации при загрузке)
    }
    
    public List<EnemyData> enemies = new List<EnemyData>();
    
    // Дополнительные данные игры
    public string currentScene;
    public float playTime; // Время игры в секундах
    public float timerRemainingTime; // Оставшееся время таймера
    
    // Данные счетчика врагов
    public int enemyCounterTotalEnemies = 0; // Всего врагов
    public int enemyCounterDefeatedEnemies = 0; // Побеждено врагов
    
    // Данные босса
    public bool bossWasSpawned = false; // Был ли босс заспавнен
    public Vector3 bossPosition; // Позиция босса
    public Quaternion bossRotation; // Поворот босса
    public float bossHealth = 0f; // Здоровье босса
    public float bossMaxHealth = 0f; // Максимальное здоровье босса
    public bool bossIsAlive = false; // Жив ли босс
    
    // Прогресс завершенных локаций для этого сохранения
    public List<string> completedLevels = new List<string>();

    /// <summary>Сцены локаций, где игрок «потерял» пациента (смерть / время), пока не вылечен снова.</summary>
    public List<string> lostPatientLevels = new List<string>();
    
    // Для дополнительных данных используйте список пар ключ-значение
    [Serializable]
    public class CustomDataPair
    {
        public string key;
        public string value;
    }
    
    public List<CustomDataPair> customData = new List<CustomDataPair>(); // Для дополнительных данных
    
    // Конструктор для нового сохранения
    public GameSaveData(int slotIndex)
    {
        saveSlotIndex = slotIndex;
        isEmpty = true;
        saveName = ""; // для старых сейвов поле могло быть заполнено русским текстом
        saveNameKey = "save.default_name";
        saveNameNumber = slotIndex + 1;
        saveDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        playerPosition = Vector3.zero;
        playerRotation = Quaternion.identity;
        playerHealth = 100f;
        playerMaxHealth = 100f;
        currentScene = "";
        playTime = 0f;
        timerRemainingTime = 0f;
        enemies = new List<EnemyData>();
        completedLevels = new List<string>();
        lostPatientLevels = new List<string>();
    }
    
    // Метод для проверки, является ли сохранение пустым
    public bool IsEmpty()
    {
        return isEmpty;
    }
    
    // Метод для обновления даты сохранения
    public void UpdateSaveDate()
    {
        saveDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        isEmpty = false;
    }
}
