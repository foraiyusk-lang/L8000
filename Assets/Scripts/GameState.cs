using UnityEngine;

namespace L8000
{
    /// <summary>
    /// 「残り8000回」= プレイヤーの総入力回数。移動・ジャンプ・発射のたびに減る。
    /// 0 になると硬直し、下からせり上がる「無」に飲まれる(→ VoidRiser)。
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        [Tooltip("開始時の残り回数。")]
        public int startingActions = 8000;

        [Tooltip("消費倍率。Producer 破壊などで増える(1.5 等)。")]
        public float spendMultiplier = 1f;

        public int Remaining { get; private set; }
        public bool Depleted { get; private set; }

        /// <summary>残り回数が変わった (新しい残り)。</summary>
        public static System.Action<int> OnChanged;
        /// <summary>0 に到達した(硬直開始)。</summary>
        public static System.Action OnDepleted;

        float _fracDebt;   // 倍率で生じた端数の繰り越し

        void Awake()
        {
            Instance = this;
            Application.runInBackground = true;   // エディタ非フォーカスでも進行(MCP 検証用)
            Remaining = Mathf.Max(0, startingActions);
            Depleted = Remaining == 0;

            // ドメインリロードを切っている検証時に前回の static が残らないよう明示リセット
            CreditLineBuilder.TotalCharsDestroyed = 0;
            CreditLineBuilder.Garble = false;
            CreditLineBuilder.DescentDropDestroyed = false;
            PlayerController.PhysicsAnomaly = false;
            Finale.WritingBroken = false;
        }

        void Start() => OnChanged?.Invoke(Remaining);

        /// <summary>入力 n 回ぶん消費する。0 に達したら OnDepleted。</summary>
        public void Spend(int n = 1)
        {
            if (Depleted || n <= 0) return;

            float cost = n * Mathf.Max(0.01f, spendMultiplier) + _fracDebt;
            int whole = Mathf.FloorToInt(cost);
            _fracDebt = cost - whole;
            if (whole <= 0) return;

            Remaining = Mathf.Max(0, Remaining - whole);
            OnChanged?.Invoke(Remaining);

            if (Remaining == 0 && !Depleted)
            {
                Depleted = true;
                OnDepleted?.Invoke();
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
