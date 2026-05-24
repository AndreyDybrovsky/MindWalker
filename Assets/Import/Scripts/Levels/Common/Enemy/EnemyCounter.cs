using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class EnemyCounter : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private string enemyTag = "Enemy";
    
    [Header("События")]
    public UnityEvent<int> OnEnemyCountChanged;
    public UnityEvent OnAllEnemiesDefeated;

    private int totalEnemies = 0;
    private int defeatedEnemies = 0;
    private int remainingEnemies => totalEnemies - defeatedEnemies;

    public int TotalEnemies => totalEnemies;
    public int DefeatedEnemies => defeatedEnemies;
    public int RemainingEnemies => remainingEnemies;

    private HashSet<GameObject> trackedEnemies = new HashSet<GameObject>();
    private HashSet<GameObject> defeatedEnemySet = new HashSet<GameObject>();

    private void Start()
    {
        CountEnemies();
    }

    private void CountEnemies()
    {
        EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        totalEnemies = 0;
        defeatedEnemies = 0;
        trackedEnemies.Clear();
        defeatedEnemySet.Clear();
        
        foreach (EnemyHealth enemyHealth in enemies)
        {
            if (enemyHealth == null) continue;

            GameObject enemy = enemyHealth.gameObject;

            if (!string.IsNullOrEmpty(enemyTag) && !enemy.CompareTag(enemyTag)) continue;
            
            if (enemy.name.Contains("(Boss)")) continue;

            if (!trackedEnemies.Contains(enemy))
            {
                totalEnemies++;
                trackedEnemies.Add(enemy);

                enemyHealth.OnEnemyDeath.AddListener(() => OnEnemyDefeated(enemy));

                if (enemyHealth.IsDead)
                {
                    defeatedEnemySet.Add(enemy);
                    defeatedEnemies++;
                }
            }
        }
        
        OnEnemyCountChanged?.Invoke(remainingEnemies);
    }

    private void OnEnemyDefeated(GameObject enemy)
    {
        if (defeatedEnemySet.Contains(enemy)) return;
        if (!trackedEnemies.Contains(enemy)) return;
        
        defeatedEnemySet.Add(enemy);
        defeatedEnemies++;
        OnEnemyCountChanged?.Invoke(remainingEnemies);
        
        if (remainingEnemies <= 0 && totalEnemies > 0)
        {
            OnAllEnemiesDefeated?.Invoke();
        }
    }

    public void RefreshCount()
    {
        HashSet<GameObject> bossEnemies = new HashSet<GameObject>();
        HashSet<GameObject> bossDefeated = new HashSet<GameObject>();
        
        foreach (GameObject enemy in trackedEnemies)
        {
            if (enemy != null && enemy.name.Contains("(Boss)"))
            {
                bossEnemies.Add(enemy);
                if (defeatedEnemySet.Contains(enemy))
                {
                    bossDefeated.Add(enemy);
                }
            }
        }
        
        CountEnemies();
        
        foreach (GameObject boss in bossEnemies)
        {
            if (boss != null && !trackedEnemies.Contains(boss))
            {
                EnemyHealth bossHealth = boss.GetComponent<EnemyHealth>();
                if (bossHealth != null)
                {
                    totalEnemies++;
                    trackedEnemies.Add(boss);
                    if (bossDefeated.Contains(boss))
                    {
                        defeatedEnemySet.Add(boss);
                        defeatedEnemies++;
                    }
                }
            }
        }
        
        OnEnemyCountChanged?.Invoke(remainingEnemies);
    }
    
    public void AddEnemy(GameObject enemy, EnemyHealth enemyHealth)
    {
        if (enemy == null || enemyHealth == null || trackedEnemies.Contains(enemy)) return;
        
        totalEnemies++;
        trackedEnemies.Add(enemy);
        enemyHealth.OnEnemyDeath.AddListener(() => OnEnemyDefeated(enemy));
        
        if (enemyHealth.IsDead)
        {
            defeatedEnemySet.Add(enemy);
            defeatedEnemies++;
        }
        
        OnEnemyCountChanged?.Invoke(remainingEnemies);
    }

    public void SetState(int total, int defeated)
    {
        totalEnemies = total;
        defeatedEnemies = defeated;
        OnEnemyCountChanged?.Invoke(remainingEnemies);
    }
}
