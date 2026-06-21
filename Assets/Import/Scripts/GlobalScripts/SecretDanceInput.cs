using System.Collections;
using UnityEngine;
using ElmanGameDevTools.PlayerSystem;

/// <summary>
/// Пасхалка на Main сцене: введи S→T→R→S→T — GG станцует.
/// </summary>
public class SecretDanceInput : MonoBehaviour
{
    [Tooltip("Объект GG с Animator (Player/Player_Object/GG).")]
    [SerializeField] private Animator ggAnimator;

    [Tooltip("AnimatorController с одной Dance-анимацией.")]
    [SerializeField] private RuntimeAnimatorController danceController;

    private static readonly KeyCode[] Sequence = {
        KeyCode.S, KeyCode.T, KeyCode.R, KeyCode.S, KeyCode.T
    };

    private int  _progress;
    private bool _dancing;
    private RuntimeAnimatorController _originalController;

    private void Update()
    {
        if (_dancing) return;

        // Проверяем только нужные клавиши последовательности
        KeyCode expected = Sequence[_progress];

        // Если нажата ожидаемая клавиша — продвигаемся
        if (Input.GetKeyDown(expected))
        {
            _progress++;
            if (_progress >= Sequence.Length)
            {
                _progress = 0;
                StartCoroutine(PlayDance());
            }
            return;
        }

        // Сброс при нажатии любой другой алфавитной клавиши
        for (KeyCode kc = KeyCode.A; kc <= KeyCode.Z; kc++)
        {
            if (Input.GetKeyDown(kc))
            {
                _progress = (kc == Sequence[0]) ? 1 : 0;
                break;
            }
        }
    }

    private IEnumerator PlayDance()
    {
        if (ggAnimator == null || danceController == null) yield break;

        _dancing = true;
        _originalController = ggAnimator.runtimeAnimatorController;

        var playerCtrl = GetComponent<PlayerController>();
        if (playerCtrl != null) playerCtrl.enabled = false;

        ggAnimator.runtimeAnimatorController = danceController;
        ggAnimator.Update(0f);

        // Ждём длину клипа Dance (3.83 сек)
        yield return new WaitForSeconds(3.83f);

        ggAnimator.runtimeAnimatorController = _originalController;
        ggAnimator.Update(0f);

        if (playerCtrl != null) playerCtrl.enabled = true;
        _dancing = false;
    }
}
