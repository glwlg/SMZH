using UnityEngine;
using UnityEngine.SceneManagement;
using XTD.Content;

namespace XTD.Flow
{
    public sealed class GameFlowController : MonoBehaviour
    {
        public static GameFlowController Instance { get; private set; }
        public RunState CurrentRun { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == "Boot")
            {
                LoadMainMenu();
            }
        }

        public void StartNewRun(ContentCatalog catalog)
        {
            CurrentRun = DemoContentFactory.CreateStartingRun(catalog ?? DemoContentFactory.CreateCatalog());
            SceneManager.LoadScene("BattlePrototype");
        }

        public void LoadMainMenu()
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
