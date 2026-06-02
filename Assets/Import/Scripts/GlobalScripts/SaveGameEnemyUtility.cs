using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Восстановление противников из сейва без массового отключения всей сцены.
/// </summary>
public static class SaveGameEnemyUtility
{
    public static int RestoreEnemies(GameSaveData saveData)
    {
        if (saveData?.enemies == null || saveData.enemies.Count == 0)
            return 0;

        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        Dictionary<string, GameObject> enemyDict = BuildEnemyLookup(allEnemies);
        Dictionary<string, GameSaveData.EnemyData> savedById = BuildSavedLookup(saveData.enemies);

        int restoredCount = 0;

        foreach (KeyValuePair<string, GameSaveData.EnemyData> pair in savedById)
        {
            GameSaveData.EnemyData enemyData = pair.Value;
            if (enemyData == null)
                continue;

            if (!TryFindEnemy(enemyDict, enemyData.enemyId, out GameObject enemy))
            {
                Debug.LogWarning(
                    $"SaveGameEnemyUtility: противник '{enemyData.enemyId}' не найден. Доступные: {string.Join(", ", enemyDict.Keys)}");
                continue;
            }

            if (enemy.TryGetComponent(out PatrolConeGuardEnemy patrolGuard))
                ApplyPatrolGuard(enemy, patrolGuard, enemyData);
            else
                ApplyStandardEnemy(enemy, enemyData);

            restoredCount++;
        }

        return restoredCount;
    }

    private static Dictionary<string, GameObject> BuildEnemyLookup(GameObject[] allEnemies)
    {
        Dictionary<string, GameObject> enemyDict = new Dictionary<string, GameObject>();

        for (int i = 0; i < allEnemies.Length; i++)
        {
            GameObject enemy = allEnemies[i];
            if (enemy == null)
                continue;

            string normalized = NormalizeEnemyName(enemy.name);
            if (!enemyDict.ContainsKey(normalized))
                enemyDict[normalized] = enemy;
            else if (!enemyDict.ContainsKey(enemy.name))
                enemyDict[enemy.name] = enemy;
        }

        return enemyDict;
    }

    private static Dictionary<string, GameSaveData.EnemyData> BuildSavedLookup(List<GameSaveData.EnemyData> enemies)
    {
        Dictionary<string, GameSaveData.EnemyData> savedById = new Dictionary<string, GameSaveData.EnemyData>();

        for (int i = 0; i < enemies.Count; i++)
        {
            GameSaveData.EnemyData data = enemies[i];
            if (data == null || string.IsNullOrEmpty(data.enemyId))
                continue;

            if (!savedById.ContainsKey(data.enemyId))
                savedById[data.enemyId] = data;
        }

        return savedById;
    }

    private static bool TryFindEnemy(
        Dictionary<string, GameObject> enemyDict,
        string enemyId,
        out GameObject enemy)
    {
        enemy = null;
        if (string.IsNullOrEmpty(enemyId))
            return false;

        if (enemyDict.TryGetValue(enemyId, out enemy))
            return true;

        enemy = GameObject.Find(enemyId);
        return enemy != null;
    }

    private static string NormalizeEnemyName(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName))
            return enemyName;

        if (enemyName.EndsWith("(Clone)"))
            return enemyName.Replace("(Clone)", "").Trim();

        return enemyName;
    }

    private static bool ShouldStayDead(GameSaveData.EnemyData enemyData)
    {
        return enemyData == null || !enemyData.isAlive || enemyData.health <= 0f;
    }

    private static void ApplyPatrolGuard(
        GameObject enemy,
        PatrolConeGuardEnemy patrolGuard,
        GameSaveData.EnemyData enemyData)
    {
        if (ShouldStayDead(enemyData))
        {
            if (enemy.TryGetComponent(out EnemyHealth deadHealth))
            {
                float maxHp = enemyData.maxHealth > 0f ? enemyData.maxHealth : deadHealth.MaxHealth;
                deadHealth.SetHealth(0f, maxHp);
            }

            enemy.SetActive(false);
            return;
        }

        enemy.SetActive(true);

        if (enemy.TryGetComponent(out EnemyHealth enemyHealth))
        {
            float maxHp = enemyData.maxHealth > 0f ? enemyData.maxHealth : enemyHealth.MaxHealth;
            enemyHealth.SetHealth(enemyData.health, maxHp);
        }

        patrolGuard.ResetToPatrolStart();
    }

    private static void ApplyStandardEnemy(GameObject enemy, GameSaveData.EnemyData enemyData)
    {
        if (ShouldStayDead(enemyData))
        {
            if (enemy.TryGetComponent(out EnemyHealth deadHealth))
            {
                float maxHp = enemyData.maxHealth > 0f ? enemyData.maxHealth : deadHealth.MaxHealth;
                deadHealth.SetHealth(0f, maxHp);
            }

            enemy.SetActive(false);
            return;
        }

        enemy.SetActive(true);

        UnityEngine.AI.NavMeshAgent agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        bool agentWasEnabled = agent != null && agent.enabled;

        if (agent != null)
            agent.enabled = false;

        enemy.transform.SetPositionAndRotation(enemyData.position, enemyData.rotation);

        if (agent != null)
        {
            if (agentWasEnabled)
                agent.enabled = true;

            if (UnityEngine.AI.NavMesh.SamplePosition(
                    enemyData.position,
                    out UnityEngine.AI.NavMeshHit hit,
                    2f,
                    UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else if (agent.isOnNavMesh)
            {
                agent.Warp(enemyData.position);
            }
        }

        if (enemy.TryGetComponent(out EnemyHealth enemyHealth))
        {
            float maxHp = enemyData.maxHealth > 0f ? enemyData.maxHealth : enemyHealth.MaxHealth;
            enemyHealth.SetHealth(enemyData.health, maxHp);
        }
    }
}
