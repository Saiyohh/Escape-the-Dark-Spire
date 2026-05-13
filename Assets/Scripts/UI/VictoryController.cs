using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class VictoryController : MonoBehaviour
    {
        [Header("Score labels (optional — leave null to skip)")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text timeLabel;
        [SerializeField] private TMP_Text fightsLabel;

        [Header("Buttons")]
        [SerializeField] private Button continueButton;

        [Header("Targets")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Start()
        {
            int gold     = RunContext.gold;
            float runT   = RunContext.runTime;
            int fights   = RunContext.fightsWon;

            MenuCanvasController.DestroyIfPresent();

            if (titleLabel  != null) titleLabel.text  = "Victory";
            if (goldLabel   != null) goldLabel.text   = $"Gold {gold}";
            if (timeLabel   != null) timeLabel.text   = $"Time {FormatTime(runT)}";
            if (fightsLabel != null) fightsLabel.text = $"Fights Won {fights}";

            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        }

        private void OnDestroy()
        {
            if (continueButton != null) continueButton.onClick.RemoveListener(OnContinueClicked);
        }

        private void OnContinueClicked()
        {
            var overlay = SceneTransitionOverlay.GetOrCreate();
            if (overlay != null)
                overlay.LoadSceneTransition(mainMenuSceneName);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
        }

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int m = whole / 60;
            int s = whole % 60;
            return $"{m:00}:{s:00}";
        }
    }
}
