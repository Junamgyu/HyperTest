using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 스테이지 시작/클리어 UI를 담당합니다.
/// GameLoopManager에서 호출합니다.
/// </summary>
public class GameLoopUI : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Announce Panel")]
    [SerializeField] private GameObject  _announcePanel;  // 전체 패널
    [SerializeField] private TextMeshProUGUI _announceText;  // 텍스트

    [Header("Animation")]
    [SerializeField] private float _fadeSpeed = 3f;

    // ─── Runtime State ────────────────────────────────────────────────────────
    private Coroutine _fadeCoroutine;
    private CanvasGroup _canvasGroup;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    private void Awake()
    {
        _canvasGroup = _announcePanel.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = _announcePanel.AddComponent<CanvasGroup>();

        _announcePanel.SetActive(false);
    }

    // ─── API ──────────────────────────────────────────────────────────────────
    public void ShowStageAnnounce(int stageNumber)
    {
        _announceText.text = $"Stage {stageNumber}";
        ShowPanel();
    }

    public void ShowClearAnnounce(int stageNumber)
    {
        _announceText.text = $"Stage {stageNumber}  Clear!";
        ShowPanel();
    }

    public void ShowGameClear()
    {
        _announceText.text = "Game Clear!";
        ShowPanel();
    }

    public void HideAnnounce()
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(FadeOut());
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private void ShowPanel()
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _announcePanel.SetActive(true);
        _canvasGroup.alpha = 0f;
        _fadeCoroutine = StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        while (_canvasGroup.alpha < 1f)
        {
            _canvasGroup.alpha += Time.deltaTime * _fadeSpeed;
            yield return null;
        }
        _canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOut()
    {
        while (_canvasGroup.alpha > 0f)
        {
            _canvasGroup.alpha -= Time.deltaTime * _fadeSpeed;
            yield return null;
        }
        _canvasGroup.alpha = 0f;
        _announcePanel.SetActive(false);
    }
}